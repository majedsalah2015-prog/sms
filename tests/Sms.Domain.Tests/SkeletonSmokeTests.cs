using Xunit;

namespace Sms.Domain.Tests
{
    public class SkeletonSmokeTests
    {
        [Fact]
        public void Domain_assembly_is_reachable()
        {
            Assert.NotNull(typeof(Sms.Domain.AssemblyMarker).Assembly);
        }
    }
}
