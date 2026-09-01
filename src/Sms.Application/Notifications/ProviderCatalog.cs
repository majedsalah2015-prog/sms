using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Notifications;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// The gateways this deployment knows how to talk to, and what each one needs typed
    /// into the provider console (doc/Modules/33 §8.3, BR-NTF-003).
    /// <para>
    /// A closed list, on purpose. <c>Provider.ProviderCode</c> is matched against a
    /// registered <c>IChannelSender</c> at dispatch, so a school that invents a code
    /// registers a gateway that will never be reached and finds out one absent parent
    /// notification at a time. The console offers these and nothing else.
    /// </para>
    /// <para>
    /// The two entries share a sender: 360dialog's WhatsApp API is a hosted
    /// <em>WhatsApp Business Cloud</em> endpoint and Twilio's is Twilio's own, but both
    /// are HTTP form-posts with basic auth and a message id back, which is all
    /// <c>TwilioStyleChannelSender</c> assumes. What differs — the base URL, what the
    /// account identifier is called, whether the sender needs a <c>whatsapp:</c> prefix —
    /// is data, and lives here.
    /// </para>
    /// </summary>
    public static class ProviderCatalog
    {
        public const string Twilio = "TWILIO";

        public const string Dialog360 = "360DIALOG";

        /// <summary>
        /// One registrable gateway. <paramref name="AccountLabelEn"/> names the non-secret
        /// half in the vendor's own words, because a Sys Admin is copying it off that
        /// vendor's console and "Account identifier" is not what it says there.
        /// </summary>
        public sealed record GatewayDefinition(
            string Code,
            string NameEn,
            string NameAr,
            IReadOnlyList<NotificationChannel> Channels,
            string DefaultBaseUrl,
            string AccountLabelEn,
            string AccountLabelAr,
            string SecretLabelEn,
            string SecretLabelAr,
            string SenderLabelEn,
            string SenderLabelAr);

        private static readonly GatewayDefinition[] All =
        {
            new(
                Twilio,
                "Twilio",
                "تويليو",
                new[] { NotificationChannel.WhatsApp, NotificationChannel.Sms },
                "https://api.twilio.com",
                "Account SID", "معرّف الحساب (Account SID)",
                "Auth token", "رمز المصادقة (Auth Token)",
                "Sender number (E.164)", "رقم المُرسِل (بصيغة E.164)"),

            new(
                Dialog360,
                "360dialog",
                "360dialog",
                new[] { NotificationChannel.WhatsApp },
                "https://waba-v2.360dialog.io",
                "Channel ID", "معرّف القناة (Channel ID)",
                "API key", "مفتاح الواجهة (API Key)",
                "Sender number (E.164)", "رقم المُرسِل (بصيغة E.164)"),
        };

        public static IReadOnlyList<GatewayDefinition> Gateways => All;

        /// <summary>The gateways that can serve <paramref name="channel"/> — what the console's picker offers once a channel is chosen.</summary>
        public static IReadOnlyList<GatewayDefinition> For(NotificationChannel channel)
            => All.Where(g => g.Channels.Contains(channel)).ToList();

        public static GatewayDefinition? Find(string? code)
            => code == null ? null : All.FirstOrDefault(g => string.Equals(g.Code, code, StringComparison.OrdinalIgnoreCase));

        /// <summary>Whether this deployment has a sender behind <paramref name="code"/> at all. The console refuses anything else rather than storing a code nothing answers to.</summary>
        public static bool IsKnown(string? code) => Find(code) != null;
    }
}
