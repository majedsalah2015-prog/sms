using System;
using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Transport
{
    /// <summary>
    /// svc.Route (doc/Modules/23 §7, BR-TRN-003): ordered stops with
    /// times, one direction, an assigned bus + driver (+ optional
    /// attendant — doc Q1: KSA requires supervisors, so attendant is
    /// modeled but not mandatory until the pack says so). Numbered from
    /// doc 08's "RTE" series (seeded by E-010). Capacity is a hard check
    /// against Bus.Capacity at subscription time; overflow goes to the
    /// waitlist (BR-ADM-006 pattern).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Route : AuditableEntity, ISchoolScoped, IYearScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public string RouteNo { get; set; } = string.Empty;

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public RouteDirection Direction { get; set; }

        public int BusId { get; set; }

        public int DriverId { get; set; }

        public int? AttendantId { get; set; }

        public bool IsActive { get; set; } = true;

        public List<RouteStop> Stops { get; set; } = new();
    }

    /// <summary>BR-TRN-003 stop: bilingual named point, optional geo, scheduled time; ZoneFeeCategoryId is the transport fee-category variant priced for this stop's zone (doc §7: "zone pricing lives in fee structures").</summary>
    public class RouteStop : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int RouteId { get; set; }

        public int SequenceNumber { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public TimeSpan ScheduledTime { get; set; }

        public int ZoneFeeCategoryId { get; set; }
    }
}
