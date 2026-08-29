using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Fees;
using Sms.Application.Audit;
using Sms.Application.Calendar;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Guards;
using Sms.Application.Common.Interfaces;
using Sms.Application.Installments;
using Sms.Application.Notifications;
using Sms.Domain.Calendar;
using Sms.Domain.Fees;
using Sms.Domain.Installments;
using Sms.Domain.Payments;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Installments
{
    /// <summary>
    /// Standalone admin operations — save themselves. Installment status
    /// is never stored: every read derives it from Module 21 allocations
    /// (BR-INS-007) via <see cref="LoadScheduleAsync"/>.
    /// </summary>
    public class InstallmentAdmin : IInstallmentAdmin
    {
        public const string DueSoonEventCode = "InstallmentDueSoon";
        public const string OverdueEventCode = "InstallmentOverdue";

        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;
        private readonly IWorkingYearContext _workingYear;
        private readonly INotificationPublisher _notifications;
        private readonly IFeeAdmin _fees;

        public InstallmentAdmin(AppDbContext db, IClock clock, IAuditContext audit, IWorkingYearContext workingYear, INotificationPublisher notifications, IFeeAdmin fees)
        {
            _db = db;
            _clock = clock;
            _audit = audit;
            _workingYear = workingYear;
            _notifications = notifications;
            _fees = fees;
        }

        // ------------------------------------------------------------------ templates

        public async Task<PlanTemplate> DefineTemplateAsync(
            int academicYearId, string nameAr, string nameEn, IReadOnlyList<TemplateSplit> splits,
            int? feeCategoryId = null, decimal downPaymentPercent = 0m, int graceDays = 0, CancellationToken cancellationToken = default)
        {
            if (splits.Count == 0 || !InstallmentScheduleBuilder.SplitsSumToHundred(splits.Select(s => s.Percent).ToList()))
            {
                throw new InvalidTemplateSplitException(TemplateSplitFault.SplitsDoNotSumTo100);
            }

            if (splits.Any(s => s.DueDate == null && s.OffsetDaysFromYearStart == null))
            {
                throw new InvalidTemplateSplitException(TemplateSplitFault.SplitHasNoDueDateRule);
            }

            var template = new PlanTemplate
            {
                AcademicYearId = academicYearId, NameAr = nameAr, NameEn = nameEn, FeeCategoryId = feeCategoryId,
                DownPaymentPercent = downPaymentPercent, GraceDays = graceDays,
            };
            for (var i = 0; i < splits.Count; i++)
            {
                template.Installments.Add(new TemplateInstallment
                {
                    SequenceNumber = i + 1, SplitPercent = splits[i].Percent, DueDate = splits[i].DueDate?.Date,
                    OffsetDaysFromYearStart = splits[i].OffsetDaysFromYearStart,
                });
            }

            _db.PlanTemplates.Add(template);
            await _db.SaveChangesAsync(cancellationToken);
            return template;
        }

        public async Task<PlanTemplate> UpdateTemplateAsync(
            int planTemplateId, string nameAr, string nameEn, IReadOnlyList<TemplateSplit> splits,
            int? feeCategoryId = null, decimal downPaymentPercent = 0m, int graceDays = 0, CancellationToken cancellationToken = default)
        {
            var template = await _db.PlanTemplates.Include(t => t.Installments)
                .SingleAsync(t => t.Id == planTemplateId, cancellationToken);

            if (template.Status != PlanTemplateStatus.Draft)
            {
                throw new PlanTemplateNotDraftException(planTemplateId);
            }

            ValidateSplits(splits);

            template.NameAr = nameAr;
            template.NameEn = nameEn;
            template.FeeCategoryId = feeCategoryId;
            template.DownPaymentPercent = downPaymentPercent;
            template.GraceDays = graceDays;

            // The splits are replaced wholesale rather than diffed. Sequence numbers are positional and
            // carry no identity — nothing references a TemplateInstallment, because a schedule copies the
            // shape at assignment rather than pointing back at it — so matching them up would be work in
            // service of a relationship that does not exist.
            _db.TemplateInstallments.RemoveRange(template.Installments);
            template.Installments.Clear();
            for (var i = 0; i < splits.Count; i++)
            {
                template.Installments.Add(new TemplateInstallment
                {
                    SequenceNumber = i + 1, SplitPercent = splits[i].Percent, DueDate = splits[i].DueDate?.Date,
                    OffsetDaysFromYearStart = splits[i].OffsetDaysFromYearStart,
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            return template;
        }

        public async Task DeleteTemplateAsync(int planTemplateId, CancellationToken cancellationToken = default)
        {
            var template = await _db.PlanTemplates.Include(t => t.Installments)
                .SingleAsync(t => t.Id == planTemplateId, cancellationToken);

            var assignments = await _db.PlanAssignments.CountAsync(a => a.PlanTemplateId == planTemplateId, cancellationToken);
            if (assignments > 0)
            {
                throw new RecordInUseException(UsageReport.From(
                    new UsageReference("assigned plan(s)", "خطة مُسنَدة", assignments)));
            }

            _db.TemplateInstallments.RemoveRange(template.Installments);
            _db.PlanTemplates.Remove(template);
            await _db.SaveChangesAsync(cancellationToken);
        }

        private static void ValidateSplits(IReadOnlyList<TemplateSplit> splits)
        {
            if (splits.Count == 0 || !InstallmentScheduleBuilder.SplitsSumToHundred(splits.Select(s => s.Percent).ToList()))
            {
                throw new InvalidTemplateSplitException(TemplateSplitFault.SplitsDoNotSumTo100);
            }

            if (splits.Any(s => s.DueDate == null && s.OffsetDaysFromYearStart == null))
            {
                throw new InvalidTemplateSplitException(TemplateSplitFault.SplitHasNoDueDateRule);
            }
        }

        public async Task ApproveTemplateAsync(int planTemplateId, CancellationToken cancellationToken = default)
        {
            var template = await _db.PlanTemplates.SingleAsync(t => t.Id == planTemplateId, cancellationToken);
            template.Status = PlanTemplateStatus.Approved;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ assignment + schedule generation

        private async Task<Func<DateTime, bool>> BuildWorkingDayPredicateAsync(int academicYearId, ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken)
        {
            var overrides = await _db.CalendarDays
                .Where(d => d.AcademicYearId == academicYearId)
                .ToDictionaryAsync(d => d.Date.Date, d => d.DayType, cancellationToken);
            return date => CalendarDayResolver.Resolve(date, weekendDays, overrides) == DayType.Working;
        }

        private async Task<IReadOnlyList<InstallmentScheduleBuilder.ChargePortion>> LoadNetChargePortionsAsync(IReadOnlyList<int> chargeIds, CancellationToken cancellationToken)
        {
            var charges = await _db.Charges.Where(c => chargeIds.Contains(c.Id)).OrderBy(c => c.PostedAtUtc).ThenBy(c => c.Id).ToListAsync(cancellationToken);
            // EF Core's Sqlite provider can't translate Sum() over decimal - materialize then sum in memory.
            var creditRows = await _db.CreditNotes.Where(n => chargeIds.Contains(n.ChargeId)).Select(n => new { n.ChargeId, n.Amount }).ToListAsync(cancellationToken);
            var creditedByCharge = creditRows.GroupBy(r => r.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            // S5/E-502: discount documents already applied to a charge are net of the schedule too (BR-DIS-005) -
            // a discount approved AFTER assignment reaches the schedule via ReduceScheduleAsync instead.
            var discountRows = await _db.DiscountDocuments.Where(d => chargeIds.Contains(d.ChargeId)).Select(d => new { d.ChargeId, d.Amount }).ToListAsync(cancellationToken);
            var discountedByCharge = discountRows.GroupBy(r => r.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            return charges
                .Select(c => new InstallmentScheduleBuilder.ChargePortion(
                    c.Id,
                    c.GrossAmount
                    - (creditedByCharge.TryGetValue(c.Id, out var cr) ? cr : 0m)
                    - (discountedByCharge.TryGetValue(c.Id, out var ds) ? ds : 0m)))
                .Where(p => p.Amount > 0m)
                .ToList();
        }

        private async Task<PlanTemplate> LoadApprovedTemplateAsync(int planTemplateId, CancellationToken cancellationToken)
        {
            var template = await _db.PlanTemplates.Include(t => t.Installments).SingleAsync(t => t.Id == planTemplateId, cancellationToken);
            if (template.Status != PlanTemplateStatus.Approved)
            {
                throw new PlanTemplateNotApprovedException(planTemplateId);
            }

            return template;
        }

        /// <summary>BR-INS-002: where the template's splits land in this year, each shifted off a non-working day.</summary>
        private async Task<IReadOnlyList<DateTime>> DueDatesAsync(PlanTemplate template, int yearId, ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken)
        {
            var year = await _db.AcademicYears.SingleAsync(y => y.Id == yearId, cancellationToken);
            var isWorkingDay = await BuildWorkingDayPredicateAsync(yearId, weekendDays, cancellationToken);
            return template.Installments.OrderBy(i => i.SequenceNumber)
                .Select(s => s.DueDate ?? year.StartDate.AddDays(s.OffsetDaysFromYearStart!.Value))
                .Select(d => DueDateShifter.ShiftToWorkingDay(d, isWorkingDay))
                .ToList();
        }

        private Task<bool> HasPlanAsync(int studentId, int yearId, int? feeCategoryId, CancellationToken cancellationToken)
            => _db.PlanAssignments.AnyAsync(
                a => a.StudentId == studentId && a.AcademicYearId == yearId && a.FeeCategoryId == feeCategoryId, cancellationToken);

        /// <summary>
        /// Builds the schedule graph. Nothing here reads the database, so a batch may keep one
        /// template and one set of due dates across a whole grade without re-deriving them per
        /// student — and the template stays a detached read even after <c>ChangeTracker.Clear()</c>,
        /// because only its id is written onto the assignment.
        /// </summary>
        private static PlanAssignment BuildAssignment(
            PlanTemplate template, IReadOnlyList<DateTime> dueDates, int yearId, int studentId, int payerId,
            IReadOnlyList<InstallmentScheduleBuilder.ChargePortion> portions, bool isException, string? exceptionReason)
        {
            var splits = template.Installments.OrderBy(i => i.SequenceNumber).ToList();
            var total = portions.Sum(p => p.Amount);
            var scheduled = InstallmentScheduleBuilder.Build(total, splits.Select(s => s.SplitPercent).ToList(), dueDates);
            var lines = InstallmentScheduleBuilder.MapChargesToInstallments(portions, scheduled.Select(s => s.Amount).ToList());

            var assignment = new PlanAssignment
            {
                AcademicYearId = yearId, StudentId = studentId, PayerId = payerId, PlanTemplateId = template.Id,
                FeeCategoryId = template.FeeCategoryId, IsException = isException, ExceptionReason = exceptionReason,
            };
            foreach (var s in scheduled)
            {
                var installment = new Installment { SequenceNumber = s.SequenceNumber, DueDate = s.DueDate, Amount = s.Amount };
                foreach (var line in lines.Where(l => l.InstallmentIndex == s.SequenceNumber - 1))
                {
                    installment.ChargeLines.Add(new InstallmentChargeLine { ChargeId = line.ChargeId, Amount = line.Amount });
                }

                assignment.Installments.Add(installment);
            }

            return assignment;
        }

        public async Task<PlanAssignment> AssignPlanAsync(
            int studentId, int payerId, int planTemplateId, ISet<DayOfWeek> weekendDays,
            bool isException = false, string? exceptionReason = null, CancellationToken cancellationToken = default)
        {
            var template = await LoadApprovedTemplateAsync(planTemplateId, cancellationToken);

            if (isException && string.IsNullOrWhiteSpace(exceptionReason))
            {
                throw new ExceptionAssignmentReasonRequiredException();
            }

            var yearId = _workingYear.AcademicYearId;
            if (await HasPlanAsync(studentId, yearId, template.FeeCategoryId, cancellationToken))
            {
                throw new PlanAssignmentExistsException(studentId);
            }

            var chargeIds = await _db.Charges
                .Where(c => c.StudentId == studentId && c.PayerId == payerId && c.AcademicYearId == yearId && c.Status == ChargeStatus.Posted)
                .Where(c => template.FeeCategoryId == null || c.FeeCategoryId == template.FeeCategoryId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
            var portions = await LoadNetChargePortionsAsync(chargeIds, cancellationToken);
            if (portions.Count == 0)
            {
                throw new NoChargesToScheduleException(studentId);
            }

            var dueDates = await DueDatesAsync(template, yearId, weekendDays, cancellationToken);
            var assignment = BuildAssignment(template, dueDates, yearId, studentId, payerId, portions, isException, exceptionReason);

            _db.PlanAssignments.Add(assignment);
            await _db.SaveChangesAsync(cancellationToken);

            await LogRevisionAsync(assignment.Id, ScheduleRevisionCause.Generated, null, "[]", cancellationToken);
            return assignment;
        }

        // ------------------------------------------------------------------ doc §8.2 grade-wide defaults

        public Task<GradeAssignmentRun> PreviewGradeAssignmentAsync(
            int gradeLevelId, int planTemplateId, CancellationToken cancellationToken = default)
            => RunGradeAsync(gradeLevelId, planTemplateId, null, cancellationToken);

        public Task<GradeAssignmentRun> AssignPlanToGradeAsync(
            int gradeLevelId, int planTemplateId, ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken = default)
            => RunGradeAsync(gradeLevelId, planTemplateId, weekendDays, cancellationToken);

        /// <summary>
        /// One evaluation behind both the preview and the run, so what the officer was shown is
        /// what happens. <paramref name="weekendDays"/> null is the preview: it writes nothing,
        /// and the due-date shift it would need is never computed.
        /// </summary>
        private async Task<GradeAssignmentRun> RunGradeAsync(
            int gradeLevelId, int planTemplateId, ISet<DayOfWeek>? weekendDays, CancellationToken cancellationToken)
        {
            var template = await LoadApprovedTemplateAsync(planTemplateId, cancellationToken);
            var mandatoryCategoryIds = await MandatoryCategoryIdsAsync(cancellationToken);
            if (template.FeeCategoryId is int scoped && !mandatoryCategoryIds.Contains(scoped))
            {
                throw new TemplateCategoryNotMandatoryException(planTemplateId);
            }

            var yearId = _workingYear.AcademicYearId;
            var profileIds = await _db.GradeYearProfiles
                .Where(p => p.AcademicYearId == yearId && p.GradeLevelId == gradeLevelId)
                .Select(p => p.Id).ToListAsync(cancellationToken);
            var enrolledIds = await _db.Enrollments
                .Where(e => e.AcademicYearId == yearId && e.Status == EnrollmentStatus.Active && profileIds.Contains(e.GradeYearProfileId))
                .Select(e => e.StudentId).Distinct().ToListAsync(cancellationToken);

            // Register order, so the same run reads the same way twice and the officer can follow it down
            // the list. Read through the soft-active filter deliberately: a deactivated student record
            // with an enrolment still marked Active is a data fault, and the safe reading of it is not to
            // raise a payment schedule against them.
            var studentIds = await _db.Students
                .Where(s => enrolledIds.Contains(s.Id)).OrderBy(s => s.StudentNo)
                .Select(s => s.Id).ToListAsync(cancellationToken);

            var dueDates = weekendDays == null ? null : await DueDatesAsync(template, yearId, weekendDays, cancellationToken);

            var lines = new List<GradeAssignmentLine>();
            foreach (var studentId in studentIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lines.Add(await EvaluateStudentAsync(studentId, template, yearId, mandatoryCategoryIds, dueDates, cancellationToken));

                // One student = one committed unit, and the tracker is dropped between them: a grade is
                // 25–40 students today but a whole-school run is the obvious next ask, and a batch that
                // keeps every saved graph re-walks all of it on the next DetectChanges. Everything below
                // re-reads what it needs; `template` and `dueDates` are detached reads by design.
                if (dueDates != null)
                {
                    _db.ChangeTracker.Clear();
                }
            }

            return new GradeAssignmentRun(gradeLevelId, planTemplateId, lines);
        }

        /// <summary>
        /// The mandatory fee categories of this school, read <b>past</b> the soft-active filter.
        /// A category that has been retired mid-year still owns the charges it already posted, and
        /// reading the list through the filter would drop those charges out of the schedule
        /// silently — a schedule that is short by a whole fee, with nothing on screen to say so.
        /// </summary>
        private async Task<List<int>> MandatoryCategoryIdsAsync(CancellationToken cancellationToken)
            => await _db.FeeCategories.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.SchoolId == _db.CurrentSchoolId && c.IsMandatory)
                .Select(c => c.Id).ToListAsync(cancellationToken);

        private async Task<GradeAssignmentLine> EvaluateStudentAsync(
            int studentId, PlanTemplate template, int yearId, List<int> mandatoryCategoryIds,
            IReadOnlyList<DateTime>? dueDates, CancellationToken cancellationToken)
        {
            var existingId = await _db.PlanAssignments
                .Where(a => a.StudentId == studentId && a.AcademicYearId == yearId && a.FeeCategoryId == template.FeeCategoryId)
                .Select(a => (int?)a.Id).FirstOrDefaultAsync(cancellationToken);
            if (existingId != null)
            {
                return new GradeAssignmentLine(studentId, null, GradeAssignmentOutcome.AlreadyPlanned, 0m, existingId);
            }

            var charges = await _db.Charges
                .Where(c => c.StudentId == studentId && c.AcademicYearId == yearId && c.Status == ChargeStatus.Posted)
                .Where(c => template.FeeCategoryId == null || c.FeeCategoryId == template.FeeCategoryId)
                .Where(c => mandatoryCategoryIds.Contains(c.FeeCategoryId))
                .Select(c => new { c.Id, c.PayerId })
                .ToListAsync(cancellationToken);

            var payerIds = charges.Select(c => c.PayerId).Distinct().ToList();
            if (payerIds.Count == 0)
            {
                return new GradeAssignmentLine(studentId, null, GradeAssignmentOutcome.NoMandatoryCharges, 0m, null);
            }

            if (payerIds.Count > 1)
            {
                return new GradeAssignmentLine(studentId, null, GradeAssignmentOutcome.PayerSplit, 0m, null);
            }

            var payerId = payerIds[0];
            var portions = await LoadNetChargePortionsAsync(charges.Select(c => c.Id).ToList(), cancellationToken);
            if (portions.Count == 0)
            {
                // Charges exist but credit notes and discounts have taken all of them off. There is
                // still nothing to split, and saying "no mandatory charges" is the truth the officer
                // needs — the fees screen is where the credit notes are.
                return new GradeAssignmentLine(studentId, null, GradeAssignmentOutcome.NoMandatoryCharges, 0m, null);
            }

            var total = portions.Sum(p => p.Amount);
            if (dueDates == null)
            {
                return new GradeAssignmentLine(studentId, payerId, GradeAssignmentOutcome.Ready, total, null);
            }

            var assignment = BuildAssignment(template, dueDates, yearId, studentId, payerId, portions, isException: false, exceptionReason: null);
            _db.PlanAssignments.Add(assignment);
            await _db.SaveChangesAsync(cancellationToken);
            await LogRevisionAsync(assignment.Id, ScheduleRevisionCause.Generated, null, "[]", cancellationToken);
            return new GradeAssignmentLine(studentId, payerId, GradeAssignmentOutcome.Assigned, total, assignment.Id);
        }

        // ------------------------------------------------------------------ derived schedule

        private sealed record LoadedInstallment(Installment Row, decimal Paid, InstallmentStatus Status, bool IsCollectible);

        private sealed record LoadedSchedule(PlanAssignment Assignment, PlanTemplate Template, IReadOnlyList<LoadedInstallment> Installments, IReadOnlyList<InstallmentChargeLine> Lines);

        private async Task<LoadedSchedule> LoadScheduleAsync(int planAssignmentId, CancellationToken cancellationToken)
        {
            var assignment = await _db.PlanAssignments.SingleAsync(a => a.Id == planAssignmentId, cancellationToken);
            var template = await _db.PlanTemplates.SingleAsync(t => t.Id == assignment.PlanTemplateId, cancellationToken);
            var rows = await _db.Installments
                .Where(i => i.PlanAssignmentId == planAssignmentId)
                .OrderBy(i => i.DueDate).ThenBy(i => i.SequenceNumber)
                .ToListAsync(cancellationToken);
            var ids = rows.Select(r => r.Id).ToList();
            var lines = await _db.InstallmentChargeLines.Where(l => ids.Contains(l.InstallmentId)).ToListAsync(cancellationToken);
            var chargeIds = lines.Select(l => l.ChargeId).Distinct().ToList();

            var allocationRows = await _db.PaymentAllocations
                .Where(a => chargeIds.Contains(a.ChargeId))
                .Select(a => new { a.ChargeId, a.AllocatedAmount })
                .ToListAsync(cancellationToken);
            var allocatedByCharge = allocationRows.GroupBy(a => a.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));
            var scheduledByCharge = lines.GroupBy(l => l.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            // Module 21 pays charges; the schedule's collected total is what has been allocated to its charges,
            // capped at what this schedule actually scheduled of each charge.
            var totalPaid = scheduledByCharge.Sum(kv => Math.Min(kv.Value, allocatedByCharge.TryGetValue(kv.Key, out var a) ? a : 0m));

            var collectible = rows.Where(r => !r.IsSuperseded && !r.IsWrittenOff).ToList();
            var paidPerCollectible = InstallmentPaymentWaterfall.Apply(collectible.Select(r => r.Amount).ToList(), totalPaid);
            var paidById = collectible.Select((r, i) => (r.Id, paidPerCollectible[i])).ToDictionary(x => x.Id, x => x.Item2);

            var today = _clock.UtcNow;
            var loaded = rows.Select(r =>
            {
                var paid = paidById.TryGetValue(r.Id, out var p) ? p : 0m;
                var status = InstallmentStatusDeriver.Derive(r.Amount, paid, r.DueDate, template.GraceDays, today, r.IsSuperseded, r.IsWrittenOff);
                return new LoadedInstallment(r, paid, status, !r.IsSuperseded && !r.IsWrittenOff);
            }).ToList();

            return new LoadedSchedule(assignment, template, loaded, lines);
        }

        public async Task<IReadOnlyList<InstallmentView>> GetScheduleAsync(int planAssignmentId, CancellationToken cancellationToken = default)
        {
            var schedule = await LoadScheduleAsync(planAssignmentId, cancellationToken);
            return schedule.Installments
                .Select(i => new InstallmentView(i.Row.Id, i.Row.SequenceNumber, i.Row.DueDate, i.Row.Amount, i.Paid, i.Status, i.Row.CoveringPdcId != null))
                .ToList();
        }

        private static string Snapshot(IEnumerable<Installment> rows) => JsonSerializer.Serialize(rows
            .OrderBy(r => r.SequenceNumber)
            .Select(r => new { r.SequenceNumber, DueDate = r.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), r.Amount, r.IsSuperseded, r.IsWrittenOff }));

        private async Task LogRevisionAsync(int planAssignmentId, ScheduleRevisionCause cause, string? reason, string beforeJson, CancellationToken cancellationToken)
        {
            var rows = await _db.Installments.Where(i => i.PlanAssignmentId == planAssignmentId).ToListAsync(cancellationToken);
            _db.ScheduleRevisions.Add(new ScheduleRevision
            {
                PlanAssignmentId = planAssignmentId, Cause = cause, Reason = reason, BeforeJson = beforeJson, AfterJson = Snapshot(rows), OccurredAtUtc = _clock.UtcNow,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Takes <paramref name="amount"/> off an installment's charge lines, last line first; returns what was freed per charge.</summary>
        private static List<InstallmentScheduleBuilder.ChargePortion> FreeFromLines(List<InstallmentChargeLine> lines, decimal amount, AppDbContext db)
        {
            var freed = new List<InstallmentScheduleBuilder.ChargePortion>();
            var remaining = amount;
            foreach (var line in lines.OrderByDescending(l => l.Id).ToList())
            {
                if (remaining <= 0m)
                {
                    break;
                }

                var take = Math.Min(line.Amount, remaining);
                line.Amount -= take;
                remaining -= take;
                freed.Add(new InstallmentScheduleBuilder.ChargePortion(line.ChargeId, take));
                if (line.Amount == 0m)
                {
                    db.InstallmentChargeLines.Remove(line);
                }
            }

            return freed;
        }

        // ------------------------------------------------------------------ BR-INS-003 controlled recomputation

        public async Task AppendChargeAsync(int planAssignmentId, int chargeId, CancellationToken cancellationToken = default)
        {
            var schedule = await LoadScheduleAsync(planAssignmentId, cancellationToken);
            var charge = await _db.Charges.SingleAsync(c => c.Id == chargeId, cancellationToken);
            if (charge.Status != ChargeStatus.Posted || charge.StudentId != schedule.Assignment.StudentId)
            {
                throw new ChargeNotPostedException(chargeId);
            }

            if (schedule.Lines.Any(l => l.ChargeId == chargeId))
            {
                return; // already scheduled — idempotent
            }

            var portion = (await LoadNetChargePortionsAsync(new[] { chargeId }, cancellationToken)).SingleOrDefault();
            if (portion == null)
            {
                return;
            }

            var before = Snapshot(schedule.Installments.Select(i => i.Row));
            var open = schedule.Installments.Where(i => i.IsCollectible && i.Paid < i.Row.Amount).Select(i => i.Row).ToList();
            if (open.Count == 0)
            {
                var lastSeq = schedule.Installments.Select(i => i.Row.SequenceNumber).DefaultIfEmpty(0).Max();
                var due = schedule.Installments.Select(i => i.Row.DueDate).DefaultIfEmpty(_clock.UtcNow.Date).Max();
                var single = new Installment { PlanAssignmentId = planAssignmentId, SequenceNumber = lastSeq + 1, DueDate = due < _clock.UtcNow.Date ? _clock.UtcNow.Date : due, Amount = portion.Amount };
                single.ChargeLines.Add(new InstallmentChargeLine { ChargeId = chargeId, Amount = portion.Amount });
                _db.Installments.Add(single);
            }
            else
            {
                var spread = InstallmentScheduleBuilder.SpreadEvenly(portion.Amount, open.Count);
                for (var i = 0; i < open.Count; i++)
                {
                    open[i].Amount += spread[i];
                    _db.InstallmentChargeLines.Add(new InstallmentChargeLine { InstallmentId = open[i].Id, ChargeId = chargeId, Amount = spread[i] });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await LogRevisionAsync(planAssignmentId, ScheduleRevisionCause.ChargeAppended, $"charge {charge.ChargeNo}", before, cancellationToken);
        }

        public async Task ReduceScheduleAsync(int planAssignmentId, decimal reduction, string reason, CancellationToken cancellationToken = default)
        {
            var schedule = await LoadScheduleAsync(planAssignmentId, cancellationToken);
            var before = Snapshot(schedule.Installments.Select(i => i.Row));
            var open = schedule.Installments
                .Where(i => i.IsCollectible)
                .Select(i => new ScheduleReductionAllocator.OpenInstallment(i.Row.Id, i.Row.DueDate, i.Row.Amount, i.Paid))
                .ToList();

            var changes = ScheduleReductionAllocator.Reduce(open, reduction, _clock.UtcNow);
            _audit.Reason = reason;
            foreach (var (installmentId, newAmount) in changes)
            {
                var row = schedule.Installments.Single(i => i.Row.Id == installmentId).Row;
                var freed = row.Amount - newAmount;
                row.Amount = newAmount;
                FreeFromLines(schedule.Lines.Where(l => l.InstallmentId == installmentId).ToList(), freed, _db);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await LogRevisionAsync(planAssignmentId, ScheduleRevisionCause.Reduced, reason, before, cancellationToken);
        }

        // ------------------------------------------------------------------ BR-INS-005 rescheduling

        private sealed record ProposedLine(string DueDate, decimal Amount);

        public async Task<RescheduleCase> ProposeRescheduleAsync(
            int planAssignmentId, int proposedByUserId, string reason, IReadOnlyList<ProposedInstallment> proposal,
            ISet<DayOfWeek> weekendDays, int maxExtensionMonths = 3, CancellationToken cancellationToken = default)
        {
            var schedule = await LoadScheduleAsync(planAssignmentId, cancellationToken);
            var unpaid = schedule.Installments.Where(i => i.IsCollectible && i.Paid < i.Row.Amount).ToList();
            var remainder = unpaid.Sum(i => i.Row.Amount - i.Paid);
            var proposed = proposal.Sum(p => p.Amount);
            if (proposal.Count == 0 || proposed != remainder)
            {
                throw new RescheduleRemainderMismatchException(remainder, proposed);
            }

            var isWorkingDay = await BuildWorkingDayPredicateAsync(schedule.Assignment.AcademicYearId, weekendDays, cancellationToken);
            var shifted = proposal.Select(p => new ProposedInstallment(DueDateShifter.ShiftToWorkingDay(p.DueDate, isWorkingDay), p.Amount)).ToList();

            var year = await _db.AcademicYears.SingleAsync(y => y.Id == schedule.Assignment.AcademicYearId, cancellationToken);
            var originalLast = unpaid.Max(i => i.Row.DueDate);
            var proposedLast = shifted.Max(p => p.DueDate);

            var rescheduleCase = new RescheduleCase
            {
                PlanAssignmentId = planAssignmentId, ProposedByUserId = proposedByUserId, Reason = reason,
                ProposedScheduleJson = JsonSerializer.Serialize(shifted.Select(p => new ProposedLine(p.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p.Amount))),
                RemainderAmount = remainder,
                RequiresPrincipal = RescheduleApprovalRouter.RequiresPrincipal(originalLast, proposedLast, year.EndDate, maxExtensionMonths),
                ProposedAtUtc = _clock.UtcNow,
            };
            _db.RescheduleCases.Add(rescheduleCase);
            await _db.SaveChangesAsync(cancellationToken);
            return rescheduleCase;
        }

        public async Task DecideRescheduleAsync(int rescheduleCaseId, bool approve, string? decisionReason = null, CancellationToken cancellationToken = default)
        {
            var rescheduleCase = await _db.RescheduleCases.SingleAsync(c => c.Id == rescheduleCaseId, cancellationToken);
            if (rescheduleCase.Status != RescheduleCaseStatus.Proposed)
            {
                throw new RescheduleCaseNotPendingException(rescheduleCaseId);
            }

            rescheduleCase.DecidedAtUtc = _clock.UtcNow;
            rescheduleCase.DecisionReason = decisionReason;
            if (!approve)
            {
                rescheduleCase.Status = RescheduleCaseStatus.Rejected;
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            var schedule = await LoadScheduleAsync(rescheduleCase.PlanAssignmentId, cancellationToken);
            var unpaid = schedule.Installments.Where(i => i.IsCollectible && i.Paid < i.Row.Amount).ToList();
            var remainder = unpaid.Sum(i => i.Row.Amount - i.Paid);
            if (remainder != rescheduleCase.RemainderAmount)
            {
                // Money moved between proposal and decision — the proposal no longer covers the remainder exactly.
                throw new RescheduleRemainderMismatchException(remainder, rescheduleCase.RemainderAmount);
            }

            var before = Snapshot(schedule.Installments.Select(i => i.Row));
            var freed = new List<InstallmentScheduleBuilder.ChargePortion>();
            foreach (var item in unpaid)
            {
                var lines = schedule.Lines.Where(l => l.InstallmentId == item.Row.Id).ToList();
                if (item.Paid == 0m)
                {
                    // Wholly unpaid: superseded, kept in history; its charge lines move to the new schedule.
                    item.Row.IsSuperseded = true;
                    freed.AddRange(FreeFromLines(lines, item.Row.Amount, _db));
                }
                else
                {
                    // Partially paid: the paid part stays (derives to Paid), only the unpaid remainder moves.
                    var moving = item.Row.Amount - item.Paid;
                    item.Row.Amount = item.Paid;
                    freed.AddRange(FreeFromLines(lines, moving, _db));
                }
            }

            var proposal = JsonSerializer.Deserialize<List<ProposedLine>>(rescheduleCase.ProposedScheduleJson)!;
            var mapped = InstallmentScheduleBuilder.MapChargesToInstallments(freed, proposal.Select(p => p.Amount).ToList());
            var nextSeq = schedule.Installments.Max(i => i.Row.SequenceNumber) + 1;
            for (var i = 0; i < proposal.Count; i++)
            {
                var installment = new Installment
                {
                    PlanAssignmentId = rescheduleCase.PlanAssignmentId, SequenceNumber = nextSeq + i,
                    DueDate = DateTime.ParseExact(proposal[i].DueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture), Amount = proposal[i].Amount,
                };
                foreach (var line in mapped.Where(m => m.InstallmentIndex == i))
                {
                    installment.ChargeLines.Add(new InstallmentChargeLine { ChargeId = line.ChargeId, Amount = line.Amount });
                }

                _db.Installments.Add(installment);
            }

            schedule.Assignment.RescheduleCount++;
            rescheduleCase.Status = RescheduleCaseStatus.Approved;
            await _db.SaveChangesAsync(cancellationToken);
            await LogRevisionAsync(rescheduleCase.PlanAssignmentId, ScheduleRevisionCause.Rescheduled, rescheduleCase.Reason, before, cancellationToken);
        }

        // ------------------------------------------------------------------ BR-INS-006 promises

        public async Task<PromiseToPay> RecordPromiseAsync(int installmentId, int recordedByUserId, DateTime promisedDate, decimal amount, int horizonDays = 30, CancellationToken cancellationToken = default)
        {
            var row = await _db.Installments.SingleAsync(i => i.Id == installmentId, cancellationToken);
            var schedule = await LoadScheduleAsync(row.PlanAssignmentId, cancellationToken);
            var item = schedule.Installments.Single(i => i.Row.Id == installmentId);
            if (!InstallmentStatusDeriver.IsTrulyOverdue(item.Row.Amount, item.Paid, item.Row.DueDate, schedule.Template.GraceDays, _clock.UtcNow, item.Row.IsSuperseded, item.Row.IsWrittenOff))
            {
                throw new InstallmentNotOverdueException(installmentId);
            }

            var today = _clock.UtcNow.Date;
            if (promisedDate.Date < today || promisedDate.Date > today.AddDays(horizonDays))
            {
                throw new PromiseDateOutOfRangeException(promisedDate);
            }

            var promise = new PromiseToPay { InstallmentId = installmentId, RecordedByUserId = recordedByUserId, PromisedDate = promisedDate.Date, Amount = amount };
            _db.PromisesToPay.Add(promise);
            await _db.SaveChangesAsync(cancellationToken);
            return promise;
        }

        public async Task<int> EvaluatePromisesAsync(CancellationToken cancellationToken = default)
        {
            var today = _clock.UtcNow.Date;
            var due = await _db.PromisesToPay.Where(p => p.Status == PromiseStatus.Open && p.PromisedDate < today).ToListAsync(cancellationToken);
            if (due.Count == 0)
            {
                return 0;
            }

            var installmentIds = due.Select(p => p.InstallmentId).Distinct().ToList();
            var assignmentByInstallment = await _db.Installments.Where(i => installmentIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.PlanAssignmentId, cancellationToken);
            var schedules = new Dictionary<int, LoadedSchedule>();
            var broken = 0;
            foreach (var promise in due)
            {
                var assignmentId = assignmentByInstallment[promise.InstallmentId];
                if (!schedules.TryGetValue(assignmentId, out var schedule))
                {
                    schedule = await LoadScheduleAsync(assignmentId, cancellationToken);
                    schedules[assignmentId] = schedule;
                }

                var item = schedule.Installments.Single(i => i.Row.Id == promise.InstallmentId);
                promise.Status = item.Status == InstallmentStatus.Paid ? PromiseStatus.Kept : PromiseStatus.Broken;
                promise.ResolvedAtUtc = _clock.UtcNow;
                if (promise.Status == PromiseStatus.Broken)
                {
                    broken++;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return broken;
        }

        // ------------------------------------------------------------------ BR-INS-009 PDC coverage, WF-06 write-off

        public async Task MarkPdcCoveredAsync(int installmentId, int pdcId, CancellationToken cancellationToken = default)
        {
            var row = await _db.Installments.SingleAsync(i => i.Id == installmentId, cancellationToken);
            var assignment = await _db.PlanAssignments.SingleAsync(a => a.Id == row.PlanAssignmentId, cancellationToken);
            var pdc = await _db.Pdcs.SingleAsync(p => p.Id == pdcId, cancellationToken);
            var live = pdc.Status is PdcStatus.Lodged or PdcStatus.Due or PdcStatus.Deposited;
            if (pdc.PayerId != assignment.PayerId || !live)
            {
                throw new PdcNotCoverableException(pdcId);
            }

            row.CoveringPdcId = pdcId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task WriteOffAsync(int installmentId, string reason, CancellationToken cancellationToken = default)
        {
            var row = await _db.Installments.SingleAsync(i => i.Id == installmentId, cancellationToken);
            if (row.IsSuperseded || row.IsWrittenOff)
            {
                throw new InstallmentNotOpenException(installmentId);
            }

            // What is actually being given up: the scheduled amount less whatever has already been
            // collected against it. Read before the flag flips, because flipping it removes this
            // installment from the collectible set the waterfall pays down.
            var schedule = await LoadScheduleAsync(row.PlanAssignmentId, cancellationToken);
            var unpaid = schedule.Installments
                .Where(i => i.Row.Id == installmentId)
                .Select(i => i.Row.Amount - i.Paid)
                .Single();

            _audit.Reason = reason;
            row.IsWrittenOff = true;
            row.WriteOffReason = reason;

            // Gap G-6: the flag alone left the receivable on the balance sheet for ever. The charges
            // this installment scheduled are the receivable, so they are what has to be relieved —
            // as write-off credit notes, which every remainder calculation in the system already
            // subtracts, and which the ledger books against bad debt rather than against revenue.
            await RelieveWrittenOffChargesAsync(installmentId, unpaid, reason, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Spreads <paramref name="unpaid"/> across the charges this installment scheduled, in charge
        /// order, never taking more from a charge than is still open on it. A schedule can span
        /// several charges and a charge can be paid from outside the schedule, so the line amount is
        /// a ceiling rather than the answer.
        /// </summary>
        private async Task RelieveWrittenOffChargesAsync(int installmentId, decimal unpaid, string reason, CancellationToken cancellationToken)
        {
            if (unpaid <= 0m)
            {
                return;
            }

            var lines = await _db.InstallmentChargeLines
                .Where(l => l.InstallmentId == installmentId)
                .OrderBy(l => l.ChargeId)
                .Select(l => new { l.ChargeId, l.Amount })
                .ToListAsync(cancellationToken);
            if (lines.Count == 0)
            {
                return;
            }

            var chargeIds = lines.Select(l => l.ChargeId).Distinct().ToList();
            var charges = await _db.Charges.Where(c => chargeIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

            // EF Core's Sqlite provider can't translate Sum() over decimal - materialize then sum.
            var credited = (await _db.CreditNotes.Where(n => chargeIds.Contains(n.ChargeId)).Select(n => new { n.ChargeId, n.Amount }).ToListAsync(cancellationToken))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var discounted = (await _db.DiscountDocuments.Where(d => chargeIds.Contains(d.ChargeId)).Select(d => new { d.ChargeId, d.Amount }).ToListAsync(cancellationToken))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var allocated = (await _db.PaymentAllocations.Where(a => chargeIds.Contains(a.ChargeId)).Select(a => new { a.ChargeId, a.AllocatedAmount }).ToListAsync(cancellationToken))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));

            decimal Open(int chargeId) => charges[chargeId].GrossAmount
                - (credited.TryGetValue(chargeId, out var cr) ? cr : 0m)
                - (discounted.TryGetValue(chargeId, out var ds) ? ds : 0m)
                - (allocated.TryGetValue(chargeId, out var al) ? al : 0m);

            var left = unpaid;
            foreach (var line in lines)
            {
                if (left <= 0m)
                {
                    break;
                }

                var take = Math.Min(Math.Min(line.Amount, Open(line.ChargeId)), left);
                if (take <= 0m)
                {
                    continue;
                }

                await _fees.IssueWriteOffCreditNoteAsync(line.ChargeId, take, reason, cancellationToken);
                left -= take;
            }
        }

        // ------------------------------------------------------------------ BR-INS-008 dunning

        public async Task<IReadOnlyList<DunningEvent>> RunDunningAsync(CancellationToken cancellationToken = default)
        {
            await EvaluatePromisesAsync(cancellationToken);

            var fired = new List<DunningEvent>();
            var assignmentIds = await _db.PlanAssignments.Select(a => a.Id).ToListAsync(cancellationToken);
            foreach (var assignmentId in assignmentIds)
            {
                var schedule = await LoadScheduleAsync(assignmentId, cancellationToken);
                var installmentIds = schedule.Installments.Select(i => i.Row.Id).ToList();
                var events = await _db.DunningEvents.Where(e => installmentIds.Contains(e.InstallmentId)).ToListAsync(cancellationToken);
                var brokenPromises = await _db.PromisesToPay
                    .Where(p => installmentIds.Contains(p.InstallmentId) && p.Status == PromiseStatus.Broken)
                    .ToListAsync(cancellationToken);

                foreach (var item in schedule.Installments.Where(i => i.IsCollectible))
                {
                    var stepsFired = events.Where(e => e.InstallmentId == item.Row.Id).ToList();
                    var lastFiredAt = stepsFired.Select(e => e.FiredAtUtc).DefaultIfEmpty(DateTime.MinValue).Max();
                    var hasBrokenPromise = brokenPromises.Any(p => p.InstallmentId == item.Row.Id && p.ResolvedAtUtc > lastFiredAt);
                    var trulyOverdue = InstallmentStatusDeriver.IsTrulyOverdue(item.Row.Amount, item.Paid, item.Row.DueDate, schedule.Template.GraceDays, _clock.UtcNow, item.Row.IsSuperseded, item.Row.IsWrittenOff);

                    var step = DunningLadderEvaluator.Next(
                        item.Row.DueDate, _clock.UtcNow, item.Status, trulyOverdue, item.Row.CoveringPdcId != null,
                        stepsFired.Select(e => e.Step).ToList(), hasBrokenPromise);
                    if (step == null)
                    {
                        continue;
                    }

                    var dunningEvent = new DunningEvent { InstallmentId = item.Row.Id, Step = step.Value, FiredAtUtc = _clock.UtcNow, TriggeredByBrokenPromise = hasBrokenPromise };
                    _db.DunningEvents.Add(dunningEvent);
                    fired.Add(dunningEvent);

                    var recipients = await ResolvePayerRecipientsAsync(schedule.Assignment.PayerId, cancellationToken);
                    var payload = new Dictionary<string, string>
                    {
                        ["InstallmentNo"] = item.Row.SequenceNumber.ToString(CultureInfo.InvariantCulture),
                        ["DueDate"] = item.Row.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["Amount"] = (item.Row.Amount - item.Paid).ToString("0.00", CultureInfo.InvariantCulture),
                        ["Step"] = step.Value.ToString(),
                    };
                    var eventCode = step.Value < DunningStep.Overdue3 ? DueSoonEventCode : OverdueEventCode;
                    await _notifications.PublishAsync(eventCode, recipients, payload, cancellationToken);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return fired;
        }

        private async Task<IReadOnlyCollection<NotificationRecipient>> ResolvePayerRecipientsAsync(int payerId, CancellationToken cancellationToken)
        {
            var payer = await _db.Payers.SingleOrDefaultAsync(p => p.Id == payerId, cancellationToken);
            if (payer?.ParentId == null)
            {
                return Array.Empty<NotificationRecipient>();
            }

            var parent = await _db.Parents.SingleOrDefaultAsync(p => p.Id == payer.ParentId, cancellationToken);
            return parent?.UserAccountId == null
                ? Array.Empty<NotificationRecipient>()
                : new[] { new NotificationRecipient(parent.UserAccountId.Value, parent.PreferredLanguage) };
        }
    }
}
