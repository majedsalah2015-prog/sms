using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Activities
{
    /// <summary>
    /// ppl.Achievement (doc/Modules/29 §7, BR-ACT-006): writes to student
    /// file tabs 11/12 (BR-STU-004 — the tabs themselves aren't built,
    /// this is the source row a future tab would read). CertificateIssueId
    /// optionally links an E-403 honor certificate; issuing one is the
    /// caller's job (compose IActivityAdmin + ICertificateAdmin), not
    /// done automatically here.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Achievement : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StudentId { get; set; }

        public int? ProgramId { get; set; }

        public int? CompetitionEventId { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTime AwardedAtUtc { get; set; }

        public int? CertificateIssueId { get; set; }
    }
}
