using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Infrastructure.Seeding;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The dashboard's panels are hard-wired in <see cref="DashboardPanels"/>; the
    /// registry that gates, orders and personalizes them is seeded in
    /// <see cref="WidgetRegistrySeedContributor"/>. The two are joined by a bare
    /// string code, and nothing else checks that join: a definition whose code does
    /// not match a panel governs nothing at all, silently — the panel keeps rendering
    /// for everyone, and the permission the school thought it applied does nothing.
    /// <para>
    /// These tests are that check, and the second one is a gate on new work: add a
    /// panel and you must either register it or say here why it cannot be registered
    /// yet.
    /// </para>
    /// </summary>
    public class DashboardWidgetRegistryTests
    {
        /// <summary>
        /// Panels that deliberately ship unregistered, because the module that owns
        /// them has no screens yet and therefore no permission to gate them with.
        /// Register them — and delete them from here — when those screens land.
        /// </summary>
        private static readonly HashSet<string> KnownUnregistered = new(StringComparer.OrdinalIgnoreCase)
        {
            DashboardPanels.Certificates,   // module 18 has no screens
            DashboardPanels.Restricted,     // module 24 has no screens
        };

        [Fact]
        public void Every_seeded_widget_registers_a_panel_that_exists()
        {
            var panels = DashboardPanels.All.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var code in WidgetRegistrySeedContributor.RegisteredPanelCodes)
            {
                Assert.True(panels.Contains(code), $"The seeder registers '{code}', which is not a dashboard panel — it would govern nothing.");
            }
        }

        [Fact]
        public void Every_panel_is_either_registered_or_a_stated_exception()
        {
            var registered = WidgetRegistrySeedContributor.RegisteredPanelCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var panel in DashboardPanels.All)
            {
                Assert.True(
                    registered.Contains(panel.Code) || KnownUnregistered.Contains(panel.Code),
                    $"Panel '{panel.Code}' is neither registered by WidgetRegistrySeedContributor nor listed as a stated exception, so it renders for every user regardless of permission (BR-DSH-001).");
            }
        }
    }
}
