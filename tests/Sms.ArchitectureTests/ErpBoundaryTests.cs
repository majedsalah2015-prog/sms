using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Sms.ArchitectureTests
{
    /// <summary>
    /// Enforces the boundary the embedded-accounting design rests on
    /// (docs/Integration/01-Embedded-Accounting-Plan.md §3): the school's own
    /// layers must not know the ERP exists, and the bridge that does know it
    /// must reach the ERP only through published contracts.
    /// <para>
    /// The value of the whole arrangement is that removing
    /// <c>Sms.Erp.Bridge</c> leaves a standalone school system. That stays true
    /// only while these three tests pass: one reference from
    /// <c>Sms.Application</c> to an ERP type would make the school unbuildable
    /// without the submodule, quietly and permanently.
    /// </para>
    /// </summary>
    public class ErpBoundaryTests
    {
        private const string ErpNamespaceRoot = "ERP2028";

        private static readonly Assembly Domain = typeof(Sms.Domain.AssemblyMarker).Assembly;
        private static readonly Assembly Application = typeof(Sms.Application.AssemblyMarker).Assembly;
        private static readonly Assembly Infrastructure = typeof(Sms.Infrastructure.AssemblyMarker).Assembly;
        private static readonly Assembly Bridge = typeof(Sms.Erp.Bridge.AssemblyMarker).Assembly;

        [Theory]
        [InlineData(nameof(Domain))]
        [InlineData(nameof(Application))]
        [InlineData(nameof(Infrastructure))]
        public void School_layers_do_not_depend_on_the_ERP(string layer)
        {
            var assembly = layer switch
            {
                nameof(Domain) => Domain,
                nameof(Application) => Application,
                _ => Infrastructure,
            };

            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOn(ErpNamespaceRoot)
                .GetResult();

            Assert.True(result.IsSuccessful, Failing(result));
        }

        /// <summary>
        /// The bridge may see <c>Accounting.Contracts</c> and nothing else of
        /// the module — the same rule the ERP imposes on its own modules
        /// (its docs/Architecture/05-Dependency-Rules.md §3). Reaching into
        /// <c>.Application</c> or <c>.Infrastructure</c> for "just one service"
        /// is how a contract seam turns into a coupling.
        /// </summary>
        [Fact]
        public void Bridge_reaches_the_ERP_only_through_contracts()
        {
            var result = Types.InAssembly(Bridge)
                .ShouldNot()
                .HaveDependencyOnAny(
                    "ERP2028.Modules.Accounting.Application",
                    "ERP2028.Modules.Accounting.Infrastructure",
                    "ERP2028.Modules.Accounting.Domain",
                    "ERP2028.Modules.Accounting.Web",
                    "ERP2028.Modules.Organization.Application",
                    "ERP2028.Modules.Organization.Infrastructure",
                    "ERP2028.Modules.Organization.Domain",
                    "ERP2028.Modules.Organization.Web")
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
