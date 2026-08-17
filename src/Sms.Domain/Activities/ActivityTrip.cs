using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Activities
{
    /// <summary>
    /// ppl.Trip (doc/Modules/29 §7, BR-ACT-004) — named <c>ActivityTrip</c>
    /// because <c>Sms.Domain.Transport.Trip</c> already exists (E-601's ad-hoc
    /// trip execution entity); same collision-avoidance call as
    /// <c>ActivityProgram</c>. Companion 1:1 row to an ActivityProgram
    /// (not a subtype — this codebase avoids table-per-hierarchy), adding
    /// itinerary/ratio/checklist fields. TransportRouteId optionally ties
    /// to E-601's Route for the "Module 23 ad-hoc trip" transport plan
    /// (doc's own §2 in-scope line) — external transport (chartered bus,
    /// no Route row) just leaves it null.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class ActivityTrip : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ProgramId { get; set; }

        public string ItineraryText { get; set; } = string.Empty;

        /// <summary>BR-ACT-004: students per one staff member, e.g. 10 for a 1:10 KG ratio.</summary>
        public int StaffRatioRequired { get; set; }

        public int AssignedStaffCount { get; set; }

        public int? TransportRouteId { get; set; }

        /// <summary>Auto-true when a Route is attached at definition time (an existing route is confirmed by construction); external/chartered transport needs an explicit confirmation call.</summary>
        public bool TransportConfirmed { get; set; }

        public bool DepartureChecklistComplete { get; set; }

        public bool ReturnHeadcountConfirmed { get; set; }
    }
}
