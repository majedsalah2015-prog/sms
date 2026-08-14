using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Sms.ArchitectureTests
{
    /// <summary>
    /// Enforces the Clean Architecture dependency directions of ADR-1
    /// (docs/02-System-Architecture.md §2). These tests are a CI gate:
    /// a module may never re-introduce a forbidden layer dependency.
    /// </summary>
    public class LayerDependencyTests
    {
        private static readonly Assembly Domain = typeof(Sms.Domain.AssemblyMarker).Assembly;
        private static readonly Assembly Application = typeof(Sms.Application.AssemblyMarker).Assembly;
        private static readonly Assembly Infrastructure = typeof(Sms.Infrastructure.AssemblyMarker).Assembly;

        [Fact]
        public void Domain_depends_on_nothing_in_outer_layers()
        {
            var result = Types.InAssembly(Domain)
                .ShouldNot()
                .HaveDependencyOnAny("Sms.Application", "Sms.Infrastructure", "Sms.Web")
                .GetResult();

            Assert.True(result.IsSuccessful, Failing(result));
        }

        [Fact]
        public void Application_does_not_depend_on_infrastructure_or_web()
        {
            var result = Types.InAssembly(Application)
                .ShouldNot()
                .HaveDependencyOnAny("Sms.Infrastructure", "Sms.Web")
                .GetResult();

            Assert.True(result.IsSuccessful, Failing(result));
        }

        [Fact]
        public void Infrastructure_does_not_depend_on_web()
        {
            var result = Types.InAssembly(Infrastructure)
                .ShouldNot()
                .HaveDependencyOnAny("Sms.Web")
                .GetResult();

            Assert.True(result.IsSuccessful, Failing(result));
        }

        private static string Failing(TestResult result)
        {
            if (result.IsSuccessful || result.FailingTypeNames == null)
            {
                return string.Empty;
            }

            return "Offending types: " + string.Join(", ", result.FailingTypeNames);
        }
    }
}
