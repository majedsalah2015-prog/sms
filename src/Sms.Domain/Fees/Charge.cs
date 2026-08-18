using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Fees
{
    /// <summary>
    /// ppl.Charge (doc/Modules/19 §7, BR-FEE-003/005): a posted invoice
    /// line for one student x category. ChargeNo uses doc 08's strict
    /// "INV" series (already seeded by E-010). VatRateSnapshot/VatAmount
    /// freeze BR-GLB-061's "every document snapshots the rates used at
    /// posting" even if the category's rate changes later.
    /// InvoiceUuid/InvoiceHash/PreviousInvoiceHash are BR-FEE-005's
    /// e-invoicing-readiness structural fields (ZATCA-style ID + hash
    /// chain) — populated for real at posting (see ZatcaQrCodeBuilder /
    /// GradingAdmin-equivalent GenerateChargeAsync), but live submission
    /// to a tax authority endpoint is out of scope (activation per
    /// country pack, per doc's own framing).
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Charge : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int StudentId { get; set; }

        public int PayerId { get; set; }

        public int FeeCategoryId { get; set; }

        public ChargeSourceType SourceType { get; set; }

        /// <summary>doc 08 INV series.</summary>
        public string ChargeNo { get; set; } = string.Empty;

        public decimal NetAmount { get; set; }

        public decimal? VatRateSnapshot { get; set; }

        public decimal VatAmount { get; set; }

        public decimal GrossAmount { get; set; }

        public ChargeStatus Status { get; set; } = ChargeStatus.Posted;

        public DateTime PostedAtUtc { get; set; }

        public Guid InvoiceUuid { get; set; }

        public string? InvoiceHash { get; set; }

        public string? PreviousInvoiceHash { get; set; }

        /// <summary>
        /// BR-FEE-009 / BR-AYR-009 (S8/E-801): set only on
        /// <see cref="ChargeSourceType.OpeningBalance"/> charges — the closed
        /// year whose receivable this charge carries forward. Null otherwise.
        /// </summary>
        public int? SourceAcademicYearId { get; set; }
    }
}
