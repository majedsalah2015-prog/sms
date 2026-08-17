using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Activities
{
    /// <summary>ppl.ActivitySession (doc/Modules/29 §7, BR-ACT-003): a dated meeting of a Program — the roster (from active ProgramEnrollments) is captured against, not generated automatically from the weekly slot (mirrors Timetable's Session shape but stays manually created here, simpler scope).</summary>
    [Audited(AuditTier.T2)]
    public class ActivitySession : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ProgramId { get; set; }

        public DateTime Date { get; set; }
    }
}
