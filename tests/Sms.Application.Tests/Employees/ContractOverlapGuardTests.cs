using System;
using Sms.Application.Employees;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Employees
{
    public class ContractOverlapGuardTests
    {
        [Fact]
        [BusinessRule("BR-EMP-003")]
        public void Fully_contained_range_overlaps()
        {
            Assert.True(ContractOverlapGuard.Overlaps(
                new DateTime(2026, 3, 1), new DateTime(2026, 5, 1),
                new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)));
        }

        [Fact]
        [BusinessRule("BR-EMP-003")]
        public void Touching_boundaries_do_not_overlap()
        {
            Assert.False(ContractOverlapGuard.Overlaps(
                new DateTime(2027, 1, 1), new DateTime(2027, 12, 31),
                new DateTime(2026, 1, 1), new DateTime(2027, 1, 1)));
        }

        [Fact]
        [BusinessRule("BR-EMP-003")]
        public void Disjoint_ranges_do_not_overlap()
        {
            Assert.False(ContractOverlapGuard.Overlaps(
                new DateTime(2028, 1, 1), new DateTime(2028, 12, 31),
                new DateTime(2026, 1, 1), new DateTime(2026, 12, 31)));
        }
    }
}
