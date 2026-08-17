using System;

namespace Sms.Application.Installments
{
    /// <summary>
    /// Pure BR-INS-005 routing: P3 (officer proposes → Finance Manager
    /// approves) by default; + Principal (P4) when the proposal extends
    /// the last due date beyond N months or crosses the academic
    /// year-end. The chain itself isn't routed (status-only workflow
    /// substitution) — this decides which flag the case carries.
    /// </summary>
    public static class RescheduleApprovalRouter
    {
        public static bool RequiresPrincipal(DateTime originalLastDueDate, DateTime proposedLastDueDate, DateTime academicYearEndDate, int maxExtensionMonths)
        {
            if (proposedLastDueDate.Date > academicYearEndDate.Date)
            {
                return true;
            }

            return proposedLastDueDate.Date > originalLastDueDate.Date.AddMonths(maxExtensionMonths);
        }
    }
}
