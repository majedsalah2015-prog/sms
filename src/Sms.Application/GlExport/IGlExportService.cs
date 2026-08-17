using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.GlExport;

namespace Sms.Application.GlExport
{
    /// <summary>
    /// E-503 (O3 assumption): generic journal-summary export with a
    /// per-school mapping table and CSV output. No named accounting-system
    /// adapter — the pilot school's ERP picks the first one (Implementation
    /// 01 O3). doc/Modules/19 §8 "GL export" screen deferred.
    /// </summary>
    public interface IGlExportService
    {
        Task<GlAccountMapping> DefineMappingAsync(string key, string accountCode, string accountNameAr, string accountNameEn, CancellationToken cancellationToken = default);

        /// <summary>
        /// Summarizes every posted charge, credit note, discount document, posted receipt and paid refund whose date falls in
        /// [from, to] into a balanced, numbered batch. Throws <see cref="Common.Exceptions.GlMappingMissingException"/> listing
        /// every unmapped key, <see cref="Common.Exceptions.GlPeriodOverlapException"/> if a non-voided batch already covers any of the period.
        /// </summary>
        Task<GlExportBatch> GenerateAsync(DateTime periodFromUtc, DateTime periodToUtc, int generatedByUserId, CancellationToken cancellationToken = default);

        /// <summary>Renders the batch as CSV; the result's SHA-256 equals GlExportBatch.ContentHash.</summary>
        Task<string> RenderCsvAsync(int glExportBatchId, CancellationToken cancellationToken = default);

        /// <summary>Voids a batch (reason mandatory, T1) so the period can be regenerated after corrections.</summary>
        Task VoidAsync(int glExportBatchId, string reason, CancellationToken cancellationToken = default);
    }
}
