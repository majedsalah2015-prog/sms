using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    /// Drains Queued deliveries through the matching IChannelSender
    /// (BR-NOT-006: 3 attempts, then terminal Failed) and tallies BR-NOT-006
    /// SMS/WhatsApp cost counters on success. A standalone operation — saves
    /// its own batch.
    /// </summary>
    public class NotificationDispatcher : INotificationDispatcher
    {
        private const int MaxAttempts = 3;

        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IEnumerable<IChannelSender> _senders;

        public NotificationDispatcher(AppDbContext db, IClock clock, IEnumerable<IChannelSender> senders)
        {
            _db = db;
            _clock = clock;
            _senders = senders;
        }

        public async Task<int> DispatchQueuedAsync(CancellationToken cancellationToken = default)
        {
            var queued = await _db.Deliveries.Where(d => d.Status == DeliveryStatus.Queued).ToListAsync(cancellationToken);
            var now = _clock.UtcNow;

            foreach (var delivery in queued)
            {
                delivery.AttemptCount += 1;
                delivery.LastAttemptAtUtc = now;

                var sender = _senders.FirstOrDefault(s => s.Channel == delivery.Channel);
                if (sender == null)
                {
                    delivery.Status = DeliveryStatus.Failed;
                    delivery.FailureReason = $"No sender registered for channel '{delivery.Channel}'.";
                    continue;
                }

                var outcome = await sender.SendAsync(delivery, cancellationToken);
                if (outcome.Succeeded)
                {
                    delivery.Status = delivery.Channel == NotificationChannel.InApp ? DeliveryStatus.Delivered : DeliveryStatus.Sent;
                    delivery.ProviderReference = outcome.ProviderReference;
                    delivery.FailureReason = null;

                    if (delivery.Channel == NotificationChannel.Sms || delivery.Channel == NotificationChannel.WhatsApp)
                    {
                        await IncrementBudgetAsync(delivery.Channel, now, cancellationToken);
                    }
                }
                else
                {
                    delivery.FailureReason = outcome.FailureReason;
                    delivery.Status = delivery.AttemptCount >= MaxAttempts ? DeliveryStatus.Failed : DeliveryStatus.Queued;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return queued.Count;
        }

        private async Task IncrementBudgetAsync(NotificationChannel channel, DateTime now, CancellationToken cancellationToken)
        {
            var periodKey = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var counter = await _db.BudgetCounters.SingleOrDefaultAsync(
                c => c.Channel == channel && c.PeriodKey == periodKey, cancellationToken);

            if (counter == null)
            {
                counter = new BudgetCounter { Channel = channel, PeriodKey = periodKey, MessageCount = 0 };
                _db.BudgetCounters.Add(counter);
            }

            counter.MessageCount += 1;
        }
    }
}
