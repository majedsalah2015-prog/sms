using System.Collections.Generic;
using Sms.Application.Notifications;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Notifications
{
    public class TemplateRendererTests
    {
        [Fact]
        [BusinessRule("BR-NOT-001")]
        public void Substitutes_every_placeholder_present_in_the_payload()
        {
            var payload = new Dictionary<string, string> { ["studentName"] = "Layla", ["date"] = "2026-08-15" };

            var result = TemplateRenderer.Render("{studentName} was absent on {date}.", payload);

            Assert.Equal("Layla was absent on 2026-08-15.", result);
        }

        [Fact]
        [BusinessRule("BR-NOT-001")]
        public void A_payload_key_missing_from_the_template_is_simply_unused()
        {
            var payload = new Dictionary<string, string> { ["studentName"] = "Layla", ["extra"] = "ignored" };

            var result = TemplateRenderer.Render("{studentName} was absent.", payload);

            Assert.Equal("Layla was absent.", result);
        }

        [Fact]
        [BusinessRule("BR-NOT-002")]
        public void A_template_token_missing_from_the_payload_is_left_visible_rather_than_throwing()
        {
            var payload = new Dictionary<string, string> { ["studentName"] = "Layla" };

            var result = TemplateRenderer.Render("{studentName} missed {subject}.", payload);

            Assert.Equal("Layla missed {subject}.", result);
        }
    }
}
