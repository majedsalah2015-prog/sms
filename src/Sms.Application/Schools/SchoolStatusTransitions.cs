using Sms.Domain.Schools;

namespace Sms.Application.Schools
{
    /// <summary>
    /// Pure BR-SCH-005 transition rules. Setup→Active's wizard-completion
    /// gate (BR-SET-003) isn't enforced here — the Setup Wizard/checklist
    /// (E-101) doesn't exist yet; this only enforces which status pairs are
    /// legal moves at all. Closed is terminal.
    /// </summary>
    public static class SchoolStatusTransitions
    {
        public static bool CanTransition(SchoolStatus from, SchoolStatus to)
        {
            return (from, to) switch
            {
                (SchoolStatus.Setup, SchoolStatus.Active) => true,
                (SchoolStatus.Active, SchoolStatus.Suspended) => true,
                (SchoolStatus.Suspended, SchoolStatus.Active) => true, // reactivation
                (SchoolStatus.Active, SchoolStatus.Closed) => true,
                (SchoolStatus.Suspended, SchoolStatus.Closed) => true,
                _ => false,
            };
        }
    }
}
