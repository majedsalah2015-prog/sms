using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Transport
{
    /// <summary>
    /// svc.TransportSubscription (doc/Modules/23 §7, BR-TRN-004): a
    /// service subscription per enrollment (year) — AM stop and PM stop
    /// may differ. Registration posts the zone-priced transport charge
    /// (E-303 PostChargeAsync, ChargeSourceType.ServiceAssignment);
    /// ChargeId links it. Suspension (BR-TRN-008) is effective-dated and
    /// reason-required (T1); IsSelfReleaseAllowed is the secondary-stage
    /// parent-consent flag of BR-TRN-006.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class TransportSubscription : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int EnrollmentId { get; set; }

        public int StudentId { get; set; }

        public int PayerId { get; set; }

        public int? AmRouteStopId { get; set; }

        public int? PmRouteStopId { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public TransportSubscriptionStatus Status { get; set; } = TransportSubscriptionStatus.Active;

        public int? ChargeId { get; set; }

        public bool IsSelfReleaseAllowed { get; set; }

        [RequiresAuditReason]
        public DateTime? SuspendedEffectiveDate { get; set; }

        public string? SuspensionReason { get; set; }

        public int? SuspensionApprovedByUserId { get; set; }
    }

    /// <summary>BR-TRN-003 waiting list per route (BR-ADM-006 pattern): a subscription request that exceeded bus capacity, in arrival order.</summary>
    public class RouteWaitlist : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int RouteId { get; set; }

        public int TransportSubscriptionId { get; set; }

        public DateTime QueuedAtUtc { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
