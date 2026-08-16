using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Students
{
    /// <summary>Pure BR-STU-003/BR-PAR-005: every active student needs ≥ 1 financially-responsible guardian link.</summary>
    public static class FinancialResponsibilityGuard
    {
        public static bool HasAtLeastOneResponsible(IEnumerable<bool> financiallyResponsibleFlags)
            => financiallyResponsibleFlags.Any(f => f);
    }
}
