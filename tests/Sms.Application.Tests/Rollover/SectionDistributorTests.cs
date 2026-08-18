using System.Linq;
using Sms.Application.Rollover;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Rollover
{
    public class SectionDistributorTests
    {
        [Fact]
        [BusinessRule("BR-SCN-008")]
        public void Balances_size_by_filling_the_least_filled_section_first()
        {
            var sections = new[]
            {
                new DistributionSection(1, capacity: 30, currentCount: 5, GenderPolicy.Mixed),
                new DistributionSection(2, capacity: 30, currentCount: 0, GenderPolicy.Mixed),
            };
            var candidates = Enumerable.Range(100, 9).Select(id => new DistributionCandidate(id, Gender.Male));

            var result = SectionDistributor.Distribute(candidates, sections);

            Assert.Empty(result.UnplacedStudentIds);
            // 5+0 → after 9: section 2 catches up (5 go there first), then alternate → 7 / 7
            Assert.Equal(2, result.Assignments.Values.Count(s => s == 1));
            Assert.Equal(7, result.Assignments.Values.Count(s => s == 2));
        }

        [Fact]
        [BusinessRule("BR-SCN-002")]
        public void Never_exceeds_capacity_and_reports_the_unplaced()
        {
            var sections = new[] { new DistributionSection(1, capacity: 3, currentCount: 2, GenderPolicy.Mixed) };
            var candidates = new[] { new DistributionCandidate(1, Gender.Male), new DistributionCandidate(2, Gender.Female) };

            var result = SectionDistributor.Distribute(candidates, sections);

            Assert.Single(result.Assignments);
            Assert.Equal(new[] { 2 }, result.UnplacedStudentIds);
        }

        [Fact]
        [BusinessRule("BR-GRD-004")]
        public void Respects_section_gender_policy()
        {
            var sections = new[]
            {
                new DistributionSection(1, 30, 0, GenderPolicy.Boys),
                new DistributionSection(2, 30, 0, GenderPolicy.Girls),
            };
            var candidates = new[]
            {
                new DistributionCandidate(1, Gender.Male), new DistributionCandidate(2, Gender.Female),
                new DistributionCandidate(3, Gender.Female),
            };

            var result = SectionDistributor.Distribute(candidates, sections);

            Assert.Equal(1, result.Assignments[1]);
            Assert.Equal(2, result.Assignments[2]);
            Assert.Equal(2, result.Assignments[3]);
        }

        [Fact]
        public void Is_deterministic_across_runs()
        {
            var sections = new[] { new DistributionSection(1, 30, 0, GenderPolicy.Mixed), new DistributionSection(2, 30, 0, GenderPolicy.Mixed) };
            var candidates = new[] { 5, 3, 9, 1 }.Select(id => new DistributionCandidate(id, Gender.Male)).ToList();

            var a = SectionDistributor.Distribute(candidates, sections).Assignments;
            var b = SectionDistributor.Distribute(candidates.AsEnumerable().Reverse(), sections).Assignments;

            Assert.Equal(a.OrderBy(k => k.Key), b.OrderBy(k => k.Key));
        }
    }
}
