using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Notifications;
using Sms.Application.Setup;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Notifications
{
    /// <summary>doc/Modules/33 (Notifications Administration) — standalone admin operations, save themselves, no larger transaction to ride.</summary>
    public class NotificationOpsAdmin : INotificationOpsAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly ISecretProtector _protector;
        private readonly ISystemSetupAdmin _settings;
        private readonly IRecipientAddressBook _addresses;
        private readonly IEnumerable<IChannelSender> _senders;

        public NotificationOpsAdmin(
            AppDbContext db,
            IClock clock,
            ISecretProtector protector,
            ISystemSetupAdmin settings,
            IRecipientAddressBook addresses,
            IEnumerable<IChannelSender> senders)
        {
            _db = db;
            _clock = clock;
            _protector = protector;
            _settings = settings;
            _addresses = addresses;
            _senders = senders;
        }

        // ------------------------------------------------------------------ templates

        public async Task MarkTemplateVersionTestSentAsync(int templateVersionId, CancellationToken cancellationToken = default)
        {
            var version = await _db.TemplateVersions.SingleAsync(v => v.Id == templateVersionId, cancellationToken);
            if (!TemplatePublishTransitions.CanTransition(version.PublishStatus, TemplatePublishStatus.TestSent))
            {
                throw new InvalidTemplatePublishTransitionException(version.PublishStatus, TemplatePublishStatus.TestSent);
            }

            version.PublishStatus = TemplatePublishStatus.TestSent;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task PublishTemplateVersionAsync(int templateVersionId, CancellationToken cancellationToken = default)
        {
            var version = await _db.TemplateVersions.SingleAsync(v => v.Id == templateVersionId, cancellationToken);
            if (!TemplatePublishTransitions.CanTransition(version.PublishStatus, TemplatePublishStatus.Published))
            {
                throw new InvalidTemplatePublishTransitionException(version.PublishStatus, TemplatePublishStatus.Published);
            }

            version.PublishStatus = TemplatePublishStatus.Published;

            // Past the filter: publishing a version of a template a school retired should
            // revive it, not fail on a row the plain query cannot see.
            var template = await _db.Templates
                .IgnoreQueryFilters()
                .SingleAsync(t => t.Id == version.TemplateId && t.SchoolId == _db.CurrentSchoolId, cancellationToken);
            template.CurrentVersionNumber = version.VersionNumber;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> TestSendTemplateVersionAsync(
            int templateVersionId, int recipientUserId, CancellationToken cancellationToken = default)
        {
            var version = await _db.TemplateVersions.SingleOrDefaultAsync(v => v.Id == templateVersionId, cancellationToken)
                          ?? throw new UnknownTemplateException(templateVersionId);

            var template = await _db.Templates
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(t => t.Id == version.TemplateId && t.SchoolId == _db.CurrentSchoolId, cancellationToken)
                          ?? throw new UnknownTemplateException(version.TemplateId);

            // A test send proves the channel end to end, so it needs a real address on that
            // channel. Refusing here — rather than queueing a delivery that will fail — is the
            // difference between "your own number is not on file" and a mystery in the log.
            string? address = null;
            if (template.Channel != NotificationChannel.InApp)
            {
                var resolved = await _addresses.ResolveAsync(new[] { recipientUserId }, template.Channel, cancellationToken);
                if (!resolved.TryGetValue(recipientUserId, out address))
                {
                    throw new RecipientUnreachableException(recipientUserId, template.Channel);
                }
            }

            // Placeholders are filled with their own names in braces-free form rather than left
            // as tokens: a tester needs to see where each value lands, and a literal "{Amount}"
            // in a WhatsApp message is exactly what this send exists to catch.
            var sample = TemplatePlaceholderRules
                .Available(template.EventCode)
                .ToDictionary(key => key, key => $"[{key}]", StringComparer.Ordinal);

            var delivery = new Delivery
            {
                EventCode = template.EventCode,
                Channel = template.Channel,
                RecipientUserId = recipientUserId,
                TemplateVersionId = version.Id,
                RenderedSubject = TemplateRenderer.Render(version.SubjectEn ?? version.SubjectAr ?? string.Empty, sample),
                RenderedBody = TemplateRenderer.Render(version.BodyEn, sample),
                RecipientAddress = address,
                Status = DeliveryStatus.Queued,
            };
            _db.Deliveries.Add(delivery);

            if (TemplatePublishTransitions.CanTransition(version.PublishStatus, TemplatePublishStatus.TestSent))
            {
                version.PublishStatus = TemplatePublishStatus.TestSent;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return delivery.Id;
        }

        // ------------------------------------------------------------------ subscriptions

        public async Task SetSubscriptionStatutoryAsync(int subscriptionRuleId, bool isStatutory, CancellationToken cancellationToken = default)
        {
            var rule = await _db.SubscriptionRules.SingleAsync(r => r.Id == subscriptionRuleId, cancellationToken);
            rule.IsStatutory = isStatutory;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DisableSubscriptionAsync(int subscriptionRuleId, bool principalApprovalGranted = false, CancellationToken cancellationToken = default)
        {
            var rule = await _db.SubscriptionRules.SingleAsync(r => r.Id == subscriptionRuleId, cancellationToken);
            if (!StatutorySubscriptionGuard.CanDisable(rule.IsStatutory, principalApprovalGranted))
            {
                throw new StatutorySubscriptionChangeDeniedException(subscriptionRuleId);
            }

            rule.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ providers (BR-NTF-003)

        public async Task<IReadOnlyList<Provider>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => await _db.Providers
                .IgnoreQueryFilters()
                .Where(p => p.SchoolId == _db.CurrentSchoolId)
                .OrderBy(p => p.Channel)
                .ThenBy(p => p.FailoverOrder)
                .ToListAsync(cancellationToken);

        public async Task<Provider?> GetProviderAsync(int providerId, CancellationToken cancellationToken = default)
            => await _db.Providers
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(p => p.Id == providerId && p.SchoolId == _db.CurrentSchoolId, cancellationToken);

        public async Task<Provider> SaveProviderAsync(
            int? providerId,
            NotificationChannel channel,
            string providerCode,
            string displayName,
            string? accountIdentifier,
            string? secret,
            string? senderId,
            string? apiBaseUrl,
            int failoverOrder,
            CancellationToken cancellationToken = default)
        {
            var gateway = ProviderCatalog.Find(providerCode) ?? throw new UnknownProviderCodeException(providerCode);
            if (!gateway.Channels.Contains(channel))
            {
                throw new ProviderChannelMismatchException(gateway.Code, channel);
            }

            var provider = providerId is { } id
                ? await GetProviderAsync(id, cancellationToken) ?? throw new UnknownProviderCodeException(providerCode)
                : null;

            if (provider == null)
            {
                provider = new Provider { Channel = channel, ProviderCode = gateway.Code };
                _db.Providers.Add(provider);
            }
            else
            {
                provider.Channel = channel;
                provider.ProviderCode = gateway.Code;
            }

            provider.DisplayName = string.IsNullOrWhiteSpace(displayName) ? gateway.NameEn : displayName.Trim();
            provider.AccountIdentifier = Trimmed(accountIdentifier);
            provider.SenderId = NormalizeSender(senderId);
            provider.ApiBaseUrl = Trimmed(apiBaseUrl);
            provider.FailoverOrder = failoverOrder < 1 ? 1 : failoverOrder;

            // A blank secret means "leave it alone" — the console cannot show the stored token,
            // so re-typing it would be the price of every unrelated edit. Only a non-empty value
            // rotates it, and rotating invalidates the last test result: the credentials that
            // passed are no longer the credentials on the row.
            if (!string.IsNullOrWhiteSpace(secret))
            {
                provider.SecretCipher = _protector.Protect(secret!.Trim());
                provider.LastTestOutcome = ProviderTestOutcome.NeverTested;
                provider.LastTestedAtUtc = null;
                provider.LastTestDetail = null;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return provider;
        }

        public async Task<ProviderTestOutcome> TestProviderAsync(int providerId, CancellationToken cancellationToken = default)
        {
            var provider = await GetProviderAsync(providerId, cancellationToken)
                           ?? throw new UnknownProviderCodeException(null);

            var (passed, detail) = provider.IsConfigured
                ? await VerifyAsync(provider, cancellationToken)
                : (false, "The gateway is missing an account identifier, a token or a sender number.");

            provider.LastTestOutcome = passed ? ProviderTestOutcome.Passed : ProviderTestOutcome.Failed;
            provider.LastTestedAtUtc = _clock.UtcNow;
            provider.LastTestDetail = detail.Length > 500 ? detail.Substring(0, 500) : detail;

            await _db.SaveChangesAsync(cancellationToken);
            return provider.LastTestOutcome;
        }

        public async Task DeactivateProviderAsync(int providerId, CancellationToken cancellationToken = default)
        {
            var provider = await GetProviderAsync(providerId, cancellationToken)
                           ?? throw new UnknownProviderCodeException(null);

            if (!provider.IsActive)
            {
                return;
            }

            // BR-NTF-003's "deletion blocked while referenced by active rules", read as what it
            // protects: a school must not be able to switch off its last way of reaching parents
            // on a channel it is still subscribed to and hear nothing about it. A second active
            // gateway makes this a failover change rather than a blackout.
            var othersActive = await _db.Providers
                .IgnoreQueryFilters()
                .CountAsync(p => p.SchoolId == _db.CurrentSchoolId
                                 && p.Channel == provider.Channel
                                 && p.IsActive
                                 && p.Id != provider.Id, cancellationToken);

            if (othersActive == 0)
            {
                var rules = await _db.SubscriptionRules
                    .IgnoreQueryFilters()
                    .CountAsync(r => r.SchoolId == _db.CurrentSchoolId
                                     && r.Channel == provider.Channel
                                     && r.IsActive, cancellationToken);

                if (rules > 0)
                {
                    throw new ProviderInUseException(provider.Channel, rules);
                }
            }

            provider.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ReactivateProviderAsync(int providerId, CancellationToken cancellationToken = default)
        {
            var provider = await GetProviderAsync(providerId, cancellationToken)
                           ?? throw new UnknownProviderCodeException(null);

            provider.IsActive = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ delivery operations (BR-NTF-005)

        public async Task<IReadOnlyList<DeliveryRow>> ListDeliveriesAsync(
            DeliveryStatus? status = null,
            NotificationChannel? channel = null,
            string? eventCode = null,
            int take = 200,
            CancellationToken cancellationToken = default)
        {
            var query = _db.Deliveries.AsQueryable();

            if (status is { } wantedStatus)
            {
                query = query.Where(d => d.Status == wantedStatus);
            }

            if (channel is { } wantedChannel)
            {
                query = query.Where(d => d.Channel == wantedChannel);
            }

            if (!string.IsNullOrWhiteSpace(eventCode))
            {
                query = query.Where(d => d.EventCode == eventCode);
            }

            var page = await query
                .OrderByDescending(d => d.Id)
                .Take(take < 1 ? 1 : take)
                .Select(d => new
                {
                    d.Id, d.EventCode, d.Channel, d.RecipientUserId, d.RecipientAddress, d.RenderedSubject,
                    d.Status, d.AttemptCount, d.LastAttemptAtUtc, d.FailureReason, d.CreatedAtUtc,
                })
                .ToListAsync(cancellationToken);

            if (page.Count == 0)
            {
                return new List<DeliveryRow>();
            }

            // Past the filter: a delivery to somebody deactivated since is still a delivery, and
            // a log with a blank name where a person used to be reads as a bug.
            var userIds = page.Select(d => d.RecipientUserId).Distinct().ToList();
            var names = await _db.UserAccounts
                .IgnoreQueryFilters()
                .Where(a => a.SchoolId == _db.CurrentSchoolId && userIds.Contains(a.Id))
                .Select(a => new { a.Id, a.UserName })
                .ToDictionaryAsync(a => a.Id, a => a.UserName, cancellationToken);

            return page.Select(d => new DeliveryRow(
                    d.Id,
                    d.EventCode,
                    d.Channel,
                    d.RecipientUserId,
                    names.TryGetValue(d.RecipientUserId, out var name) ? name : $"#{d.RecipientUserId}",
                    d.Channel == NotificationChannel.Email ? d.RecipientAddress : PhoneNumberRules.Mask(d.RecipientAddress),
                    d.RenderedSubject,
                    d.Status,
                    d.AttemptCount,
                    d.LastAttemptAtUtc,
                    d.FailureReason,
                    d.CreatedAtUtc))
                .ToList();
        }

        public async Task<DeliveryTotals> CountDeliveriesAsync(CancellationToken cancellationToken = default)
        {
            var counts = await _db.Deliveries
                .GroupBy(d => d.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            int Of(DeliveryStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;

            return new DeliveryTotals(
                Of(DeliveryStatus.Queued), Of(DeliveryStatus.Sent), Of(DeliveryStatus.Delivered), Of(DeliveryStatus.Failed));
        }

        public async Task<int> RetryDeliveriesAsync(IReadOnlyCollection<int> deliveryIds, CancellationToken cancellationToken = default)
        {
            if (deliveryIds.Count == 0)
            {
                return 0;
            }

            var rows = await _db.Deliveries
                .Where(d => deliveryIds.Contains(d.Id) && d.Status == DeliveryStatus.Failed)
                .ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                row.Status = DeliveryStatus.Queued;

                // Reset, not decrement: BR-NOT-006's three strikes are three strikes per attempt
                // to deliver, and an operator who has fixed the number or the token is starting a
                // new one. Leaving the count at 3 would have the dispatcher fail it on sight.
                row.AttemptCount = 0;
                row.FailureReason = null;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return rows.Count;
        }

        // ------------------------------------------------------------------ the notification centre (§8.6)

        public async Task<IReadOnlyList<InboxItem>> ListInboxAsync(
            int userId, bool includeRead, CancellationToken cancellationToken = default)
        {
            var query = _db.Deliveries.Where(d =>
                d.RecipientUserId == userId
                && d.Channel == NotificationChannel.InApp

                // Queued in-app rows are not yet in the inbox: the dispatcher marks them
                // Delivered, and showing one before that would let a reader see a message the
                // log still says was never delivered.
                && d.Status == DeliveryStatus.Delivered);

            if (!includeRead)
            {
                query = query.Where(d => !d.IsRead);
            }

            return await query
                .OrderByDescending(d => d.Id)
                .Take(100)
                .Select(d => new InboxItem(d.Id, d.EventCode, d.RenderedSubject, d.RenderedBody, d.IsRead, d.CreatedAtUtc))
                .ToListAsync(cancellationToken);
        }

        public async Task MarkInAppReadAsync(int deliveryId, int userId, CancellationToken cancellationToken = default)
        {
            // The user id is in the predicate, not checked afterwards: this is the one screen
            // in the module with no permission behind it, so "it is mine" has to be the query.
            var delivery = await _db.Deliveries.SingleOrDefaultAsync(
                d => d.Id == deliveryId && d.RecipientUserId == userId && d.Channel == NotificationChannel.InApp,
                cancellationToken);

            if (delivery == null || delivery.IsRead)
            {
                return;
            }

            delivery.IsRead = true;
            delivery.ReadAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> MarkAllInAppReadAsync(int userId, CancellationToken cancellationToken = default)
        {
            var unread = await _db.Deliveries
                .Where(d => d.RecipientUserId == userId && d.Channel == NotificationChannel.InApp && !d.IsRead)
                .ToListAsync(cancellationToken);

            if (unread.Count == 0)
            {
                return 0;
            }

            var now = _clock.UtcNow;
            foreach (var delivery in unread)
            {
                delivery.IsRead = true;
                delivery.ReadAtUtc = now;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return unread.Count;
        }

        // ------------------------------------------------------------------ budgets (BR-NTF-004)

        public async Task<BudgetCheckResult> EvaluateBudgetAsync(
            NotificationChannel channel, string periodKey, int budgetLimit, bool isSafetyClass, CancellationToken cancellationToken = default)
        {
            var counter = await _db.BudgetCounters.SingleOrDefaultAsync(
                c => c.Channel == channel && c.PeriodKey == periodKey, cancellationToken);
            var count = counter?.MessageCount ?? 0;

            return new BudgetCheckResult
            {
                CurrentCount = count,
                ShouldAlert = BudgetThresholdEvaluator.ShouldAlert(count, budgetLimit),
                ShouldBlock = BudgetThresholdEvaluator.ShouldBlock(count, budgetLimit, isSafetyClass),
            };
        }

        public async Task<IReadOnlyList<BudgetRow>> ListBudgetsAsync(string periodKey, CancellationToken cancellationToken = default)
        {
            var counters = await _db.BudgetCounters
                .Where(c => c.PeriodKey == periodKey)
                .ToListAsync(cancellationToken);

            var hardStop = ParseBool(await _settings.GetSettingAsync(SettingKeys.BudgetHardStop, cancellationToken: cancellationToken));

            var rows = new List<BudgetRow>();
            foreach (var channel in Sms.Application.Messaging.AnnouncementChannels.Costed)
            {
                var limitKey = channel == NotificationChannel.Sms
                    ? SettingKeys.SmsMonthlyBudget
                    : SettingKeys.WhatsAppMonthlyBudget;

                var limit = ParseCount(await _settings.GetSettingAsync(limitKey, cancellationToken: cancellationToken));

                rows.Add(new BudgetRow(
                    channel,
                    periodKey,
                    counters.FirstOrDefault(c => c.Channel == channel)?.MessageCount ?? 0,
                    limit,
                    hardStop));
            }

            return rows;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// The registered sender for this provider's channel, asked to prove the credentials.
        /// A deployment whose sender is not the HTTP one (a stub, in a test) has nothing to
        /// verify against and says so rather than reporting a pass it did not earn.
        /// </summary>
        private async Task<(bool Passed, string Detail)> VerifyAsync(Provider provider, CancellationToken cancellationToken)
        {
            var sender = _senders.FirstOrDefault(s => s.Channel == provider.Channel);
            return sender is TwilioStyleChannelSender http
                ? await http.VerifyAsync(provider, cancellationToken)
                : (false, $"No verifiable transport is registered for {provider.Channel} in this deployment.");
        }

        private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>
        /// The sender number stored the way it will be sent: E.164 with no assumed country,
        /// because a school's own WhatsApp number is issued internationally and a leading zero
        /// on it is a typo rather than a national format to complete.
        /// </summary>
        private static string? NormalizeSender(string? senderId)
        {
            var trimmed = Trimmed(senderId);
            if (trimmed == null)
            {
                return null;
            }

            var normalized = PhoneNumberRules.Normalize(trimmed);
            return normalized.IsValid ? normalized.E164 : trimmed;
        }

        private static bool ParseBool(string? value) => bool.TryParse(value, out var parsed) && parsed;

        private static int? ParseCount(string? value)
            => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : null;
    }
}
