using System;
using System.Collections.Generic;
using Sms.Domain.Attachments;
using Sms.Domain.Numbering;

namespace Sms.Web.Models
{
    /// <summary>
    /// The two registries doc/Modules/01 §8.3 asks the settings hub to embed and
    /// which had engines but no screen: doc 08's numbering series, and doc 10 §5's
    /// document-type catalogue. Both are configuration a school adjusts once and
    /// then lives with, so both screens are lists that explain what a change costs
    /// before it is made.
    /// </summary>
    public sealed class NumberingRegistryViewModel
    {
        /// <summary>
        /// One series version, with what it will produce and whether it can still be
        /// edited in place.
        /// <paramref name="IssuedThisPeriod"/> is the sequence the current reset period
        /// has reached — the number the next document actually continues from.
        /// <paramref name="IssuedEver"/> is every period added up, which is the honest
        /// answer to "how much has this series produced" and a different question.
        /// </summary>
        public sealed record Row(NumberingSeries Series, string Preview, int IssuedThisPeriod, int IssuedEver, bool IsCurrent);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        /// <summary>Pre-filled when the operator clicked "new version" on an existing series.</summary>
        public string? Code { get; set; }

        public string? EntityName { get; set; }

        public string? FormatTemplate { get; set; }

        public ResetPolicy ResetPolicy { get; set; } = ResetPolicy.Never;

        public GapPolicy GapPolicy { get; set; } = GapPolicy.Normal;

        public DateTime EffectiveFrom { get; set; }

        /// <summary>True when the code already exists and locked, so saving cuts over to a new version rather than editing.</summary>
        public bool WouldCutOver { get; set; }
    }

    public sealed class DocumentTypeCatalogViewModel
    {
        public sealed record Row(DocumentType Type, int AttachmentCount);

        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        /// <summary>Module codes that already own a document type, for the filter.</summary>
        public IReadOnlyList<string> Modules { get; set; } = Array.Empty<string>();

        public string? ModuleFilter { get; set; }

        public bool IncludeInactive { get; set; }

        /// <summary>
        /// The type the form is correcting, if any — the ?edit= on the URL. The code is the
        /// identity, so editing locks that field and the save is the same upsert as adding.
        /// </summary>
        public int? EditId { get; set; }

        // --- the form ---------------------------------------------------------

        public string? Code { get; set; }

        public string? ModuleCode { get; set; }

        public string? NameAr { get; set; }

        public string? NameEn { get; set; }

        public bool AllowPdf { get; set; } = true;

        public bool AllowJpg { get; set; } = true;

        public bool AllowPng { get; set; } = true;

        public bool AllowDocx { get; set; }

        public bool AllowXlsx { get; set; }

        /// <summary>Megabytes, because nobody configures a limit in bytes; null means the product default.</summary>
        public int? MaxSizeMb { get; set; }

        public bool IsMandatoryByDefault { get; set; }

        public bool IsExpiryTracked { get; set; }

        public bool IsRestricted { get; set; }
    }
}
