using Sms.Application.Common.Interfaces;

namespace Sms.Infrastructure.Common
{
    /// <summary>
    /// v1 single-tenant deployment: one database = one customer (doc 02 §4).
    /// SchoolId comes from configuration until M02 (Schools) provides real
    /// resolution; the working year is a fixed default until M03 (Academic Years)
    /// delivers the year switcher and Active-year rules (BR-GLB-021).
    /// </summary>
    public sealed class StaticTenantContext : ITenantContext, IWorkingYearContext
    {
        public StaticTenantContext(int schoolId, int academicYearId)
        {
            SchoolId = schoolId;
            AcademicYearId = academicYearId;
        }

        public int SchoolId { get; }

        public int AcademicYearId { get; }
    }
}
