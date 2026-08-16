using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Portal
{
    /// <summary>Pure BR-SEC-011: a portal caller may view a student's data if it's their own record (student self-service) or a guardian-visible child.</summary>
    public static class PortalAccessEvaluator
    {
        public static bool CanView(int studentId, int? studentOwnUserAccountId, int requestingUserAccountId, IReadOnlyList<int> guardianVisibleStudentIds)
        {
            if (studentOwnUserAccountId.HasValue && studentOwnUserAccountId.Value == requestingUserAccountId)
            {
                return true;
            }

            return guardianVisibleStudentIds.Contains(studentId);
        }
    }
}
