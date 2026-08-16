using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Employees
{
    /// <summary>
    /// ppl.Qualification (doc/Modules/12 §7, BR-EMP-004): degrees,
    /// certifications, licenses. IsTeachingRelevant flags entries that
    /// should feed the BR-SUB-006 qualification matrix — the actual feed
    /// (auto-populating Sms.Domain.Subjects.TeacherSubjectQualification)
    /// is deferred, same as this slice's other cross-module wiring.
    /// TrainingRecord (PD hours) is deferred entirely — no ministry
    /// PD-hour reporting consumer exists yet.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Qualification : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int EmployeeId { get; set; }

        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        public string? InstitutionName { get; set; }

        public DateTime DateAwarded { get; set; }

        public bool IsTeachingRelevant { get; set; }

        public int? DocumentAttachmentId { get; set; }
    }
}
