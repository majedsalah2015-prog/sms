using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Grades
{
    /// <summary>
    /// Pure BR-GRD-002/009: the promotion ladder must be acyclic and
    /// complete. Checked at year activation per doc/Modules/05 §9 — the
    /// caller (AcademicYearAdmin.ActivateAsync) wiring this in is a
    /// follow-up integration point, not done in this slice.
    /// </summary>
    public static class PromotionPathValidator
    {
        /// <summary>Every non-graduating grade must declare a target (BR-GRD-002).</summary>
        public static IReadOnlyList<int> FindGradesMissingPromotionTarget(IEnumerable<GradeSnapshot> grades)
            => grades.Where(g => !g.IsGraduating && g.PromotionTargetGradeLevelId == null)
                     .Select(g => g.GradeLevelId)
                     .ToList();

        /// <summary>True if following any grade's promotion chain revisits a grade before reaching a terminus.</summary>
        public static bool HasCycle(IEnumerable<GradeSnapshot> grades)
        {
            var targets = grades.ToDictionary(g => g.GradeLevelId, g => g.PromotionTargetGradeLevelId);

            foreach (var start in targets.Keys)
            {
                var visited = new HashSet<int>();
                var current = start;

                while (targets.TryGetValue(current, out var next) && next.HasValue)
                {
                    if (!visited.Add(current))
                    {
                        return true;
                    }

                    current = next.Value;
                }
            }

            return false;
        }
    }
}
