using System;
using Sms.Domain.Common;

namespace Sms.Domain.Schools
{
    /// <summary>
    /// core.Semester (BR-AYR-007): configurable per year. Locking once
    /// marks/invoices reference it is deferred — that trigger needs
    /// Grading/Fees modules that don't exist yet.
    /// </summary>
    public class Semester : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int SequenceNumber { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }
    }
}
