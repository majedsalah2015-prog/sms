using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Setup
{
    /// <summary>
    /// core.SetupChecklist (doc/Modules/01 §7, BR-SET-003): one row per
    /// wizard step per school, tracking completion. Steps can be revisited
    /// (re-completed) until "Setup Complete" is declared, which stamps
    /// School.SetupCompletedAtUtc once every mandatory step is Completed.
    /// Record-level audit is enough — the interesting fact is who completed
    /// which step when, which the row itself carries.
    /// </summary>
    [Audited(AuditTier.T3)]
    public class SetupChecklist : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        /// <summary>One of <c>SetupWizardSteps</c> (Sms.Application.Setup).</summary>
        public string StepCode { get; set; } = string.Empty;

        public SetupStepStatus Status { get; set; } = SetupStepStatus.Pending;

        public DateTime? CompletedAtUtc { get; set; }

        public int? CompletedByUserId { get; set; }

        public string? Notes { get; set; }
    }
}
