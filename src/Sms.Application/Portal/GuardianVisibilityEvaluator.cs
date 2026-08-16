using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Portal
{
    /// <summary>Pure BR-SEC-011/BR-PAR-004: a parent's portal-visible children = active (not unlinked), portal-visible guardianship links.</summary>
    public static class GuardianVisibilityEvaluator
    {
        public readonly struct GuardianLink
        {
            public GuardianLink(int studentId, bool isPortalVisible, DateTime? effectiveToUtc)
            {
                StudentId = studentId;
                IsPortalVisible = isPortalVisible;
                EffectiveToUtc = effectiveToUtc;
            }

            public int StudentId { get; }

            public bool IsPortalVisible { get; }

            public DateTime? EffectiveToUtc { get; }
        }

        public static IReadOnlyList<int> GetVisibleStudentIds(IEnumerable<GuardianLink> links)
            => links.Where(l => l.IsPortalVisible && l.EffectiveToUtc == null).Select(l => l.StudentId).Distinct().ToList();
    }
}
