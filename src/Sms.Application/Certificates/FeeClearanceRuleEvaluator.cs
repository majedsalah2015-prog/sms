using System;
using Sms.Domain.Certificates;

namespace Sms.Application.Certificates
{
    /// <summary>
    /// Pure BR-CRT-008: is the student's financial position acceptable under
    /// the type's clearance rule? Positions follow E-303's convention
    /// (positive = owes). NoOverdue takes the overdue slice of the position
    /// separately so the rule is fully specified here even though no
    /// caller can supply a real overdue figure yet (Charge has no due date).
    /// </summary>
    public static class FeeClearanceRuleEvaluator
    {
        public static bool IsClear(FeeClearanceRule rule, decimal position, decimal overduePosition)
        {
            return rule switch
            {
                FeeClearanceRule.Disabled => true,
                FeeClearanceRule.NoOverdue => overduePosition <= 0m,
                FeeClearanceRule.FullClearance => position <= 0m,
                _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unknown fee clearance rule."),
            };
        }
    }
}
