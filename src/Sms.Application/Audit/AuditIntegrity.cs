using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sms.Domain.Audit;

namespace Sms.Application.Audit
{
    /// <summary>
    /// Pure hash-chain arithmetic behind the tamper-evidence checkpoints
    /// (BR-AUD-007). Canonical serialization is length-prefixed so values
    /// containing separators cannot collide.
    /// </summary>
    public static class AuditIntegrity
    {
        public static string ComputeEntriesHash(IEnumerable<AuditEntry> entriesInIdOrder)
        {
            var builder = new StringBuilder();
            foreach (var entry in entriesInIdOrder)
            {
                builder.Append(CanonicalString(entry));
                builder.Append('\n');
            }

            return Sha256Hex(builder.ToString());
        }

        public static string ComputeChainHash(string? previousChainHash, string entriesHash)
        {
            return Sha256Hex((previousChainHash ?? string.Empty) + entriesHash);
        }

        /// <summary>
        /// Verifies a checkpoint against the recomputed hash of its stored
        /// entries and the previous checkpoint's chain hash.
        /// </summary>
        public static bool Verify(IntegrityCheckpoint checkpoint, IEnumerable<AuditEntry> storedEntriesInIdOrder, string? previousChainHash)
        {
            var entriesHash = ComputeEntriesHash(storedEntriesInIdOrder);
            if (!string.Equals(entriesHash, checkpoint.EntriesHash, StringComparison.Ordinal))
            {
                return false;
            }

            var chainHash = ComputeChainHash(previousChainHash, entriesHash);
            return string.Equals(chainHash, checkpoint.ChainHash, StringComparison.Ordinal);
        }

        public static string CanonicalString(AuditEntry entry)
        {
            var parts = new[]
            {
                entry.Id.ToString(CultureInfo.InvariantCulture),
                entry.EntityType,
                entry.EntityId?.ToString(CultureInfo.InvariantCulture),
                entry.BusinessKey,
                entry.FieldName,
                entry.OldValue,
                entry.NewValue,
                entry.ActorUserId.ToString(CultureInfo.InvariantCulture),
                ((short)entry.Action).ToString(CultureInfo.InvariantCulture),
                entry.Reason,
                entry.CorrelationId.ToString("N"),
                entry.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture),
            };

            var builder = new StringBuilder();
            foreach (var part in parts)
            {
                builder.Append(part == null ? -1 : part.Length);
                builder.Append(':');
                builder.Append(part);
                builder.Append('|');
            }

            return builder.ToString();
        }

        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));

            var builder = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
