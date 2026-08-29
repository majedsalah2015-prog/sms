using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Notifications;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Notifications
{
    /// <summary>
    /// The real WhatsApp and SMS transport, over an official intermediary's HTTP API
    /// (BR-NOT-009, BR-NTF-003, doc/Modules/33 §8.3).
    /// <para>
    /// One class for two channels and two gateways because they are one shape: a form
    /// post with basic auth, a <c>To</c>, a <c>From</c> and a <c>Body</c>, and a message
    /// id back. What differs between Twilio and 360dialog — the base URL, the path, the
    /// <c>whatsapp:</c> prefix — is data on the <see cref="Provider"/> row and in
    /// <see cref="ProviderCatalog"/>, so registering the second gateway needs a console
    /// entry rather than a class.
    /// </para>
    /// <para>
    /// <b>It never throws for a bad send.</b> <see cref="IChannelSender"/> says so and
    /// the dispatcher depends on it: one unreachable number, one rejected token or one
    /// gateway outage must fail its own delivery and leave the rest of the batch to run.
    /// Every failure path below returns <see cref="ChannelSendOutcome.Failure"/> with a
    /// reason the operations screen can show, including the ones that are really
    /// configuration mistakes — those say so in the reason rather than raising, because
    /// a school finds out from the failure queue either way.
    /// </para>
    /// <para>
    /// <b>What is deliberately not here.</b> WhatsApp's 24-hour session window and
    /// pre-approved message templates: outside a conversation started by the parent,
    /// Meta only delivers a template registered in the Business Manager, and this sender
    /// posts free-form text. That is correct for SMS and for a WhatsApp reply inside the
    /// window, and will be rejected by the gateway outside it — visibly, in the failure
    /// queue, with the gateway's own words. Registering and naming approved templates is
    /// its own slice; see the notes in the provider console view.
    /// </para>
    /// </summary>
    public class TwilioStyleChannelSender : IChannelSender
    {
        private readonly AppDbContext _db;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISecretProtector _protector;

        public TwilioStyleChannelSender(
            NotificationChannel channel,
            AppDbContext db,
            IHttpClientFactory httpClientFactory,
            ISecretProtector protector)
        {
            Channel = channel;
            _db = db;
            _httpClientFactory = httpClientFactory;
            _protector = protector;
        }

        public NotificationChannel Channel { get; }

        public async Task<ChannelSendOutcome> SendAsync(Delivery delivery, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(delivery.RecipientAddress))
            {
                // BR-NTF-005's data-quality case: the address book had nothing when this was
                // queued. Retrying cannot help — a registrar has to fix the contact record.
                return ChannelSendOutcome.Failure("No address on file for this recipient.");
            }

            var provider = await ActiveProviderAsync(cancellationToken);
            if (provider == null)
            {
                return ChannelSendOutcome.Failure($"No configured, active gateway is registered for {Channel}.");
            }

            var token = _protector.Unprotect(provider.SecretCipher);
            if (string.IsNullOrWhiteSpace(token))
            {
                // Either nothing was ever entered, or the key ring that sealed it is gone.
                // Both need the token re-entered; neither is worth a retry.
                return ChannelSendOutcome.Failure(
                    $"The stored credentials for '{provider.DisplayName}' could not be read — re-enter the token in the provider console.");
            }

            return await PostAsync(provider, token!, delivery.RecipientAddress!, MessageBody(delivery), cancellationToken);
        }

        /// <summary>
        /// BR-NTF-003's verify action. Sends nothing: it asks the gateway to describe the
        /// account, which proves the credentials without spending a message or waking a
        /// parent. A 401/403 is a wrong token, a 404 is a wrong account identifier, and
        /// both come back as the sentence the console shows.
        /// </summary>
        public async Task<(bool Passed, string Detail)> VerifyAsync(Provider provider, CancellationToken cancellationToken = default)
        {
            var token = _protector.Unprotect(provider.SecretCipher);
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, "No readable credentials are stored for this gateway.");
            }

            using var client = Client(provider, token!);
            try
            {
                using var response = await client.GetAsync(VerifyPath(provider), cancellationToken);
                return response.IsSuccessStatusCode
                    ? (true, $"HTTP {(int)response.StatusCode}")
                    : (false, await DescribeAsync(response, cancellationToken));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                return (false, ex.Message);
            }
        }

        private async Task<ChannelSendOutcome> PostAsync(
            Provider provider, string token, string to, string body, CancellationToken cancellationToken)
        {
            using var client = Client(provider, token);

            // KeyValuePair<string?, string?> because that is what FormUrlEncodedContent takes
            // under nullable reference types; a Dictionary<string, string> is not assignable to it.
            var form = new List<KeyValuePair<string?, string?>>
            {
                new("To", Address(provider, to)),
                new("From", Address(provider, provider.SenderId!)),
                new("Body", body),
            };

            try
            {
                using var content = new FormUrlEncodedContent(form);
                using var response = await client.PostAsync(SendPath(provider), content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return ChannelSendOutcome.Failure(await DescribeAsync(response, cancellationToken));
                }

                var payload = await response.Content.ReadAsStringAsync();
                return ChannelSendOutcome.Success(MessageId(payload) ?? $"{provider.ProviderCode}:accepted");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // A timeout or a DNS failure is the gateway being unreachable, not the message
                // being wrong: this is the case the dispatcher's three attempts exist for.
                return ChannelSendOutcome.Failure(ex.Message);
            }
        }

        /// <summary>
        /// The gateway to use: active, configured, on this channel, lowest failover order
        /// first (BR-NTF-003). Read past the soft-active filter with the school predicate
        /// restored by hand — the same reason <c>NotificationConfigAdmin</c> does it, and
        /// here also because the dispatcher may run on a background thread whose ambient
        /// tenant is the job's rather than a request's.
        /// </summary>
        private async Task<Provider?> ActiveProviderAsync(CancellationToken cancellationToken)
        {
            var candidates = await _db.Providers
                .IgnoreQueryFilters()
                .Where(p => p.SchoolId == _db.CurrentSchoolId && p.Channel == Channel && p.IsActive)
                .OrderBy(p => p.FailoverOrder)
                .ToListAsync(cancellationToken);

            return candidates.FirstOrDefault(p => p.IsConfigured && ProviderCatalog.IsKnown(p.ProviderCode));
        }

        private HttpClient Client(Provider provider, string token)
        {
            var client = _httpClientFactory.CreateClient("notifications");
            client.BaseAddress = new Uri(BaseUrl(provider));
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{provider.AccountIdentifier}:{token}")));
            return client;
        }

        private static string BaseUrl(Provider provider)
            => string.IsNullOrWhiteSpace(provider.ApiBaseUrl)
                ? ProviderCatalog.Find(provider.ProviderCode)?.DefaultBaseUrl ?? "https://api.twilio.com"
                : provider.ApiBaseUrl!.TrimEnd('/');

        private static string SendPath(Provider provider)
            => string.Equals(provider.ProviderCode, ProviderCatalog.Dialog360, StringComparison.OrdinalIgnoreCase)
                ? "/messages"
                : $"/2010-04-01/Accounts/{provider.AccountIdentifier}/Messages.json";

        /// <summary>A read-only endpoint on each gateway that answers only for good credentials.</summary>
        private static string VerifyPath(Provider provider)
            => string.Equals(provider.ProviderCode, ProviderCatalog.Dialog360, StringComparison.OrdinalIgnoreCase)
                ? "/health"
                : $"/2010-04-01/Accounts/{provider.AccountIdentifier}.json";

        /// <summary>WhatsApp addresses carry the channel prefix the API distinguishes them by; SMS numbers are bare E.164.</summary>
        private string Address(Provider provider, string number)
        {
            if (Channel != NotificationChannel.WhatsApp
                || string.Equals(provider.ProviderCode, ProviderCatalog.Dialog360, StringComparison.OrdinalIgnoreCase))
            {
                return number;
            }

            return number.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase) ? number : "whatsapp:" + number;
        }

        /// <summary>
        /// Subject and body joined for a channel that has no subject line. An event whose
        /// template left the subject empty must not lead with a blank line.
        /// </summary>
        private static string MessageBody(Delivery delivery)
            => string.IsNullOrWhiteSpace(delivery.RenderedSubject)
                ? delivery.RenderedBody
                : delivery.RenderedSubject + "\n\n" + delivery.RenderedBody;

        private static async Task<string> DescribeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var status = (int)response.StatusCode;
            string detail;
            try
            {
                detail = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                detail = string.Empty;
            }

            // The column is 500 wide and a gateway's error page is not. Keep the front of it:
            // the code and the message are always at the start of a JSON error body.
            if (detail.Length > 300)
            {
                detail = detail.Substring(0, 300);
            }

            return string.IsNullOrWhiteSpace(detail)
                ? $"Gateway returned HTTP {status.ToString(CultureInfo.InvariantCulture)}."
                : $"HTTP {status.ToString(CultureInfo.InvariantCulture)}: {detail}";
        }

        /// <summary>
        /// The provider's own message id, pulled out of the JSON without a serializer — the
        /// two gateways name it "sid" and "id", both as a plain string, and taking a
        /// dependency on a full model of a response we otherwise ignore would be more to
        /// maintain than the substring.
        /// </summary>
        private static string? MessageId(string payload)
        {
            foreach (var key in new[] { "\"sid\":\"", "\"id\":\"" })
            {
                var start = payload.IndexOf(key, StringComparison.Ordinal);
                if (start < 0)
                {
                    continue;
                }

                start += key.Length;
                var end = payload.IndexOf('"', start);
                if (end > start)
                {
                    return payload.Substring(start, end - start);
                }
            }

            return null;
        }
    }
}
