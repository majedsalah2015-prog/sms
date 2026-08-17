using Sms.Domain.Common;

namespace Sms.Domain.GlExport
{
    /// <summary>One summary journal line: account × source kind, debit or credit, with the number of source documents folded into it.</summary>
    public class GlJournalLine : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int GlExportBatchId { get; set; }

        public int SequenceNumber { get; set; }

        public string AccountKey { get; set; } = string.Empty;

        public string AccountCode { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Debit { get; set; }

        public decimal Credit { get; set; }

        public int SourceDocumentCount { get; set; }
    }
}
