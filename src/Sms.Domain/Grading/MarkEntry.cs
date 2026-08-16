using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Grading
{
    /// <summary>
    /// core.MarkEntry (doc/Modules/17 §7, BR-GRA-002/011): one student x
    /// component mark. T1 so every Score change is field-diff-logged
    /// (BR-GRA-011: "T1-audited from first entry"), but deliberately
    /// WITHOUT [RequiresAuditReason] — CreateMarksheetAsync pre-seeds one
    /// stub row (Score = null) per student x component so completeness
    /// can be tracked, which means a teacher's very first real entry is
    /// an EF "Modified" transition, not "Added"; demanding a reason on
    /// every routine mark entry would be impractical. Contrast with
    /// E-301's AttendanceDay.Status, where a value already exists from
    /// capture and only a later WF-14 correction needs the ambient reason.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class MarkEntry : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MarksheetId { get; set; }

        public int BlueprintComponentId { get; set; }

        public int EnrollmentId { get; set; }

        public decimal? Score { get; set; }

        public bool IsAbsent { get; set; }

        public bool IsExempt { get; set; }
    }
}
