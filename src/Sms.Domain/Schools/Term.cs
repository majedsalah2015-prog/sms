using System;
using Sms.Domain.Common;

namespace Sms.Domain.Schools
{
    /// <summary>core.Term (BR-AYR-007): nested within a Semester's date range.</summary>
    public class Term : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int SemesterId { get; set; }

        public int SequenceNumber { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }
    }
}
