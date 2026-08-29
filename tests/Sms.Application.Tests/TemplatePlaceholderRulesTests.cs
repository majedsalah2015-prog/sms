using System.Linq;
using Sms.Application.Notifications;
using Xunit;

namespace Sms.Application.Tests
{
    /// <summary>
    /// doc/Modules/33 §9's publish-time placeholder check — the one that stops a parent
    /// receiving the literal word "{Amount}".
    /// </summary>
    public class TemplatePlaceholderRulesTests
    {
        [Fact]
        public void A_placeholder_the_event_supplies_is_accepted()
            => Assert.Empty(TemplatePlaceholderRules.Unknown(
                "InstallmentOverdue", "Installment {InstallmentNo}", null, "{Amount} due {DueDate}", "..."));

        [Fact]
        public void A_placeholder_the_event_does_not_supply_is_named()
        {
            var unknown = TemplatePlaceholderRules.Unknown(
                "InstallmentOverdue", null, null, "Dear {ParentName}, {Amount} is due.", "...");

            Assert.Equal("ParentName", Assert.Single(unknown));
        }

        [Fact]
        public void The_check_is_case_sensitive_because_the_renderer_is()
        {
            // TemplateRenderer looks the key up in an ordinal dictionary: {dueDate} and
            // {DueDate} are two different tokens at send time, so treating them as one here
            // would bless a template that renders wrong.
            var unknown = TemplatePlaceholderRules.Unknown("InstallmentOverdue", null, null, "{dueDate}", "...");

            Assert.Equal("dueDate", Assert.Single(unknown));
        }

        [Fact]
        public void An_event_no_module_publishes_yet_is_not_validated()
        {
            // Writing the wording ahead of the module that will send it is legitimate; refusing
            // every placeholder on it would block that for no gain.
            Assert.Empty(TemplatePlaceholderRules.Unknown("CertificateIssued", null, null, "{Anything}", "..."));
            Assert.Empty(TemplatePlaceholderRules.Available("CertificateIssued"));
        }

        [Fact]
        public void The_available_keys_are_what_the_studio_offers()
        {
            var available = TemplatePlaceholderRules.Available("LibraryOverdue");

            Assert.Equal(new[] { "Barcode", "DaysOverdue" }, available.ToArray());
        }

        [Fact]
        public void Each_placeholder_is_listed_once_in_the_order_it_first_appears()
        {
            var used = TemplatePlaceholderRules.Used("{B} {A}", "{A} again");

            Assert.Equal(new[] { "B", "A" }, used.ToArray());
        }
    }
}
