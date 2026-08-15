using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Numbering;
using Sms.Domain.Numbering;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Numbering
{
    /// <summary>
    /// doc 08 §3 definition/cutover. A standalone admin action — unlike
    /// <see cref="NumberIssuer"/> it saves itself, since there is no larger
    /// posting transaction to ride.
    /// </summary>
    public class NumberingSeriesAdmin : INumberingSeriesAdmin
    {
        private readonly AppDbContext _db;

        public NumberingSeriesAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<NumberingSeries> DefineSeriesAsync(
            string code,
            string entityName,
            string formatTemplate,
            ResetPolicy resetPolicy,
            GapPolicy gapPolicy,
            DateTime effectiveFromUtc,
            CancellationToken cancellationToken = default)
        {
            var existing = await _db.NumberingSeries.SingleOrDefaultAsync(s => s.Code == code && s.IsActive, cancellationToken);

            if (existing != null && !existing.IsLocked)
            {
                // Free to edit in place until the first number is issued (doc 08 §3).
                existing.EntityName = entityName;
                existing.FormatTemplate = formatTemplate;
                existing.ResetPolicy = resetPolicy;
                existing.GapPolicy = gapPolicy;
                existing.EffectiveFromUtc = effectiveFromUtc;
                await _db.SaveChangesAsync(cancellationToken);
                return existing;
            }

            if (existing != null)
            {
                // Locked: the old version is deactivated, never deleted — it stays
                // queryable for continuity reporting (doc 08 §7).
                existing.IsActive = false;
            }

            var next = new NumberingSeries
            {
                Code = code,
                Version = existing == null ? 1 : existing.Version + 1,
                EntityName = entityName,
                FormatTemplate = formatTemplate,
                ResetPolicy = resetPolicy,
                GapPolicy = gapPolicy,
                EffectiveFromUtc = effectiveFromUtc,
                IsActive = true,
            };
            _db.NumberingSeries.Add(next);
            await _db.SaveChangesAsync(cancellationToken);
            return next;
        }
    }
}
