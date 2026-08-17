using System;
using System.Collections.Generic;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>E-503: the mapping table has no active account for one or more journal keys the period needs.</summary>
    public class GlMappingMissingException : InvalidOperationException
    {
        public GlMappingMissingException(IReadOnlyCollection<string> missingKeys)
            : base($"No GL account mapping for: {string.Join(", ", missingKeys)}.")
        {
            MissingKeys = missingKeys;
        }

        public IReadOnlyCollection<string> MissingKeys { get; }
    }

    /// <summary>E-503: a non-voided batch already covers part of the requested period — documents must never reach the ledger twice.</summary>
    public class GlPeriodOverlapException : InvalidOperationException
    {
        public GlPeriodOverlapException(string existingBatchNo)
            : base($"Period overlaps existing GL export batch {existingBatchNo}; void it first.")
        {
        }
    }

    /// <summary>E-503: only a Generated batch can be voided.</summary>
    public class GlBatchNotGeneratedException : InvalidOperationException
    {
        public GlBatchNotGeneratedException(int glExportBatchId)
            : base($"GL export batch {glExportBatchId} is not in Generated status.")
        {
        }
    }
}
