using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Installments;
using Sms.Application.ReadModels;
using Sms.Application.Security;
using Sms.Domain.Discounts;
using Sms.Domain.Fees;
using Sms.Domain.Grades;
using Sms.Domain.Installments;
using Sms.Domain.Parents;
using Sms.Domain.Sections;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Web.Finance;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/19 §8.7 read from the student's side — the half of that screen the payer view
    /// cannot answer.
    /// <para>
    /// §8.7 was built payer-first: <c>/fees/position</c> starts from a guardian and shows the family
    /// balance. That is the right shape for a statement and the wrong one for a counter, where the
    /// name offered is a child's. A clerk asked "how much is left on Sara's fees" had to guess which
    /// parent to search, and a school with no roll-wide view had no way to answer "who still owes"
    /// at all. This adds the roll (P-LIST, filtered by grade, section, name and guardian) and two
    /// drill-downs from it: the child's statement (P-STMT, printable) and the breakdown of what the
    /// fee is actually made of (P-DETAIL).
    /// </para>
    /// <para>
    /// Paying is not one of them. A receipt belongs to a payer, not a student (BR-FEE-004), and
    /// BR-PAY-003 allocates it oldest-first across everything that payer owes — so a per-student
    /// payment screen would either lie about where the money went or contradict the engine. The
    /// Pay button therefore resolves the guardian the school holds responsible and hands the
    /// cashier over to Module 21's own screen, already pointed at them.
    /// </para>
    /// <para>
    /// <b>Deviation from doc/UI/02 §"print":</b> the statement is an *official document* in that
    /// catalogue (Off class — server-rendered PDF, template slots, QR block), and the PDF engine is
    /// still an open owner decision (Phase 9 T-5, docs/Status). What ships here is the operational
    /// print: a browser print stylesheet over a bilingual layout, the same substitution
    /// <c>Position</c> and <c>Receipt</c> already make. It is not a sealed document and carries no
    /// statement number — <c>IStatementService.IssueAsync</c> mints those, and nothing calls it from
    /// a screen yet.
    /// </para>
    /// </summary>
    public partial class FeesController
    {
        /// <summary>
        /// Rows rendered per page. The roll is the whole school, and a grid of two thousand children
        /// helps nobody: the filters are the instrument, and the count above the grid says what the
        /// filters actually matched so a truncated view never reads as a complete one.
        /// </summary>
        private const int StudentFinancePageSize = 200;

        // ================================================================== 8.7 (student) — the roll

        [HttpGet("students")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.StudentFinance, ActionVerb.View)]
        public async Task<IActionResult> StudentFinance(
            int? year = null, string? q = null, int? grade = null, int? section = null, string? guardian = null, bool due = false)
        {
            var m = new StudentFinanceListViewModel { Q = q, GradeId = grade, SectionId = section, Guardian = guardian, DueOnly = due };
            await FillPageAsync(m, year);
            await FillStudentFinanceRightsAsync(m);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;

            // The picker offers the grades that exist; the labels below are read through
            // IgnoreQueryFilters because a retired grade still has last year's charges hanging off it.
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            m.Grades = grades.Where(g => g.IsActive).OrderBy(g => g.SequenceOrder).ToList();

            var profiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().Where(p => p.AcademicYearId == yid).ToListAsync();
            if (grade != null)
            {
                // The profile ids are narrowed here rather than inside the query on purpose. EF Core 5
                // will not translate a *filtered* projection of a local list when the filter closes
                // over a query parameter — `profiles.Where(p => p.GradeLevelId == grade).Select(...)`
                // compiles, and threw "could not be translated" the first time anyone picked a grade
                // on this screen. Materialising the ids first leaves a plain IN (…) it can translate.
                var gradeProfileIds = profiles.Where(p => p.GradeLevelId == grade).Select(p => p.Id).ToList();
                m.Sections = await _db.Sections.AsNoTracking()
                    .Where(s => s.AcademicYearId == yid && gradeProfileIds.Contains(s.GradeYearProfileId))
                    .OrderBy(s => s.NameEn).ToListAsync();
            }

            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => e.AcademicYearId == yid && e.Status == EnrollmentStatus.Active && e.ExitDate == null).ToListAsync();
            if (grade != null)
            {
                var inGrade = profiles.Where(p => p.GradeLevelId == grade).Select(p => p.Id).ToHashSet();
                enrollments = enrollments.Where(e => inGrade.Contains(e.GradeYearProfileId)).ToList();
            }

            var memberships = await _db.SectionMemberships.AsNoTracking()
                .Where(x => x.AcademicYearId == yid && x.EffectiveToUtc == null).ToListAsync();
            if (section != null)
            {
                var inSection = memberships.Where(x => x.SectionId == section).Select(x => x.EnrollmentId).ToHashSet();
                enrollments = enrollments.Where(e => inSection.Contains(e.Id)).ToList();
            }

            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _db.CurrentSchoolId && studentIds.Contains(s.Id)).ToListAsync();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                students = students.Where(s =>
                    s.StudentNo.Contains(t, StringComparison.OrdinalIgnoreCase)
                    || Contains(s.FirstNameAr, t) || Contains(s.FatherNameAr, t) || Contains(s.FamilyNameAr, t)
                    || Contains(s.FirstNameEn, t) || Contains(s.FatherNameEn, t) || Contains(s.FamilyNameEn, t)).ToList();
            }

            // Guardians in one pass rather than per row: the same query answers the guardian column,
            // the guardian filter and the Pay button's target, and a per-student lookup over a full
            // grade is the loop this codebase has already paid for twice.
            var links = await _db.StudentGuardianLinks.AsNoTracking()
                .Where(l => studentIds.Contains(l.StudentId) && l.EffectiveToUtc == null).ToListAsync();
            var parents = await _db.Parents.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.SchoolId == _db.CurrentSchoolId && links.Select(l => l.ParentId).Contains(p.Id)).ToListAsync();
            var payers = await _db.Payers.AsNoTracking().Where(p => p.ParentId != null).ToListAsync();

            if (!string.IsNullOrWhiteSpace(guardian))
            {
                var g = guardian.Trim();
                var matched = parents.Where(p => Contains(p.NameAr, g) || Contains(p.NameEn, g)
                    || p.ParentFileNo.Contains(g, StringComparison.OrdinalIgnoreCase)
                    || p.PrimaryMobile.Contains(g, StringComparison.OrdinalIgnoreCase)).Select(p => p.Id).ToHashSet();
                var theirChildren = links.Where(l => matched.Contains(l.ParentId)).Select(l => l.StudentId).ToHashSet();
                students = students.Where(s => theirChildren.Contains(s.Id)).ToList();
            }

            var ordered = students
                .OrderBy(s => GradeOrderOf(s.Id, enrollments, profiles, grades))
                .ThenBy(s => s.StudentNo, StringComparer.OrdinalIgnoreCase).ToList();

            // The money for every listed student in four queries, not four per student. Selecting the
            // decimal out before summing is deliberate: Sum() over a decimal column throws on Sqlite,
            // which the test suite runs on and the browser never would.
            var page = ordered.Take(StudentFinancePageSize).ToList();
            var pageIds = page.Select(s => s.Id).ToList();
            var charges = await _db.Charges.AsNoTracking()
                .Where(c => pageIds.Contains(c.StudentId) && c.Status == ChargeStatus.Posted)
                .Select(c => new { c.Id, c.StudentId, c.GrossAmount }).ToListAsync();
            var chargeIds = charges.Select(c => c.Id).ToList();
            var studentOfCharge = charges.ToDictionary(c => c.Id, c => c.StudentId);
            var credits = await _db.CreditNotes.AsNoTracking().Where(n => chargeIds.Contains(n.ChargeId)).Select(n => new { n.ChargeId, n.Amount }).ToListAsync();
            var discountDocs = await _db.DiscountDocuments.AsNoTracking().Where(d => chargeIds.Contains(d.ChargeId)).Select(d => new { d.ChargeId, d.Amount }).ToListAsync();
            var allocations = await _db.PaymentAllocations.AsNoTracking().Where(a => chargeIds.Contains(a.ChargeId)).Select(a => new { a.ChargeId, a.AllocatedAmount }).ToListAsync();

            // The page's schedules in three more queries, not three per student. A plan never
            // reduces what a family owes, but it decides how much of it may be asked for today —
            // and a worklist that cannot tell the two apart sends the officer after money the
            // school itself agreed to wait for.
            var assignments = await _db.PlanAssignments.AsNoTracking()
                .Where(a => a.AcademicYearId == yid && pageIds.Contains(a.StudentId))
                .Select(a => new { a.Id, a.StudentId }).ToListAsync();
            var studentOfAssignment = assignments.ToDictionary(a => a.Id, a => a.StudentId);
            var assignmentIds = assignments.Select(a => a.Id).ToList();
            var installments = await _db.Installments.AsNoTracking()
                .Where(i => assignmentIds.Contains(i.PlanAssignmentId) && !i.IsSuperseded)
                .Select(i => new { i.Id, i.PlanAssignmentId, i.SequenceNumber, i.DueDate, i.Amount, i.IsWrittenOff }).ToListAsync();
            var installmentIds = installments.Select(i => i.Id).ToList();
            var scheduleLines = await _db.InstallmentChargeLines.AsNoTracking()
                .Where(l => installmentIds.Contains(l.InstallmentId))
                .Select(l => new { l.InstallmentId, l.ChargeId, l.Amount }).ToListAsync();

            var allocatedByCharge = allocations.GroupBy(a => a.ChargeId).ToDictionary(g => g.Key, g => g.Sum(a => a.AllocatedAmount));
            var sequenceOf = installments.ToDictionary(i => i.Id, i => i.SequenceNumber);
            var coveredById = InstallmentCoverageCalculator.Cover(
                scheduleLines.Select(l => new InstallmentCoverageCalculator.ScheduleLine(
                    l.InstallmentId, l.ChargeId, l.Amount, sequenceOf.GetValueOrDefault(l.InstallmentId, int.MaxValue))),
                allocatedByCharge);
            var scheduleOfStudent = installments
                .Where(i => studentOfAssignment.ContainsKey(i.PlanAssignmentId))
                .GroupBy(i => studentOfAssignment[i.PlanAssignmentId])
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<ScheduledPositionSplitter.ScheduledAmount>)g.Select(i =>
                        new ScheduledPositionSplitter.ScheduledAmount(
                            i.DueDate, i.Amount, coveredById.TryGetValue(i.Id, out var covered) ? covered : 0m, false, i.IsWrittenOff)).ToList());
            var today = _clock.UtcNow;

            decimal PerStudent<T>(IEnumerable<T> source, Func<T, int> chargeId, Func<T, decimal> amount, int studentId)
                => source.Where(x => studentOfCharge.TryGetValue(chargeId(x), out var s) && s == studentId).Sum(amount);

            var sections = await _db.Sections.AsNoTracking().Where(s => s.AcademicYearId == yid).ToListAsync();

            var rows = new List<StudentFinanceListViewModel.Row>(page.Count);
            foreach (var student in page)
            {
                var enrollment = enrollments.FirstOrDefault(e => e.StudentId == student.Id);
                var profile = enrollment == null ? null : profiles.FirstOrDefault(p => p.Id == enrollment.GradeYearProfileId);
                var gradeLevel = profile == null ? null : grades.FirstOrDefault(g => g.Id == profile.GradeLevelId);
                var membership = enrollment == null ? null : memberships.FirstOrDefault(x => x.EnrollmentId == enrollment.Id);
                var sec = membership == null ? null : sections.FirstOrDefault(s => s.Id == membership.SectionId);

                // BR-PAR-005: the guardian shown is the one the school bills, falling back to the
                // primary contact only when nobody has been made responsible — showing whichever
                // parent happened to be linked first would address the collection to the wrong one.
                var mine = links.Where(l => l.StudentId == student.Id).ToList();
                var chosen = mine.FirstOrDefault(l => l.IsFinanciallyResponsible) ?? mine.FirstOrDefault(l => l.IsPrimaryContact) ?? mine.FirstOrDefault();
                var parent = chosen == null ? null : parents.FirstOrDefault(p => p.Id == chosen.ParentId);
                var responsible = mine.FirstOrDefault(l => l.IsFinanciallyResponsible);
                var payerId = responsible == null ? null : payers.FirstOrDefault(p => p.ParentId == responsible.ParentId)?.Id;

                var gross = charges.Where(c => c.StudentId == student.Id).Sum(c => c.GrossAmount);
                var discounted = PerStudent(discountDocs, x => x.ChargeId, x => x.Amount, student.Id);
                var credited = PerStudent(credits, x => x.ChargeId, x => x.Amount, student.Id);
                var paid = PerStudent(allocations, x => x.ChargeId, x => x.AllocatedAmount, student.Id);

                var schedule = scheduleOfStudent.TryGetValue(student.Id, out var theirs)
                    ? theirs
                    : Array.Empty<ScheduledPositionSplitter.ScheduledAmount>();

                rows.Add(new StudentFinanceListViewModel.Row(
                    student,
                    gradeLevel == null ? null : (IsArabic ? gradeLevel.Name.NameAr : gradeLevel.Name.NameEn),
                    sec == null ? null : (IsArabic ? sec.NameAr : sec.NameEn),
                    parent,
                    chosen?.IsFinanciallyResponsible ?? false,
                    payerId,
                    gross, discounted, credited, paid,
                    discounted > 0m,
                    ScheduledPositionSplitter.Split(gross - discounted - credited - paid, schedule, today)));
            }

            if (due) rows = rows.Where(r => r.Remaining > 0m).ToList();

            m.Rows = rows;
            m.MatchCount = due ? rows.Count : ordered.Count;
            m.IsTruncated = !due && ordered.Count > page.Count;
            return View(m);
        }

        // ================================================================== 8.7 (student) — the breakdown

        [HttpGet("students/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.StudentFinance, ActionVerb.View)]
        public async Task<IActionResult> StudentFinanceDetail(
            [FromServices] Sms.Application.Installments.IInstallmentAdmin installments, int id, int? year = null)
        {
            var student = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == id && s.SchoolId == _db.CurrentSchoolId);
            if (student == null) return NotFound();

            var m = new StudentFinanceDetailViewModel { Student = student };
            await FillPageAsync(m, year);
            await FillStudentFinanceRightsAsync(m);
            if (m.Year == null) return View(m);
            var yid = m.Year.Id;

            var enrollment = await _db.Enrollments.AsNoTracking()
                .FirstOrDefaultAsync(e => e.StudentId == id && e.AcademicYearId == yid && e.ExitDate == null);
            m.NotEnrolled = enrollment == null;

            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var profile = enrollment == null ? null : await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(p => p.Id == enrollment.GradeYearProfileId);
            var gradeLevel = profile == null ? null : grades.FirstOrDefault(g => g.Id == profile.GradeLevelId);
            m.GradeName = gradeLevel == null ? null : (IsArabic ? gradeLevel.Name.NameAr : gradeLevel.Name.NameEn);

            if (enrollment != null)
            {
                var membership = await _db.SectionMemberships.AsNoTracking().FirstOrDefaultAsync(x => x.EnrollmentId == enrollment.Id && x.EffectiveToUtc == null);
                if (membership != null)
                {
                    var sec = await _db.Sections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == membership.SectionId);
                    m.SectionName = sec == null ? null : (IsArabic ? sec.NameAr : sec.NameEn);
                }
            }

            m.Guardians = await GuardiansOfAsync(id);

            // What the grade was priced at (BR-FEE-002's approved version) against what this child was
            // actually billed. Categories come through IgnoreQueryFilters: retiring a category must
            // not blank the name off a charge that already exists under it.
            var categories = await _db.FeeCategories.IgnoreQueryFilters().AsNoTracking().ToListAsync();
            var chargeEntities = await _db.Charges.AsNoTracking()
                .Where(c => c.StudentId == id && c.AcademicYearId == yid && c.Status == ChargeStatus.Posted)
                .OrderBy(c => c.PostedAtUtc).ToListAsync();
            m.Charges = await FinanceQueries.RowsAsync(_db, chargeEntities, openOnly: false);

            if (profile != null)
            {
                var lines = await _db.FeeStructureLines.AsNoTracking()
                    .Where(l => l.GradeYearProfileId == profile.Id && l.Status == FeeStructureLineStatus.Approved).ToListAsync();
                m.HasNoStructure = lines.Count == 0;
                m.Structure = lines.Select(l => new StudentFinanceDetailViewModel.StructureRow(
                        l,
                        categories.FirstOrDefault(c => c.Id == l.FeeCategoryId) ?? new FeeCategory { NameAr = "؟", NameEn = "?" },
                        chargeEntities.Where(c => c.FeeCategoryId == l.FeeCategoryId).Sum(c => c.GrossAmount)))
                    .OrderBy(r => IsArabic ? r.Category.NameAr : r.Category.NameEn).ToList();
            }

            m.Discounts = await DiscountsOfAsync(id, yid, chargeEntities.Select(c => c.Id).ToList());
            m.Plans = await PlansOfAsync(id, yid, categories);

            // BR-FEE-008 says what is owed and a schedule never changes it — but a family given a
            // nine-month plan does not owe the year's fee today, and this screen used to print the
            // whole balance under "المستحق" on the same page as the schedule contradicting it.
            // BR-INS-007's dates cut the one figure; they do not produce a second one.
            m.Position = ScheduledPositionSplitter.Split(
                m.Remaining,
                m.Plans.SelectMany(p => p.Installments)
                    .Select(i => new ScheduledPositionSplitter.ScheduledAmount(
                        i.Installment.DueDate, i.Installment.Amount, i.Covered, i.Installment.IsSuperseded, i.Installment.IsWrittenOff))
                    .ToList(),
                _clock.UtcNow);

            // Last, deliberately: the basket is built from what the panels above already read —
            // the unbilled half of the price list, the plans, the grants — so the checklist and
            // the breakdown beside it are one query's answer rather than two.
            m.FeeFile = await BuildFeeFilePanelAsync(m, id);

            // Its own right and its own panel: a Finance Manager who never posts charges gets no
            // basket above and must still be able to move a family onto a different plan.
            m.PlanChanges = await BuildPlanChangePanelsAsync(installments, m);

            // Likewise the grant desk (doc/Modules/22 §8.3): the discounts panel below was a
            // register, and the only place to act on a grant was the school-wide roll at /discounts.
            m.DiscountDesk = await BuildDiscountDeskAsync(m);
            return View(m);
        }

        // ================================================================== 8.7 (student) — the statement

        [HttpGet("students/{id:int}/statement")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.StudentFinance, ActionVerb.Print)]
        public async Task<IActionResult> StudentStatement(int id, DateTime? asOf = null)
        {
            var student = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == id && s.SchoolId == _db.CurrentSchoolId);
            if (student == null) return NotFound();

            var school = await _db.Schools.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.Id == _db.CurrentSchoolId);
            var asOfUtc = asOf?.Date.AddDays(1).AddTicks(-1);

            var m = new StudentStatementViewModel
            {
                Student = student,
                AsOf = asOf,
                PrintedAtUtc = _clock.UtcNow,
                Statement = await _statements.BuildForStudentAsync(id, asOfUtc),
                OpenCharges = await FinanceQueries.ChargeRowsAsync(_db, studentId: id),
                SchoolNameAr = school?.NameAr ?? "",
                SchoolNameEn = school?.NameEn ?? "",
                SchoolAddress = school?.AddressLine,
            };

            // Deliberately not multi-year-scoped: the statement is every year this child has been
            // billed for, which is what "كشف حساب" means to a family and what BR-GLB-064's as-of
            // date is for. The year picker on the other two screens narrows a working view, not this.
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking().Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            var enrollment = await _db.Enrollments.AsNoTracking().Where(e => e.StudentId == id && e.ExitDate == null)
                .OrderByDescending(e => e.EnrollmentDate).FirstOrDefaultAsync();
            if (enrollment != null)
            {
                var profile = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(p => p.Id == enrollment.GradeYearProfileId);
                var gradeLevel = profile == null ? null : grades.FirstOrDefault(g => g.Id == profile.GradeLevelId);
                m.GradeName = gradeLevel == null ? null : (IsArabic ? gradeLevel.Name.NameAr : gradeLevel.Name.NameEn);
                var membership = await _db.SectionMemberships.AsNoTracking().FirstOrDefaultAsync(x => x.EnrollmentId == enrollment.Id && x.EffectiveToUtc == null);
                if (membership != null)
                {
                    var sec = await _db.Sections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == membership.SectionId);
                    m.SectionName = sec == null ? null : (IsArabic ? sec.NameAr : sec.NameEn);
                }
            }

            var guardians = await GuardiansOfAsync(id);
            m.Guardians = guardians.Select(g => g.Parent).ToList();

            var now = _clock.UtcNow;
            m.Aging = m.OpenCharges.GroupBy(r => ReceivablesAgingBucketer.Bucket(r.Charge.PostedAtUtc, now))
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Remaining));
            return View(m);
        }

        // ================================================================== helpers

        private static bool Contains(string? haystack, string needle)
            => haystack != null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

        /// <summary>Grade sequence for row ordering; students with no live enrollment sort last rather than first.</summary>
        private static int GradeOrderOf(int studentId, IReadOnlyList<Enrollment> enrollments, IReadOnlyList<GradeYearProfile> profiles, IReadOnlyList<GradeLevel> grades)
        {
            var enrollment = enrollments.FirstOrDefault(e => e.StudentId == studentId);
            var profile = enrollment == null ? null : profiles.FirstOrDefault(p => p.Id == enrollment.GradeYearProfileId);
            var grade = profile == null ? null : grades.FirstOrDefault(g => g.Id == profile.GradeLevelId);
            return grade?.SequenceOrder ?? int.MaxValue;
        }

        /// <summary>
        /// BR-SEC-010: a clerk who may read the roll but not open the cashier gets no Pay button, and
        /// one who may not issue statements gets no statement button — rather than a link that
        /// answers 404 after they have already told a parent it was coming.
        /// </summary>
        private async Task FillStudentFinanceRightsAsync(object model)
        {
            var canPay = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Cashier, ActionVerb.View, HttpContext.RequestAborted);
            var canPrint = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.StudentFinance, ActionVerb.Print, HttpContext.RequestAborted);
            switch (model)
            {
                case StudentFinanceListViewModel list:
                    list.CanOpenCashier = canPay;
                    list.CanPrintStatement = canPrint;
                    break;
                case StudentFinanceDetailViewModel detail:
                    detail.CanOpenCashier = canPay;
                    detail.CanPrintStatement = canPrint;
                    break;
            }
        }

        private async Task<IReadOnlyList<StudentFinanceDetailViewModel.GuardianRow>> GuardiansOfAsync(int studentId)
        {
            var links = await _db.StudentGuardianLinks.AsNoTracking().Where(l => l.StudentId == studentId && l.EffectiveToUtc == null).ToListAsync();
            var parentIds = links.Select(l => l.ParentId).ToList();
            var parents = await _db.Parents.IgnoreQueryFilters().AsNoTracking().Where(p => parentIds.Contains(p.Id)).ToListAsync();
            var payers = await _db.Payers.AsNoTracking().Where(p => p.ParentId != null && parentIds.Contains(p.ParentId.Value)).ToListAsync();
            // Retiring "father" from the relationship catalogue must not blank the word off a family
            // that already uses it, so the label is read outside the soft-active filter.
            var relIds = links.Select(l => l.RelationshipLookupId).Distinct().ToList();
            var relationships = await _db.LookupValues.IgnoreQueryFilters().AsNoTracking().Where(v => relIds.Contains(v.Id)).ToListAsync();

            return links
                .Select(l =>
                {
                    var parent = parents.FirstOrDefault(p => p.Id == l.ParentId);
                    var rel = relationships.FirstOrDefault(v => v.Id == l.RelationshipLookupId);
                    return parent == null ? null : new StudentFinanceDetailViewModel.GuardianRow(
                        parent, rel?.Name.NameAr ?? "", rel?.Name.NameEn ?? "", l.IsFinanciallyResponsible,
                        payers.FirstOrDefault(p => p.ParentId == l.ParentId)?.Id);
                })
                .Where(r => r != null).Select(r => r!)
                .OrderByDescending(r => r.IsFinanciallyResponsible).ToList();
        }

        private async Task<IReadOnlyList<StudentFinanceDetailViewModel.DiscountRow>> DiscountsOfAsync(int studentId, int yearId, IReadOnlyList<int> chargeIds)
        {
            var grants = await _db.DiscountGrants.AsNoTracking()
                .Where(g => g.StudentId == studentId && g.AcademicYearId == yearId).ToListAsync();
            if (grants.Count == 0) return Array.Empty<StudentFinanceDetailViewModel.DiscountRow>();

            var typeIds = grants.Select(g => g.DiscountTypeId).Distinct().ToList();
            var types = await _db.DiscountTypes.IgnoreQueryFilters().AsNoTracking().Where(t => typeIds.Contains(t.Id)).ToListAsync();
            var grantIds = grants.Select(g => g.Id).ToList();
            var documents = await _db.DiscountDocuments.AsNoTracking()
                .Where(d => grantIds.Contains(d.DiscountGrantId) && chargeIds.Contains(d.ChargeId))
                .Select(d => new { d.DiscountGrantId, d.Amount }).ToListAsync();

            return grants
                .Select(g => new StudentFinanceDetailViewModel.DiscountRow(
                    g,
                    types.FirstOrDefault(t => t.Id == g.DiscountTypeId),
                    documents.Where(d => d.DiscountGrantId == g.Id).Sum(d => d.Amount),
                    documents.Count(d => d.DiscountGrantId == g.Id)))
                .OrderBy(r => r.Grant.Status).ThenByDescending(r => r.Applied).ToList();
        }

        private async Task<IReadOnlyList<StudentFinanceDetailViewModel.PlanRow>> PlansOfAsync(int studentId, int yearId, IReadOnlyList<FeeCategory> categories)
        {
            var assignments = await _db.PlanAssignments.AsNoTracking()
                .Where(a => a.StudentId == studentId && a.AcademicYearId == yearId).ToListAsync();
            if (assignments.Count == 0) return Array.Empty<StudentFinanceDetailViewModel.PlanRow>();

            var assignmentIds = assignments.Select(a => a.Id).ToList();
            var templateIds = assignments.Select(a => a.PlanTemplateId).Distinct().ToList();
            var templates = await _db.PlanTemplates.IgnoreQueryFilters().AsNoTracking().Where(t => templateIds.Contains(t.Id)).ToListAsync();
            var installments = await _db.Installments.AsNoTracking()
                .Where(i => assignmentIds.Contains(i.PlanAssignmentId) && !i.IsSuperseded)
                .OrderBy(i => i.SequenceNumber).ToListAsync();

            // How much of each installment the receipts have actually covered. An installment names
            // the charges it draws on (InstallmentChargeLine), and BR-PAY-003 allocates a receipt to
            // charges — so the covered share is the allocation against those charges, capped at the
            // line, not a second ledger of its own.
            var installmentIds = installments.Select(i => i.Id).ToList();
            var sequenceOf = installments.ToDictionary(i => i.Id, i => i.SequenceNumber);
            var chargeLines = await _db.InstallmentChargeLines.AsNoTracking()
                .Where(l => installmentIds.Contains(l.InstallmentId))
                .Select(l => new { l.InstallmentId, l.ChargeId, l.Amount }).ToListAsync();
            var lineChargeIds = chargeLines.Select(l => l.ChargeId).Distinct().ToList();
            var allocations = await _db.PaymentAllocations.AsNoTracking()
                .Where(a => lineChargeIds.Contains(a.ChargeId))
                .Select(a => new { a.ChargeId, a.AllocatedAmount }).ToListAsync();
            var allocatedByCharge = allocations.GroupBy(a => a.ChargeId).ToDictionary(g => g.Key, g => g.Sum(a => a.AllocatedAmount));
            var coveredById = InstallmentCoverageCalculator.Cover(
                chargeLines.Select(l => new InstallmentCoverageCalculator.ScheduleLine(
                    l.InstallmentId, l.ChargeId, l.Amount, sequenceOf.GetValueOrDefault(l.InstallmentId, int.MaxValue))),
                allocatedByCharge);

            var today = _clock.UtcNow;
            return assignments.Select(a =>
                {
                    var template = templates.FirstOrDefault(t => t.Id == a.PlanTemplateId);
                    return new StudentFinanceDetailViewModel.PlanRow(
                        a,
                        template,
                        a.FeeCategoryId == null ? null : categories.FirstOrDefault(c => c.Id == a.FeeCategoryId),
                        installments.Where(i => i.PlanAssignmentId == a.Id)
                            .Select(i =>
                            {
                                var covered = coveredById.TryGetValue(i.Id, out var c) ? c : 0m;
                                return new StudentFinanceDetailViewModel.InstallmentRow(i, covered,
                                    InstallmentStatusDeriver.Derive(i.Amount, covered, i.DueDate, template?.GraceDays ?? 0, today, i.IsSuperseded, i.IsWrittenOff));
                            }).ToList());
                })
                .ToList();
        }
    }
}
