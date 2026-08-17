using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Timetable
{
    /// <summary>
    /// core.Substitution (doc/Modules/15 §7, BR-TTB-007): the who/why detail
    /// behind a Session flipped to Substituted — kept separate from
    /// Session so the substitution register report (by teacher/period,
    /// feeding Module 12's payroll-prep count per BR-TTB-007) survives
    /// even if the session later changes again.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Substitution : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int SessionId { get; set; }

        public int SubstituteTeacherProfileId { get; set; }

        public string Reason { get; set; } = string.Empty;

        /// <summary>BR-TTB-007: counted per teacher for payroll-prep (Module 12 Q3's accepted export line) — the export itself is deferred, this just flags the countable rows.</summary>
        public bool IsCountedForPayroll { get; set; } = true;

        public DateTime AssignedAtUtc { get; set; }
    }
}
