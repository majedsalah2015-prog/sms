namespace Sms.Domain.Numbering
{
    /// <summary>
    /// doc 08 §3. Strict is mandatory for receipts/invoices/refund vouchers
    /// (BR-NUM-003) — the issuer enforces gap-free concurrency safety for
    /// both policies identically; Strict is the marker that a series carries
    /// a legal continuity requirement (doc 08 §7 auditor report).
    /// </summary>
    public enum GapPolicy : short
    {
        Normal = 1,
        Strict = 2,
    }
}
