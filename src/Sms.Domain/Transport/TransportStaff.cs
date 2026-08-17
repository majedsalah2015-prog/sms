using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Transport
{
    /// <summary>
    /// svc.TransportStaff (doc/Modules/23 §7, BR-TRN-002): a driver or
    /// attendant — either an employee (EmployeeId) or a light contractor
    /// record (ContractorName). Drivers carry a licence (mandatory, class
    /// + expiry) — validated against the bus's required class at trip
    /// open. Substitutions are per-trip (Trip.DriverId can differ from
    /// Route.DriverId), the simplified mirror of BR-TTB-007.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class TransportStaff : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public TransportStaffKind Kind { get; set; }

        public int? EmployeeId { get; set; }

        public string? ContractorName { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string? LicenseNo { get; set; }

        public LicenseClass? LicenseClass { get; set; }

        public DateTime? LicenseExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
