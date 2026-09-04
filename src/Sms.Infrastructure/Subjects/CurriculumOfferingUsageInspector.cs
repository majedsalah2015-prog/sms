using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Guards;
using Sms.Domain.Subjects;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Subjects
{
    /// <summary>
    /// What stands in the way of removing a subject from a grade's plan.
    /// <para>
    /// BR-SUB-004 names three — marks, timetable sessions, teacher assignments —
    /// and the rule behind them is what this counts: anything that would be left
    /// pointing at a plan line that no longer exists. Exams and term results
    /// belong to that set as plainly as the three the rule spells out.
    /// </para>
    /// <para>
    /// Marks are counted through their blueprint. A marksheet does not reference
    /// the offering directly, so counting blueprints alone would let an offering
    /// with a blueprint but no marks read as "1 blueprint" when what the teacher
    /// actually loses is a term of marks; both are reported, so the refusal says
    /// which it is.
    /// </para>
    /// <para>
    /// An end-dated offering counts as much as a live one. Ending it closes it
    /// for future terms and leaves every mark ever recorded against it standing —
    /// which is the whole reason ending exists as an alternative to removing.
    /// </para>
    /// <para>
    /// Lessons and homework are counted for the same reason, though BR-SUB-004
    /// does not name them: BR-LRN-001 anchors every piece of e-learning content
    /// on the offering rather than the subject, and promises that ending one
    /// leaves its content readable. Removing an offering out from under a term's
    /// lessons would break that promise — and, because every non-ownership
    /// cascade in this model is downgraded to Restrict, would surface as a
    /// foreign-key violation rather than as an answer.
    /// </para>
    /// </summary>
    public class CurriculumOfferingUsageInspector : IUsageInspector<CurriculumOffering>
    {
        private readonly AppDbContext _db;

        public CurriculumOfferingUsageInspector(AppDbContext db)
        {
            _db = db;
        }

        public async Task<UsageReport> InspectAsync(int id, CancellationToken cancellationToken = default)
        {
            var blueprintIds = await _db.Blueprints
                .Where(b => b.CurriculumOfferingId == id)
                .Select(b => b.Id)
                .ToListAsync(cancellationToken);

            var marksheets = blueprintIds.Count == 0
                ? 0
                : await _db.Marksheets.CountAsync(s => blueprintIds.Contains(s.BlueprintId), cancellationToken);

            var placements = await _db.Placements.CountAsync(p => p.CurriculumOfferingId == id, cancellationToken);
            var assignments = await _db.TeacherAssignments.CountAsync(a => a.CurriculumOfferingId == id, cancellationToken);
            var exams = await _db.Exams.CountAsync(e => e.CurriculumOfferingId == id, cancellationToken);
            var results = await _db.TermResults.CountAsync(r => r.CurriculumOfferingId == id, cancellationToken);
            var lessons = await _db.Lessons.CountAsync(l => l.CurriculumOfferingId == id, cancellationToken);
            var homework = await _db.Homeworks.CountAsync(h => h.CurriculumOfferingId == id, cancellationToken);

            return UsageReport.From(
                new UsageReference("marksheet(s)", "كشف درجات", marksheets),
                new UsageReference("blueprint(s)", "مخطط درجات", blueprintIds.Count),
                new UsageReference("timetable session(s)", "حصة في الجدول", placements),
                new UsageReference("teacher assignment(s)", "إسناد معلم", assignments),
                new UsageReference("exam(s)", "اختبار", exams),
                new UsageReference("term result(s)", "نتيجة فصل", results),
                new UsageReference("lesson(s)", "درس", lessons),
                new UsageReference("homework item(s)", "واجب", homework));
        }
    }
}
