using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Examinations
{
    /// <summary>core.ExamRound (doc/Modules/16 §7, BR-EXM-003/§4): e.g. "Final Exams Term 1" — the container a round's dated Exams schedule within.</summary>
    [Audited(AuditTier.T2)]
    public class ExamRound : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int TermId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public ExamRoundStatus Status { get; set; } = ExamRoundStatus.Draft;

        public DateTime? PublishedAtUtc { get; set; }
    }
}
