using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Infrastructure.Audit
{
    /// <summary>
    /// An audit entry waiting for the business save to complete; entries for
    /// Added entities resolve their generated id afterwards (same transaction,
    /// BR-AUD-003).
    /// </summary>
    internal sealed class PendingAuditEntry
    {
        public PendingAuditEntry(AuditEntry entry, EntityEntry? deferredIdSource)
        {
            Entry = entry;
            DeferredIdSource = deferredIdSource;
        }

        public AuditEntry Entry { get; }

        public EntityEntry? DeferredIdSource { get; }
    }

    /// <summary>
    /// Builds field-level (T1/T2) and record-level (T3) audit entries from the
    /// change tracker per doc 07 §3–4. Enforced centrally in the context, never
    /// per module.
    /// </summary>
    internal static class AuditCaptor
    {
        /// <summary>BR-GLB-007 stamps are inline on the record (BR-AUD-008); diffing them would be noise.</summary>
        private static readonly HashSet<string> ExcludedProperties = new()
        {
            nameof(AuditableEntity.Id),
            nameof(AuditableEntity.CreatedByUserId),
            nameof(AuditableEntity.CreatedAtUtc),
            nameof(AuditableEntity.ModifiedByUserId),
            nameof(AuditableEntity.ModifiedAtUtc),
        };

        public static List<PendingAuditEntry> Collect(DbContext context, IAuditContext? audit, ICurrentUser currentUser, IClock clock)
        {
            var pending = new List<PendingAuditEntry>();
            var correlationId = Guid.NewGuid();

            foreach (var entry in context.ChangeTracker.Entries().ToList())
            {
                var attribute = entry.Entity.GetType().GetCustomAttribute<AuditedAttribute>(inherit: true);
                if (attribute == null)
                {
                    continue;
                }

                // Per-school configuration may raise this later; never lower (BR-AUD-002).
                var tier = AuditClassification.Effective(attribute.Tier, configured: null);

                switch (entry.State)
                {
                    case EntityState.Added:
                        pending.Add(new PendingAuditEntry(
                            BuildEntry(entry, audit, currentUser, clock, correlationId, AuditAction.Create),
                            deferredIdSource: entry));
                        break;

                    case EntityState.Modified:
                        CollectModified(entry, tier, pending, audit, currentUser, clock, correlationId);
                        break;
                }
            }

            return pending;
        }

        public static void ResolveDeferredIds(IEnumerable<PendingAuditEntry> pending)
        {
            foreach (var item in pending)
            {
                if (item.DeferredIdSource != null)
                {
                    item.Entry.EntityId = ReadEntityId(item.DeferredIdSource);
                }
            }
        }

        private static void CollectModified(
            EntityEntry entry,
            AuditTier tier,
            List<PendingAuditEntry> pending,
            IAuditContext? audit,
            ICurrentUser currentUser,
            IClock clock,
            Guid correlationId)
        {
            var changed = entry.Properties
                .Where(p => p.IsModified
                            && !ExcludedProperties.Contains(p.Metadata.Name)
                            && !Equals(p.OriginalValue, p.CurrentValue))
                .ToList();

            if (changed.Count == 0)
            {
                return;
            }

            if (tier == AuditTier.T3)
            {
                var statusChanged = changed.Any(p => p.Metadata.Name == nameof(IActivatable.IsActive));
                pending.Add(new PendingAuditEntry(
                    BuildEntry(entry, audit, currentUser, clock, correlationId,
                        statusChanged ? AuditAction.StatusChange : AuditAction.Update),
                    deferredIdSource: null));
                return;
            }

            foreach (var property in changed)
            {
                if (tier == AuditTier.T1 && RequiresReason(property) && string.IsNullOrWhiteSpace(audit?.Reason))
                {
                    throw new MissingAuditReasonException(entry.Entity.GetType().Name, property.Metadata.Name);
                }

                var action = property.Metadata.Name == nameof(IActivatable.IsActive)
                    ? AuditAction.StatusChange
                    : AuditAction.Update;

                var auditEntry = BuildEntry(entry, audit, currentUser, clock, correlationId, action);
                auditEntry.FieldName = property.Metadata.Name;

                // A credential's value never reaches the trail — see SecretFieldAttribute for why
                // storing the ciphertext here would not count as protecting it. The entry itself
                // still goes in: knowing a school's gateway token was rotated, by whom and when, is
                // most of what auditing the provider row is for.
                if (IsSecret(property))
                {
                    auditEntry.OldValue = property.OriginalValue == null ? null : SecretFieldAttribute.Redaction;
                    auditEntry.NewValue = property.CurrentValue == null ? null : SecretFieldAttribute.Redaction;
                }
                else
                {
                    auditEntry.OldValue = ToRawValue(property.OriginalValue);
                    auditEntry.NewValue = ToRawValue(property.CurrentValue);
                }

                pending.Add(new PendingAuditEntry(auditEntry, deferredIdSource: null));
            }
        }

        private static AuditEntry BuildEntry(
            EntityEntry entry,
            IAuditContext? audit,
            ICurrentUser currentUser,
            IClock clock,
            Guid correlationId,
            AuditAction action)
        {
            var entity = entry.Entity;

            return new AuditEntry
            {
                SchoolId = entity is ISchoolScoped scoped ? scoped.SchoolId : (int?)null,
                AcademicYearId = entity is IYearScoped year ? year.AcademicYearId : (int?)null,
                EntityType = entity.GetType().Name,
                EntityId = entry.State == EntityState.Added ? (long?)null : ReadEntityId(entry),
                BusinessKey = (entity as IAuditBusinessKey)?.AuditBusinessKey,
                ActorUserId = currentUser.UserId,
                Action = action,
                Reason = audit?.Reason,
                CorrelationId = correlationId,
                SourceScreen = audit?.SourceScreen,
                ClientIp = audit?.ClientIp,
                OccurredAtUtc = clock.UtcNow,
            };
        }

        private static bool RequiresReason(PropertyEntry property)
        {
            return property.Metadata.PropertyInfo?.GetCustomAttribute<RequiresAuditReasonAttribute>(inherit: true) != null;
        }

        private static bool IsSecret(PropertyEntry property)
        {
            return property.Metadata.PropertyInfo?.GetCustomAttribute<SecretFieldAttribute>(inherit: true) != null;
        }

        private static long? ReadEntityId(EntityEntry entry)
        {
            var keyProperty = entry.Metadata.FindPrimaryKey()?.Properties.SingleOrDefault();
            if (keyProperty == null)
            {
                return null;
            }

            var value = entry.Property(keyProperty.Name).CurrentValue;
            return value == null ? (long?)null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        /// <summary>Single stored truth, culture-invariant (BR-AUD-005); display localizes later.</summary>
        private static string? ToRawValue(object? value)
        {
            return value switch
            {
                null => null,
                bool b => b ? "true" : "false",
                DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture),
            };
        }
    }
}
