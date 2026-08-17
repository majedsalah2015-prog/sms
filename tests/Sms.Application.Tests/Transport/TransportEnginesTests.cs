using System;
using System.Linq;
using Sms.Application.Transport;
using Sms.Domain.Transport;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Transport
{
    public class TransportEnginesTests
    {
        private static readonly DateTime Today = new(2026, 10, 1);

        [Fact]
        [BusinessRule("BR-TRN-001")]
        public void A_bus_is_roadworthy_only_with_every_mandatory_document_unexpired()
        {
            var docs = new[]
            {
                new RoadworthinessEvaluator.DocumentInput(BusDocumentKind.Registration, Today.AddYears(1)),
                new RoadworthinessEvaluator.DocumentInput(BusDocumentKind.Insurance, Today.AddDays(-1)),   // expired
                new RoadworthinessEvaluator.DocumentInput(BusDocumentKind.Insurance, Today.AddMonths(6)),  // renewed - latest wins
            };

            var blockers = RoadworthinessEvaluator.Blockers(docs, Today);

            Assert.Equal(new[] { BusDocumentKind.SafetyInspection }, blockers);
            Assert.False(RoadworthinessEvaluator.IsRoadworthy(docs, Today));
        }

        [Fact]
        [BusinessRule("BR-TRN-002")]
        public void Driver_needs_an_unexpired_licence_of_at_least_the_required_class()
        {
            Assert.True(DriverEligibilityEvaluator.CanDrive(LicenseClass.Heavy, Today.AddDays(1), LicenseClass.Medium, Today));
            Assert.False(DriverEligibilityEvaluator.CanDrive(LicenseClass.Light, Today.AddDays(1), LicenseClass.Medium, Today));
            Assert.False(DriverEligibilityEvaluator.CanDrive(LicenseClass.Heavy, Today.AddDays(-1), LicenseClass.Medium, Today));
            Assert.False(DriverEligibilityEvaluator.CanDrive(null, null, LicenseClass.Light, Today));
        }

        [Fact]
        [BusinessRule("BR-TRN-003")]
        public void Capacity_is_a_hard_check_and_stop_times_must_be_sequential()
        {
            Assert.True(RouteCapacityEvaluator.HasSeat(29, 30));
            Assert.False(RouteCapacityEvaluator.HasSeat(30, 30));
            Assert.True(StopSequenceValidator.AreSequential(new[] { new TimeSpan(6, 30, 0), new TimeSpan(6, 45, 0), new TimeSpan(7, 0, 0) }));
            Assert.False(StopSequenceValidator.AreSequential(new[] { new TimeSpan(6, 30, 0), new TimeSpan(6, 30, 0) }));
        }

        [Fact]
        [BusinessRule("BR-TRN-005")]
        public void A_trip_closes_only_when_every_roster_student_is_resolved_and_the_sweep_is_confirmed()
        {
            var roster = new[]
            {
                new TripCloseEvaluator.StudentEvents(1, new[] { TripLogEvent.Boarded, TripLogEvent.Alighted }),
                new TripCloseEvaluator.StudentEvents(2, new[] { TripLogEvent.AbsentDeclared }),
                new TripCloseEvaluator.StudentEvents(3, new[] { TripLogEvent.Boarded }),   // still on the bus
            };

            Assert.Equal(new[] { 3 }, TripCloseEvaluator.UnresolvedStudents(roster));
            Assert.False(TripCloseEvaluator.CanClose(roster, sweepConfirmed: true));
            Assert.False(TripCloseEvaluator.CanClose(roster.Take(2).ToList(), sweepConfirmed: false));
            Assert.True(TripCloseEvaluator.CanClose(roster.Take(2).ToList(), sweepConfirmed: true));
        }

        [Fact]
        [BusinessRule("BR-TRN-006")]
        public void Handover_policy_accepts_self_release_or_a_pickup_authorized_receiver()
        {
            Assert.True(HandoverPolicy.IsAcceptable(handoverRequired: false, selfReleaseAllowed: false, receiverIsPickupAuthorized: false));
            Assert.True(HandoverPolicy.IsAcceptable(true, selfReleaseAllowed: true, false));
            Assert.True(HandoverPolicy.IsAcceptable(true, false, receiverIsPickupAuthorized: true));
            Assert.False(HandoverPolicy.IsAcceptable(true, false, false));
        }
    }
}
