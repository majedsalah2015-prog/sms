using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Students
{
    /// <summary>
    /// ppl.Enrollment (DB doc A3 pivotal spec — "the year participation
    /// pivot"): student × academic year × grade placement. The FK target
    /// for attendance, marks, fees, services, and — as of this slice —
    /// SectionMembership (E-103), which previously carried an unconstrained
    /// EnrollmentId placeholder pending this entity's existence.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Enrollment : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int StudentId { get; set; }

        public int GradeYearProfileId { get; set; }

        public DateTime EnrollmentDate { get; set; }

        public DateTime? ExitDate { get; set; }

        public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

        public EnrollmentSourceType SourceType { get; set; }
    }
}
