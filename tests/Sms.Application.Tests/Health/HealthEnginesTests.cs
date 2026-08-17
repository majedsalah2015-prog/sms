using System;
using System.Linq;
using Sms.Application.Health;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Health
{
    public class HealthEnginesTests
    {
        [Fact]
        [BusinessRule("BR-HLT-004")]
        public void Vaccination_due_state_follows_age_against_the_schedule()
        {
            var dob = new DateTime(2020, 1, 1);
            var schedule = new[]
            {
                new VaccinationDueEvaluator.ScheduleEntry("MMR", 1, 12), new VaccinationDueEvaluator.ScheduleEntry("MMR", 2, 18), new VaccinationDueEvaluator.ScheduleEntry("HPV", 1, 132),
            };
            var given = new[] { new VaccinationDueEvaluator.GivenDose("MMR", 1) };

            var status = VaccinationDueEvaluator.Evaluate(dob, asOf: new DateTime(2021, 7, 15), schedule, given, graceDays: 30);

            Assert.Equal(VaccinationDueEvaluator.DoseState.Given, status.Single(s => s.VaccineCode == "MMR" && s.DoseNumber == 1).State);
            Assert.Equal(VaccinationDueEvaluator.DoseState.Due, status.Single(s => s.VaccineCode == "MMR" && s.DoseNumber == 2).State);   // due 2021-07-01, within grace
            Assert.Equal(VaccinationDueEvaluator.DoseState.NotYetDue, status.Single(s => s.VaccineCode == "HPV").State);
            Assert.Equal(VaccinationDueEvaluator.DoseState.Overdue, VaccinationDueEvaluator.Evaluate(dob, new DateTime(2021, 9, 1), schedule, given).Single(s => s.DoseNumber == 2 && s.VaccineCode == "MMR").State);
        }

        [Fact]
        [BusinessRule("BR-HLT-006")]
        public void Administration_is_a_deviation_outside_window_schedule_or_dosage()
        {
            var start = new DateTime(2026, 10, 1);
            var end = new DateTime(2026, 10, 31);

            Assert.False(MedicationAdministrationPolicy.IsDeviation(new DateTime(2026, 10, 5, 10, 10, 0), start, end, "10:00,14:00", 5m, 5m));
            Assert.True(MedicationAdministrationPolicy.IsDeviation(new DateTime(2026, 10, 5, 12, 0, 0), start, end, "10:00,14:00", 5m, 5m));   // off schedule
            Assert.True(MedicationAdministrationPolicy.IsDeviation(new DateTime(2026, 10, 5, 10, 0, 0), start, end, "10:00,14:00", 10m, 5m));  // double dose
            Assert.True(MedicationAdministrationPolicy.IsDeviation(new DateTime(2026, 11, 1, 10, 0, 0), start, end, "10:00,14:00", 5m, 5m));   // after end
        }

        [Fact]
        [BusinessRule("BR-HLT-005")]
        public void Sent_home_needs_verified_pickup_or_a_documented_exception()
        {
            Assert.True(SentHomePolicy.IsAcceptable(pickupVerified: true, exceptionNote: null));
            Assert.True(SentHomePolicy.IsAcceptable(false, "ambulance transfer, parent en route"));
            Assert.False(SentHomePolicy.IsAcceptable(false, "  "));
        }

        [Fact]
        [BusinessRule("BR-HLT-008")]
        public void Screening_stats_are_counts_only()
        {
            var stats = ScreeningStatsCalculator.Compute(new[] { (true, true, false), (false, false, false), (true, true, true), (false, false, false) });

            Assert.Equal(4, stats.Screened);
            Assert.Equal(2, stats.Abnormal);
            Assert.Equal(2, stats.Referred);
            Assert.Equal(1, stats.FollowedUp);
            Assert.Equal(50m, stats.AbnormalRate);
        }
    }
}
