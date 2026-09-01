using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Installments;
using Sms.Application.Notifications;
using Sms.Application.Numbering;
using Sms.Application.ReadModels;
using Sms.Domain.Fees;
using Sms.Domain.Installments;
using Sms.Domain.Parents;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Installments
{
    /// <summary>
    /// doc/Modules/20 §8.5 / §10 and doc/Modules/19 §10 — the collection roll and
    /// its notice batches (<see cref="ICollectionFollowUp"/>).
    /// <para>
    /// <b>Everything is loaded set-wise.</b> The natural way to write this is a
    /// loop over students calling <c>GetScheduleAsync</c>, and that is the loop
    /// CLAUDE.md records having paid for twice: a thousand students would mean
    /// several thousand round trips and a change tracker that grows all the way
    /// through. Instead the whole page's charges, allocations, schedules and
    /// installment lines come back in a fixed number of queries and the waterfall
    /// is replayed in memory, exactly as <c>InstallmentAdmin.LoadScheduleAsync</c>
    /// does for one family.
    /// </para>
    /// <para>
    /// <b>Decimals are summed in memory, never in SQL</b> — <c>Sum()</c> over a
    /// decimal column compiles and then throws on Sqlite, which is what the test
    /// suite runs on and what a browser never would.
    /// </para>
    /// </summary>
    public class CollectionFollowUp : ICollectionFollowUp
    {
        /// <summary>doc 08 series for the arrears notice. Numbered because BR-INS-008's letter stage is a formal document.</summary>
        private const string NoticeSeriesCode = "DUN";

        /// <summary>
        /// doc/Modules/20 §12's "DunningLetterIssued → payer (formal)". The doc
        /// named it and nothing catalogued it until this screen needed it.
        /// <para>
        /// Deliberately not the ladder's <c>InstallmentOverdue</c>. A notice covers
        /// a whole window and has no single installment behind it, while that
        /// event's seeded wording is "Installment {InstallmentNo} of {Amount}, due
        /// on {DueDate}, is unpaid" — <c>TemplateRenderer</c> leaves an unresolved
        /// token in the text rather than throwing, so borrowing the code would put
        /// the literal word <c>{InstallmentNo}</c> in a parent's inbox.
        /// </para>
        /// </summary>
        private const string NoticeEventCode = "DunningLetterIssued";

        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IWorkingYearContext _workingYear;
        private readonly INumberIssuer _numberIssuer;
        private readonly INotificationPublisher _notifications;

        public CollectionFollowUp(
            AppDbContext db,
            IClock clock,
            IWorkingYearContext workingYear,
            INumberIssuer numberIssuer,
            INotificationPublisher notifications)
        {
            _db = db;
            _clock = clock;
            _workingYear = workingYear;
            _numberIssuer = numberIssuer;
            _notifications = notifications;
        }

        public async Task<CollectionRoll> GetRollAsync(
            CollectionFilter filter, int take = OutstandingWindowEvaluator.DefaultPageSize, CancellationToken cancellationToken = default)
        {
            filter ??= new CollectionFilter();
            GuardWindow(filter);

            var rows = await BuildAsync(filter, studentIds: null, cancellationToken);
            var page = rows.Take(take).ToList();

            // Totalled over everything matched, not over the page. A collection officer reading
            // "outstanding: 812,400" under a truncated grid is entitled to have that be the school's
            // number rather than the first two hundred rows of it.
            var total = rows.Sum(r => r.Position.Outstanding);
            var chaseable = rows.Sum(r => r.Position.Notifiable);
            return new CollectionRoll(page, rows.Count, rows.Count > page.Count, total, chaseable);
        }

        public async Task<NoticeBatch> IssueNoticesAsync(
            IReadOnlyCollection<int> studentIds,
            CollectionNoticeChannel channel,
            CollectionFilter window,
            CancellationToken cancellationToken = default)
        {
            window ??= new CollectionFilter();
            GuardWindow(window);

            var selected = (studentIds ?? Array.Empty<int>()).Distinct().ToList();
            if (selected.Count == 0)
            {
                return new NoticeBatch(Array.Empty<IssuedNotice>(), 0, 0, 0);
            }

            // Rebuilt from the database rather than trusted from the form. The screen posts student
            // ids; the amount printed on a notice must be what the system says is owed at the moment
            // it is issued, not what a page rendered ten minutes ago said.
            var rows = await BuildAsync(window, selected, cancellationToken);
            var byStudent = rows.ToDictionary(r => r.StudentId);

            var issued = new List<IssuedNotice>();
            var nothingOutstanding = 0;
            var pdcCovered = 0;
            var noPortalAccount = 0;
            var now = _clock.UtcNow;

            foreach (var studentId in selected)
            {
                if (!byStudent.TryGetValue(studentId, out var row) || row.Position.Outstanding <= 0m)
                {
                    nothingOutstanding++;
                    continue;
                }

                // BR-INS-009. Only a family whose whole window balance sits behind a cheque is
                // spared: a partly covered one is still owed the difference, and the notice is
                // raised for that difference alone.
                if (row.Position.Notifiable <= 0m)
                {
                    pdcCovered++;
                    continue;
                }

                if (channel == CollectionNoticeChannel.Portal && !row.GuardianHasPortalAccount)
                {
                    noPortalAccount++;
                    continue;
                }

                var notice = new CollectionNotice
                {
                    NoticeNo = await _numberIssuer.IssueAsync(NoticeSeriesCode, cancellationToken),
                    StudentId = row.StudentId,
                    PayerId = row.PayerId,
                    Channel = channel,
                    WindowFrom = window.From?.Date,
                    WindowTo = window.To?.Date,
                    AmountDue = row.Position.Notifiable,
                    IssuedAtUtc = now,
                };
                _db.CollectionNotices.Add(notice);
                issued.Add(new IssuedNotice(notice, row));

                if (channel == CollectionNoticeChannel.Portal)
                {
                    await PublishPortalNoticeAsync(row, notice, cancellationToken);
                }
            }

            // One save for the batch: the notice log, the numbering series' advance and the queued
            // deliveries commit together or not at all. INumberIssuer and INotificationPublisher are
            // both ambient by contract (CLAUDE.md's "two service shapes") and neither saves itself,
            // which is what makes "a notice number exists only with the notice it stamps" true.
            await _db.SaveChangesAsync(cancellationToken);
            return new NoticeBatch(issued, nothingOutstanding, pdcCovered, noPortalAccount);
        }

        private static void GuardWindow(CollectionFilter filter)
        {
            if (!OutstandingWindowEvaluator.IsWindowValid(filter.From, filter.To))
            {
                throw new InvalidCollectionWindowException(filter.From!.Value, filter.To!.Value);
            }
        }

        private async Task PublishPortalNoticeAsync(CollectionRow row, CollectionNotice notice, CancellationToken cancellationToken)
        {
            var recipients = await PortalRecipientsAsync(row.StudentId, cancellationToken);
            if (recipients.Count == 0)
            {
                return;
            }

            // Values, not sentences. The template renders them in the recipient's own language
            // (BR-NOT-001) — composing the wording here would put one language in every inbox.
            var payload = new Dictionary<string, string>
            {
                ["NoticeNo"] = notice.NoticeNo,
                ["StudentNo"] = row.StudentNo,
                ["Amount"] = notice.AmountDue.ToString("0.00", CultureInfo.InvariantCulture),
                ["DueItems"] = row.Position.ItemCount.ToString(CultureInfo.InvariantCulture),
                ["DueDate"] = row.Position.OldestDueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                ["WindowFrom"] = notice.WindowFrom?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                ["WindowTo"] = notice.WindowTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            };

            await _notifications.PublishAsync(NoticeEventCode, recipients, payload, cancellationToken);
        }

        /// <summary>
        /// BR-PAR-005 / BR-SEC-011: the guardians who may be told, which is not the
        /// same set as the guardians on file. Only a linked guardian with a portal
        /// sign-in receives anything, and a link that has ended receives nothing.
        /// </summary>
        private async Task<IReadOnlyCollection<NotificationRecipient>> PortalRecipientsAsync(int studentId, CancellationToken cancellationToken)
        {
            var parentIds = await _db.StudentGuardianLinks.AsNoTracking()
                .Where(l => l.StudentId == studentId && l.EffectiveToUtc == null)
                .Select(l => l.ParentId).ToListAsync(cancellationToken);

            var parents = await _db.Parents.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.SchoolId == _db.CurrentSchoolId && parentIds.Contains(p.Id) && p.UserAccountId != null)
                .Select(p => new { p.UserAccountId, p.PreferredLanguage }).ToListAsync(cancellationToken);

            return parents
                .Select(p => new NotificationRecipient(p.UserAccountId!.Value, p.PreferredLanguage))
                .ToList();
        }

        // ------------------------------------------------------------------ the roll

        private async Task<List<CollectionRow>> BuildAsync(CollectionFilter filter, IReadOnlyList<int>? studentIds, CancellationToken cancellationToken)
        {
            var yearId = filter.AcademicYearId ?? _workingYear.AcademicYearId;
            var asOf = _clock.UtcNow;

            // Retired grades and sections are read through the filter being ignored: a grade nobody
            // teaches any more still names last year's charges, and a blank column on an arrears
            // letter is worse than a retired name on one.
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync(cancellationToken);
            var profiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.AcademicYearId == yearId).ToListAsync(cancellationToken);
            var sections = await _db.Sections.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _db.CurrentSchoolId && s.AcademicYearId == yearId).ToListAsync(cancellationToken);

            // KNOWN LIMITATION, stated rather than hidden: the roll is the year's active enrolment,
            // so a child who withdrew mid-year owing money does not appear on it. That is the same
            // shape FeesController.StudentFinance takes and the reason is consistency — two finance
            // screens that disagree about who is on the roll is a worse defect than a narrow one that
            // does not. Chasing leavers needs a decision about which year's screen owns them and how
            // far back the roll reaches, which is an owner question, not one to answer here.
            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => e.AcademicYearId == yearId && e.Status == EnrollmentStatus.Active && e.ExitDate == null)
                .ToListAsync(cancellationToken);

            if (filter.GradeLevelId != null)
            {
                var inGrade = profiles.Where(p => p.GradeLevelId == filter.GradeLevelId).Select(p => p.Id).ToHashSet();
                enrollments = enrollments.Where(e => inGrade.Contains(e.GradeYearProfileId)).ToList();
            }

            var memberships = await _db.SectionMemberships.AsNoTracking()
                .Where(x => x.AcademicYearId == yearId && x.EffectiveToUtc == null).ToListAsync(cancellationToken);
            if (filter.SectionId != null)
            {
                var inSection = memberships.Where(x => x.SectionId == filter.SectionId).Select(x => x.EnrollmentId).ToHashSet();
                enrollments = enrollments.Where(e => inSection.Contains(e.Id)).ToList();
            }

            var enrolledIds = enrollments.Select(e => e.StudentId).Distinct().ToList();

            // A notice run names its students explicitly. Narrowing to them here rather than
            // filtering the finished roll keeps a thirty-family batch from loading the whole school.
            if (studentIds != null)
            {
                var wanted = studentIds.ToHashSet();
                enrolledIds = enrolledIds.Where(wanted.Contains).ToList();
            }

            // IgnoreQueryFilters with an explicit school id, the same shape the student finance roll
            // uses: a withdrawn child still owes what they owed, and the soft-active filter would
            // quietly drop exactly the families a collection screen exists to find.
            var enrolledSet = enrolledIds.ToHashSet();
            var enrolledIn = InClause(enrolledIds);
            var studentQuery = _db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => s.SchoolId == _db.CurrentSchoolId);
            if (enrolledIn != null)
            {
                studentQuery = studentQuery.Where(s => enrolledIn.Contains(s.Id));
            }

            var students = (await studentQuery.ToListAsync(cancellationToken)).Where(s => enrolledSet.Contains(s.Id)).ToList();

            if (!string.IsNullOrWhiteSpace(filter.Query))
            {
                var t = filter.Query.Trim();
                students = students.Where(s =>
                    s.StudentNo.Contains(t, StringComparison.OrdinalIgnoreCase)
                    || Has(s.FirstNameAr, t) || Has(s.FatherNameAr, t) || Has(s.FamilyNameAr, t)
                    || Has(s.FirstNameEn, t) || Has(s.FatherNameEn, t) || Has(s.FamilyNameEn, t)).ToList();
            }

            if (students.Count == 0)
            {
                return new List<CollectionRow>();
            }

            var ids = students.Select(s => s.Id).ToList();
            var dues = await DueItemsAsync(ids, cancellationToken);
            var guardians = await GuardiansAsync(ids, cancellationToken);
            var lastNotices = await LastNoticesAsync(ids, cancellationToken);

            var rows = new List<CollectionRow>();
            foreach (var student in students)
            {
                var position = OutstandingWindowEvaluator.Position(
                    dues.TryGetValue(student.Id, out var items) ? items : Array.Empty<DueItem>(), filter.From, filter.To);
                if (position.Outstanding <= 0m)
                {
                    continue;
                }

                if (filter.NotifiableOnly && position.Notifiable <= 0m)
                {
                    continue;
                }

                // The bucket is taken from the oldest unpaid item in the window, through the same
                // engine the finance dashboard's aging donut uses — so "31–60 days" means one thing
                // in this product, not two.
                var bucket = ReceivablesAgingBucketer.Bucket(position.OldestDueDate ?? asOf, asOf);
                if (filter.Bucket != null && bucket != filter.Bucket)
                {
                    continue;
                }

                var enrollment = enrollments.FirstOrDefault(e => e.StudentId == student.Id);
                var profile = enrollment == null ? null : profiles.FirstOrDefault(p => p.Id == enrollment.GradeYearProfileId);
                var grade = profile == null ? null : grades.FirstOrDefault(g => g.Id == profile.GradeLevelId);
                var membership = enrollment == null ? null : memberships.FirstOrDefault(x => x.EnrollmentId == enrollment.Id);
                var section = membership == null ? null : sections.FirstOrDefault(s => s.Id == membership.SectionId);
                guardians.TryGetValue(student.Id, out var guardian);
                lastNotices.TryGetValue(student.Id, out var lastNotice);

                rows.Add(new CollectionRow(
                    student.Id,
                    student.StudentNo,
                    $"{student.FirstNameAr} {student.FatherNameAr} {student.FamilyNameAr}".Trim(),
                    $"{student.FirstNameEn} {student.FatherNameEn} {student.FamilyNameEn}".Trim(),
                    grade?.Name.NameAr,
                    grade?.Name.NameEn,
                    section?.NameAr,
                    section?.NameEn,
                    guardian?.PayerId,
                    guardian?.NameAr,
                    guardian?.NameEn,
                    guardian?.Mobile,
                    guardian?.IsResponsible ?? false,
                    guardian?.HasPortalAccount ?? false,
                    position,
                    bucket,
                    lastNotice?.IssuedAtUtc,
                    lastNotice?.Channel));
            }

            // Oldest arrears first, then the largest of them. That is the order the money is chased
            // in: a family six months behind is a different conversation from one that missed last
            // Tuesday, and sorting by name would bury the first among the second.
            return rows
                .OrderBy(r => r.Position.OldestDueDate ?? DateTime.MaxValue)
                .ThenByDescending(r => r.Position.Outstanding)
                .ThenBy(r => r.StudentNo, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool Has(string? value, string term)
            => !string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Above this many ids, an <c>IN (…)</c> stops being an optimisation and
        /// starts being a failure: Sqlite caps a statement at 999 parameters and
        /// throws when a list overruns it, and SQL Server degrades badly long
        /// before its own limit. A whole-school roll passes every enrolled child's
        /// id, so this is reached on the second real deployment, not the hundredth.
        /// </summary>
        private const int InClauseLimit = 400;

        /// <summary>
        /// The ids to put in an <c>IN (…)</c>, or null to let the query run over
        /// the whole tenant and be narrowed in memory afterwards — the same trade
        /// <c>SnapshotRefreshService.RefreshAgedReceivablesAsync</c> makes. The
        /// tenant filter applies either way, so the wide branch never reads past
        /// the school.
        /// <para>
        /// Every caller still filters its result against the id set, which makes
        /// the returned list correct under both branches and keeps the two from
        /// drifting apart.
        /// </para>
        /// </summary>
        private static List<int>? InClause(IReadOnlyCollection<int> ids)
            => ids.Count <= InClauseLimit ? ids.ToList() : null;

        // ------------------------------------------------------------------ what is owed

        /// <summary>
        /// Every unpaid thing, per student, from both of the product's notions of
        /// "due": a schedule row with its own due date, and a posted charge no
        /// schedule covers, aged from its posting date.
        /// </summary>
        private async Task<Dictionary<int, List<DueItem>>> DueItemsAsync(IReadOnlyList<int> studentIds, CancellationToken cancellationToken)
        {
            var wanted = studentIds.ToHashSet();
            var studentIn = InClause(studentIds);

            var chargeQuery = _db.Charges.AsNoTracking().Where(c => c.Status == ChargeStatus.Posted);
            if (studentIn != null)
            {
                chargeQuery = chargeQuery.Where(c => studentIn.Contains(c.StudentId));
            }

            var charges = (await chargeQuery.Select(c => new { c.Id, c.StudentId, c.GrossAmount, c.PostedAtUtc }).ToListAsync(cancellationToken))
                .Where(c => wanted.Contains(c.StudentId)).ToList();
            if (charges.Count == 0)
            {
                return new Dictionary<int, List<DueItem>>();
            }

            // The charge ids follow the students, so the same cap governs them — a whole-school roll
            // has more charges than children, not fewer.
            var chargeIds = charges.Select(c => c.Id).ToHashSet();
            var chargeIn = InClause(chargeIds);
            // The IN clause goes on the entity, never on the projection: EF Core 5 cannot translate a
            // Where over `new ChargeAmount(...).ChargeId` and throws at runtime — it compiles, and the
            // failure only shows when the query actually runs.
            var creditQuery = _db.CreditNotes.AsNoTracking().AsQueryable();
            var discountQuery = _db.DiscountDocuments.AsNoTracking().AsQueryable();
            var allocationQuery = _db.PaymentAllocations.AsNoTracking().AsQueryable();
            if (chargeIn != null)
            {
                creditQuery = creditQuery.Where(n => chargeIn.Contains(n.ChargeId));
                discountQuery = discountQuery.Where(d => chargeIn.Contains(d.ChargeId));
                allocationQuery = allocationQuery.Where(a => chargeIn.Contains(a.ChargeId));
            }

            var credited = SumByCharge(
                await creditQuery.Select(n => new ChargeAmount(n.ChargeId, n.Amount)).ToListAsync(cancellationToken), chargeIds);
            var discounted = SumByCharge(
                await discountQuery.Select(d => new ChargeAmount(d.ChargeId, d.Amount)).ToListAsync(cancellationToken), chargeIds);
            var allocated = SumByCharge(
                await allocationQuery.Select(a => new ChargeAmount(a.ChargeId, a.AllocatedAmount)).ToListAsync(cancellationToken), chargeIds);

            var assignmentQuery = _db.PlanAssignments.AsNoTracking();
            if (studentIn != null)
            {
                assignmentQuery = assignmentQuery.Where(a => studentIn.Contains(a.StudentId));
            }

            var assignments = (await assignmentQuery.Select(a => new { a.Id, a.StudentId }).ToListAsync(cancellationToken))
                .Where(a => wanted.Contains(a.StudentId)).ToList();
            var assignmentIds = assignments.Select(a => a.Id).ToHashSet();
            var assignmentIn = InClause(assignmentIds);

            var installmentQuery = _db.Installments.AsNoTracking();
            if (assignmentIn != null)
            {
                installmentQuery = installmentQuery.Where(i => assignmentIn.Contains(i.PlanAssignmentId));
            }

            var installments = (await installmentQuery.OrderBy(i => i.DueDate).ThenBy(i => i.SequenceNumber).ToListAsync(cancellationToken))
                .Where(i => assignmentIds.Contains(i.PlanAssignmentId)).ToList();
            var installmentIds = installments.Select(i => i.Id).ToHashSet();
            var installmentIn = InClause(installmentIds);

            var lineQuery = _db.InstallmentChargeLines.AsNoTracking();
            if (installmentIn != null)
            {
                lineQuery = lineQuery.Where(l => installmentIn.Contains(l.InstallmentId));
            }

            var lines = (await lineQuery.Select(l => new { l.InstallmentId, l.ChargeId, l.Amount }).ToListAsync(cancellationToken))
                .Where(l => installmentIds.Contains(l.InstallmentId)).ToList();

            var collectibleInstallmentIds = installments.Where(i => !i.IsSuperseded && !i.IsWrittenOff).Select(i => i.Id).ToHashSet();
            var scheduledByCharge = lines
                .Where(l => collectibleInstallmentIds.Contains(l.InstallmentId))
                .GroupBy(l => l.ChargeId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var result = new Dictionary<int, List<DueItem>>();
            List<DueItem> For(int studentId)
            {
                if (!result.TryGetValue(studentId, out var list))
                {
                    list = new List<DueItem>();
                    result[studentId] = list;
                }

                return list;
            }

            // --- scheduled: one item per installment, paid by the same waterfall the family
            // schedule screen uses, so the two never disagree about which installment a receipt paid.
            var linesByInstallment = lines.GroupBy(l => l.InstallmentId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var assignment in assignments)
            {
                var rows = installments.Where(i => i.PlanAssignmentId == assignment.Id).ToList();
                if (rows.Count == 0)
                {
                    continue;
                }

                var scheduledHere = rows.SelectMany(r => linesByInstallment.TryGetValue(r.Id, out var l) ? l : new())
                    .GroupBy(l => l.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
                var totalPaid = scheduledHere.Sum(kv => Math.Min(kv.Value, allocated.TryGetValue(kv.Key, out var a) ? a : 0m));

                var collectible = rows.Where(r => !r.IsSuperseded && !r.IsWrittenOff).ToList();
                var paidPer = InstallmentPaymentWaterfall.Apply(collectible.Select(r => r.Amount).ToList(), totalPaid);

                for (var i = 0; i < collectible.Count; i++)
                {
                    var row = collectible[i];
                    For(assignment.StudentId).Add(new DueItem(
                        DueItemSource.Installment, row.DueDate, row.Amount, paidPer[i], row.CoveringPdcId != null, IsCollectible: true));
                }
            }

            // --- unscheduled: the part of a posted charge no live schedule row covers. Its settled
            // amount is what the schedules did not already claim, which is the same split
            // LoadScheduleAsync makes — so a charge is never counted twice, and never counted zero
            // times either.
            foreach (var charge in charges)
            {
                var net = charge.GrossAmount
                    - (credited.TryGetValue(charge.Id, out var cr) ? cr : 0m)
                    - (discounted.TryGetValue(charge.Id, out var di) ? di : 0m);
                var scheduled = scheduledByCharge.TryGetValue(charge.Id, out var sc) ? sc : 0m;
                var unscheduled = net - scheduled;
                if (unscheduled <= 0m)
                {
                    continue;
                }

                var paid = allocated.TryGetValue(charge.Id, out var al) ? al : 0m;
                var settled = paid - scheduled;
                For(charge.StudentId).Add(new DueItem(
                    DueItemSource.UnscheduledCharge, charge.PostedAtUtc, unscheduled, settled < 0m ? 0m : settled, IsPdcCovered: false, IsCollectible: true));
            }

            return result;
        }

        /// <summary>A charge id and an amount taken off it — projected so the sum happens in memory, never as SQL.</summary>
        private sealed record ChargeAmount(int ChargeId, decimal Amount);

        /// <summary>
        /// What has been taken off each charge, totalled per charge. The sum is in
        /// memory on purpose: <c>Sum()</c> over a decimal column compiles and then
        /// throws on Sqlite.
        /// </summary>
        private static Dictionary<int, decimal> SumByCharge(IReadOnlyList<ChargeAmount> rows, HashSet<int> chargeIds)
            => rows
                .Where(x => chargeIds.Contains(x.ChargeId))
                .GroupBy(x => x.ChargeId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        // ------------------------------------------------------------------ who to address it to

        private sealed record GuardianCard(string NameAr, string NameEn, string? Mobile, bool IsResponsible, bool HasPortalAccount, int? PayerId);

        /// <summary>
        /// BR-PAR-005: the guardian the school bills, falling back to the primary
        /// contact only where nobody has been made responsible. Addressing an
        /// arrears letter to whichever parent happened to be linked first is how it
        /// reaches the one who does not pay.
        /// </summary>
        private async Task<Dictionary<int, GuardianCard>> GuardiansAsync(IReadOnlyList<int> studentIds, CancellationToken cancellationToken)
        {
            var wanted = studentIds.ToHashSet();
            var studentIn = InClause(studentIds);
            var linkQuery = _db.StudentGuardianLinks.AsNoTracking().Where(l => l.EffectiveToUtc == null);
            if (studentIn != null)
            {
                linkQuery = linkQuery.Where(l => studentIn.Contains(l.StudentId));
            }

            var links = (await linkQuery.ToListAsync(cancellationToken)).Where(l => wanted.Contains(l.StudentId)).ToList();
            if (links.Count == 0)
            {
                return new Dictionary<int, GuardianCard>();
            }

            var parentIds = links.Select(l => l.ParentId).Distinct().ToHashSet();
            var parentIn = InClause(parentIds);
            var parentQuery = _db.Parents.IgnoreQueryFilters().AsNoTracking().Where(p => p.SchoolId == _db.CurrentSchoolId);
            if (parentIn != null)
            {
                parentQuery = parentQuery.Where(p => parentIn.Contains(p.Id));
            }

            var parents = (await parentQuery.ToListAsync(cancellationToken)).Where(p => parentIds.Contains(p.Id)).ToList();

            // Payers are read for the parents on this roll, not for the whole school: a payer row per
            // guardian means the unfiltered read grows with the school and answers nothing extra.
            var payerQuery = _db.Payers.AsNoTracking().Where(p => p.ParentId != null);
            if (parentIn != null)
            {
                payerQuery = payerQuery.Where(p => parentIn.Contains(p.ParentId!.Value));
            }

            var payers = (await payerQuery.ToListAsync(cancellationToken))
                .Where(p => p.ParentId != null && parentIds.Contains(p.ParentId.Value)).ToList();

            var cards = new Dictionary<int, GuardianCard>();
            foreach (var studentId in studentIds)
            {
                var mine = links.Where(l => l.StudentId == studentId).ToList();
                var chosen = mine.FirstOrDefault(l => l.IsFinanciallyResponsible)
                    ?? mine.FirstOrDefault(l => l.IsPrimaryContact)
                    ?? mine.FirstOrDefault();
                var parent = chosen == null ? null : parents.FirstOrDefault(p => p.Id == chosen.ParentId);
                if (parent == null)
                {
                    continue;
                }

                // The payer is only resolved from the *responsible* guardian. A notice addressed to
                // the primary contact is a courtesy copy; billing the wrong parent is not.
                var responsible = mine.FirstOrDefault(l => l.IsFinanciallyResponsible);
                var payerId = responsible == null ? null : payers.FirstOrDefault(p => p.ParentId == responsible.ParentId)?.Id;

                cards[studentId] = new GuardianCard(
                    parent.NameAr, parent.NameEn, string.IsNullOrWhiteSpace(parent.PrimaryMobile) ? null : parent.PrimaryMobile,
                    chosen!.IsFinanciallyResponsible, parent.UserAccountId != null, payerId);
            }

            return cards;
        }

        private sealed record NoticeStamp(DateTime IssuedAtUtc, CollectionNoticeChannel Channel);

        /// <summary>
        /// The most recent notice per student, so the roll can say who has already
        /// been written to. Without it a family gets three letters in a week from
        /// three officers, which is the complaint that makes a school stop using the
        /// screen.
        /// </summary>
        private async Task<Dictionary<int, NoticeStamp>> LastNoticesAsync(IReadOnlyList<int> studentIds, CancellationToken cancellationToken)
        {
            var wanted = studentIds.ToHashSet();
            var studentIn = InClause(studentIds);
            var query = _db.CollectionNotices.AsNoTracking();
            if (studentIn != null)
            {
                query = query.Where(n => studentIn.Contains(n.StudentId));
            }

            return (await query.Select(n => new { n.StudentId, n.IssuedAtUtc, n.Channel }).ToListAsync(cancellationToken))
                .Where(n => wanted.Contains(n.StudentId))
                .GroupBy(n => n.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.IssuedAtUtc).Select(x => new NoticeStamp(x.IssuedAtUtc, x.Channel)).First());
        }
    }
}
