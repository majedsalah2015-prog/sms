using System.Linq;
using Sms.Infrastructure.Seeding;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// The seed run's order, where getting it wrong is silent.
    /// <para>
    /// Most contributors fail loudly if their dependency is missing. The school-scoped ones do
    /// not: they guard on "is there a school yet" and return quietly, so a contributor ordered
    /// before <c>DemoSeedContributor</c> logs as seeded and writes nothing. That is how
    /// <c>msg.SubscriptionRule</c> stayed empty on every deployment while the seeder reported
    /// success. This test is the thing that would have caught it.
    /// </para>
    /// </summary>
    public class SeedOrderTests
    {
        /// <summary>Reflection rather than instances: constructing these needs a DbContext and every admin port they compose, and the only thing under test is a constant.</summary>
        private static int OrderOf<T>() => (int)typeof(T)
            .GetProperty(nameof(Sms.Application.Seeding.ISeedContributor.Order))!
            .GetValue(System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(T)))!;

        [Fact]
        public void Notification_defaults_and_templates_run_after_the_school_exists()
        {
            var demo = OrderOf<DemoSeedContributor>();

            // Both guard on core.School, which DemoSeedContributor creates. Ordered before it,
            // they no-op in silence — see the class summary.
            Assert.True(OrderOf<NotificationDefaultsSeedContributor>() > demo,
                "Subscription rules must seed after the school exists, or the contributor writes nothing.");
            Assert.True(OrderOf<NotificationTemplateSeedContributor>() > demo,
                "Templates must seed after the school exists, or the contributor writes nothing.");
        }

        [Fact]
        public void Collection_accounts_run_after_the_school_exists()
        {
            // These two were the tail of DemoSeedContributor's own method, behind its early
            // return, so they never reached a database that already had a school — the cashier's
            // destination picker was empty on every one of them. Their own contributor now, and
            // ordered after the school it scopes them to, or the move fixes nothing.
            Assert.True(
                OrderOf<CollectionAccountDemoSeedContributor>() > OrderOf<DemoSeedContributor>(),
                "Collection accounts must seed after the school exists, or the contributor writes nothing.");
        }

        [Fact]
        public void Templates_run_after_the_rules_they_provide_wording_for()
        {
            // Not a hard dependency — neither reads the other — but a template with no rule is
            // never consulted, and the run log should read in the order a person expects.
            Assert.True(
                OrderOf<NotificationTemplateSeedContributor>() > OrderOf<NotificationDefaultsSeedContributor>(),
                "Wording should follow the subscriptions it serves.");
        }
    }
}
