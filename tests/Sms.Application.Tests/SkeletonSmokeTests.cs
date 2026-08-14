using Xunit;

namespace Sms.Application.Tests
{
    public class SkeletonSmokeTests
    {
        [Fact]
        public void Application_assembly_is_reachable()
        {
            Assert.NotNull(typeof(Sms.Application.AssemblyMarker).Assembly);
        }
    }
}
