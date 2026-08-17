using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;
using Sms.Domain.Grades;

namespace Sms.Domain.Activities
{
    /// <summary>
    /// ppl.Program (doc/Modules/29 §7, BR-ACT-001): a term-scoped activity
    /// instance — club/sport/competition/trip/event. Named
    /// <c>ActivityProgram</c>, not <c>Program</c>, because "Program" is
    /// also the ASP.NET entry-point class name in Sms.Web and Sms.Seeder
    /// (`Sms.Web.Program`, `Sms.Seeder.Program`) — same class of
    /// namespace-vs-type collision as `Sms.Domain.Admissions.Application`
    /// vs the `Sms.Application` project root (E-201), avoided here by
    /// renaming the entity outright instead of aliasing every call site.
    /// A single recurring weekly slot (DayOfWeek/StartTime/EndTime, all
    /// nullable) stands in for the doc's "schedule slots" plural — most
    /// clubs meet on one fixed slot; multi-slot programs are a
    /// straightforward follow-up, not modeled here. Venue-availability/
    /// timetable-conflict surfacing (BR-ACT-001's other half) needs
    /// deeper Room/Timetable integration and is deferred — VenueRoomId is
    /// recorded but not conflict-checked.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class ActivityProgram : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int TermId { get; set; }

        public int ActivityTypeId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public int SupervisorEmployeeId { get; set; }

        public int? VenueRoomId { get; set; }

        public int Capacity { get; set; }

        /// <summary>BR-GRD-004 policy inheritance — null = no gender restriction beyond the stage default.</summary>
        public GenderPolicy? EligibilityGenderPolicy { get; set; }

        public int? EligibilityStageId { get; set; }

        /// <summary>Null = free program (BR-ACT-007: free programs never generate finance records).</summary>
        public decimal? CostAmount { get; set; }

        /// <summary>Required when CostAmount is set — which Fees category (E-303) the enrollment charge posts against.</summary>
        public int? FeeCategoryId { get; set; }

        /// <summary>BR-ACT-005: trips are always true; term clubs configurable.</summary>
        public bool RequiresConsent { get; set; }

        public DayOfWeek? DayOfWeek { get; set; }

        public TimeSpan? StartTime { get; set; }

        public TimeSpan? EndTime { get; set; }

        public ProgramStatus Status { get; set; } = ProgramStatus.Proposed;
    }
}
