namespace Sms.Application.Fees
{
    /// <summary>
    /// S3/E-305 KSA-01 content pack (BR-SET-004's "country pack binds VAT
    /// defaults" — the CountryPack entity itself doesn't exist yet, E-101
    /// never started, so this is a plain reference constant a demo/onboarding
    /// seed can use when defining FeeCategory rows, not a live-configurable
    /// setting). 15% per KSA's standard VAT rate; education fees are
    /// typically VAT-exempt in KSA (a FeeCategory with VatRate = null
    /// models that), transport/uniform/activity fees are typically
    /// standard-rated — doc/Modules/19 §14 Q2 flags exactly this
    /// per-category split as the intended v1 design.
    /// </summary>
    public static class KsaVatRates
    {
        public const decimal Standard = 0.15m;
    }
}
