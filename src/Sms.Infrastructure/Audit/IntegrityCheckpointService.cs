using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Audit;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Audit
{
    /// <summary>
    /// Computes and verifies tamper-evidence checkpoints over the audit store
    /// (BR-AUD-007). Daily scheduling arrives with E-011 (jobs infrastructure);
    /// the default period is one day (doc 07 open question 3).
    /// </summary>
    public class IntegrityCheckpointService
    {
        private readonly SmsDbContext _db;
        private readonly IClock _clock;

        public IntegrityCheckpointService(SmsDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<IntegrityCheckpoint> ComputeAsync(DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken cancellationToken = default)
        {
            // AsNoTracking: hashing must see storage truth, never tracker state.
            var entries = await _db.AuditEntries.AsNoTracking()
                .Where(e => e.OccurredAtUtc >= periodStartUtc && e.OccurredAtUtc < periodEndUtc)
                .OrderBy(e => e.Id)
                .ToListAsync(cancellationToken);

            var previous = await _db.IntegrityCheckpoints.AsNoTracking()
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var entriesHash = AuditIntegrity.ComputeEntriesHash(entries);

            var checkpoint = new IntegrityCheckpoint
            {
                PeriodStartUtc = periodStartUtc,
                PeriodEndUtc = periodEndUtc,
                FirstEntryId = entries.Count == 0 ? (long?)null : entries[0].Id,
                LastEntryId = entries.Count == 0 ? (long?)null : entries[entries.Count - 1].Id,
                EntryCount = entries.Count,
                EntriesHash = entriesHash,
                PreviousChainHash = previous?.ChainHash,
                ChainHash = AuditIntegrity.ComputeChainHash(previous?.ChainHash, entriesHash),
                ComputedAtUtc = _clock.UtcNow,
            };

            _db.IntegrityCheckpoints.Add(checkpoint);
            await _db.SaveChangesAsync(cancellationToken);
            return checkpoint;
        }

        /// <summary>
        /// Recomputes the checkpoint's hashes from the stored entries; false
        /// means storage-level edits, inserts, or gaps in the covered period.
        /// </summary>
        public async Task<bool> VerifyAsync(long checkpointId, CancellationToken cancellationToken = default)
        {
            var checkpoint = await _db.IntegrityCheckpoints.AsNoTracking()
                .SingleAsync(c => c.Id == checkpointId, cancellationToken);

            var previous = await _db.IntegrityCheckpoints.AsNoTracking()
                .Where(c => c.Id < checkpoint.Id)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var entries = await _db.AuditEntries.AsNoTracking()
                .Where(e => e.OccurredAtUtc >= checkpoint.PeriodStartUtc && e.OccurredAtUtc < checkpoint.PeriodEndUtc)
                .OrderBy(e => e.Id)
                .ToListAsync(cancellationToken);

            return AuditIntegrity.Verify(checkpoint, entries, previous?.ChainHash);
        }
    }
}
