using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Guards;
using Sms.Application.Students;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Students
{
    /// <summary>
    /// What stands in the way of removing an enrollment — the answer to "was anything ever recorded
    /// against this year participation, or is it only a typing mistake?" (BR-GLB-005).
    /// <para>
    /// The enrollment is the pivot the whole year hangs off: attendance is taken against it, marks
    /// are entered against it, the bus subscription and the fee schedule are written against it. So
    /// the question is not whether the row may be deleted in the abstract — it is whether anything
    /// downstream would lose the record it points at. One attendance day makes this a piece of
    /// history and history is withdrawn, never removed.
    /// </para>
    /// <para>
    /// Two shapes of reference are counted. Most modules name the enrollment directly. Fees,
    /// instalment plans and discounts name the <i>student in a year</i> instead, which is the same
    /// thing said differently — BR-GLB-024 allows one active enrollment per student per year, so a
    /// charge in this enrollment's year is a charge against this enrollment.
    /// </para>
    /// <para>
    /// Section memberships are deliberately <b>not</b> counted. They are not something that happened
    /// to the placement, they <i>are</i> the placement, and they go when it goes — the same line
    /// <c>StudentAdmin.DeleteStudentAsync</c> draws when it removes a student's memberships but
    /// refuses on their attendance.
    /// </para>
    /// <para>
    /// <b>One list, two entry points.</b> <see cref="ReferencesAsync"/> holds the whole list of
    /// things that can reference an enrollment; the single-record guard and the batch the student
    /// file needs are both built from it, so a module added to one is added to the other.
    /// </para>
    /// </summary>
    public class EnrollmentUsageInspector : IEnrollmentUsageInspector
    {
        /// <summary>
        /// How fees, plans and discounts name a year participation — the student and the year rather
        /// than the enrollment row. A named type rather than a tuple because EF has to project into
        /// it, and a tuple projection is the kind of thing that translates on one provider and not
        /// the other.
        /// </summary>
        private sealed record StudentYear(int StudentId, int AcademicYearId);

        private readonly AppDbContext _db;

        public EnrollmentUsageInspector(AppDbContext db)
        {
            _db = db;
        }

        public async Task<UsageReport> InspectAsync(int id, CancellationToken cancellationToken = default)
        {
            var reports = await InspectManyAsync(new[] { id }, cancellationToken);
            return reports.TryGetValue(id, out var report) ? report : UsageReport.Free;
        }

        public async Task<IReadOnlyDictionary<int, UsageReport>> InspectManyAsync(
            IReadOnlyList<int> enrollmentIds, CancellationToken cancellationToken = default)
        {
            var ids = enrollmentIds.Distinct().ToList();
            var result = ids.ToDictionary(id => id, _ => UsageReport.Free);
            if (ids.Count == 0)
            {
                return result;
            }

            var counts = await ReferencesAsync(ids, cancellationToken);
            foreach (var id in ids)
            {
                result[id] = UsageReport.From(
                    counts.Select(c => new UsageReference(
                        c.ResourceEn, c.ResourceAr, c.ByEnrollment.TryGetValue(id, out var n) ? n : 0))
                    .ToArray());
            }

            return result;
        }

        /// <summary>
        /// Every kind of thing that can reference an enrollment, counted for the whole batch in one
        /// grouped query each. Adding a module that points at an enrollment means adding one entry
        /// here and nowhere else.
        /// </summary>
        private async Task<IReadOnlyList<(string ResourceEn, string ResourceAr, IReadOnlyDictionary<int, int> ByEnrollment)>> ReferencesAsync(
            IReadOnlyList<int> ids, CancellationToken ct)
        {
            // Fees, plans and discounts are keyed by student-and-year rather than by enrollment, so
            // the enrollments themselves are read first to translate between the two.
            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .Select(e => new { e.Id, e.StudentId, e.AcademicYearId })
                .ToListAsync(ct);
            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
            var yearIds = enrollments.Select(e => e.AcademicYearId).Distinct().ToList();

            // Grouped in memory rather than in SQL, on purpose. The provider difference this
            // codebase keeps paying for is aggregates: a GroupBy over a composite key translates on
            // SQL Server and can fall over on Sqlite, and it would do so at run time on a screen
            // rather than at build time. The volumes are one student's records over one school
            // career, so materializing the keys costs nothing worth this risk.
            async Task<IReadOnlyDictionary<int, int>> ByEnrollmentAsync(IQueryable<int> enrollmentIdSource)
            {
                var rows = await enrollmentIdSource.ToListAsync(ct);
                return rows.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            }

            // A student-and-year count is attributed back to whichever of this batch's enrollments
            // carries that pair — one, by BR-GLB-024, for anything active.
            async Task<IReadOnlyDictionary<int, int>> ByStudentYearAsync(IQueryable<StudentYear> source)
            {
                var rows = await source.ToListAsync(ct);
                var map = new Dictionary<int, int>();
                foreach (var e in enrollments)
                {
                    var n = rows.Count(r => r.StudentId == e.StudentId && r.AcademicYearId == e.AcademicYearId);
                    if (n > 0)
                    {
                        map[e.Id] = n;
                    }
                }

                return map;
            }

            return new List<(string, string, IReadOnlyDictionary<int, int>)>
            {
                ("attendance day(s)", "يوم حضور", await ByEnrollmentAsync(_db.AttendanceDays.AsNoTracking().Where(a => ids.Contains(a.EnrollmentId)).Select(a => a.EnrollmentId))),
                ("gate event(s)", "حركة بوابة", await ByEnrollmentAsync(_db.GateEvents.AsNoTracking().Where(g => ids.Contains(g.EnrollmentId)).Select(g => g.EnrollmentId))),
                ("leave pass(es)", "إذن خروج", await ByEnrollmentAsync(_db.LeavePasses.AsNoTracking().Where(l => ids.Contains(l.EnrollmentId)).Select(l => l.EnrollmentId))),
                ("mark entry(ies)", "درجة مرصودة", await ByEnrollmentAsync(_db.MarkEntries.AsNoTracking().Where(m => ids.Contains(m.EnrollmentId)).Select(m => m.EnrollmentId))),
                ("term result(s)", "نتيجة فصل", await ByEnrollmentAsync(_db.TermResults.AsNoTracking().Where(t => ids.Contains(t.EnrollmentId)).Select(t => t.EnrollmentId))),
                ("year result(s)", "نتيجة عام", await ByEnrollmentAsync(_db.YearResults.AsNoTracking().Where(y => ids.Contains(y.EnrollmentId)).Select(y => y.EnrollmentId))),
                ("exam attendance record(s)", "حضور اختبار", await ByEnrollmentAsync(_db.ExamAttendances.AsNoTracking().Where(x => ids.Contains(x.EnrollmentId)).Select(x => x.EnrollmentId))),
                ("exam incident(s)", "مخالفة اختبار", await ByEnrollmentAsync(_db.ExamIncidents.AsNoTracking().Where(x => ids.Contains(x.EnrollmentId)).Select(x => x.EnrollmentId))),
                ("make-up eligibility record(s)", "أهلية دور ثانٍ", await ByEnrollmentAsync(_db.MakeupEligibilities.AsNoTracking().Where(x => ids.Contains(x.EnrollmentId)).Select(x => x.EnrollmentId))),
                ("transport subscription(s)", "اشتراك نقل", await ByEnrollmentAsync(_db.TransportSubscriptions.AsNoTracking().Where(t => ids.Contains(t.EnrollmentId)).Select(t => t.EnrollmentId))),

                // The rollover names an enrollment twice — as where a child came from and where they
                // went — and either is a reason not to remove it.
                ("rollover record(s)", "سجل ترحيل", await ByEnrollmentAsync(
                    _db.RolloverStudentStates.AsNoTracking().Where(r => ids.Contains(r.SourceEnrollmentId)).Select(r => r.SourceEnrollmentId)
                        .Concat(_db.RolloverStudentStates.AsNoTracking().Where(r => r.TargetEnrollmentId != null && ids.Contains(r.TargetEnrollmentId.Value)).Select(r => r.TargetEnrollmentId!.Value)))),

                ("fee charge(s)", "رسم مستحق", await ByStudentYearAsync(
                    _db.Charges.AsNoTracking().Where(c => studentIds.Contains(c.StudentId) && yearIds.Contains(c.AcademicYearId))
                        .Select(c => new StudentYear(c.StudentId, c.AcademicYearId)))),
                ("instalment plan(s)", "خطة تقسيط", await ByStudentYearAsync(
                    _db.PlanAssignments.AsNoTracking().Where(p => studentIds.Contains(p.StudentId) && yearIds.Contains(p.AcademicYearId))
                        .Select(p => new StudentYear(p.StudentId, p.AcademicYearId)))),
                ("discount grant(s)", "منح خصم", await ByStudentYearAsync(
                    _db.DiscountGrants.AsNoTracking().Where(d => studentIds.Contains(d.StudentId) && yearIds.Contains(d.AcademicYearId))
                        .Select(d => new StudentYear(d.StudentId, d.AcademicYearId)))),
            };
        }
    }
}
