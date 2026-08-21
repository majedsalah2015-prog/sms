namespace Sms.Application.Fees
{
    /// <summary>Pure BR-GLB-060/061: VatRate is a fraction (0.15 = 15%); null = exempt. Rounded to 2dp, BR-GLB-060's "no floating-point money" via decimal + explicit rounding.</summary>
    public static class VatCalculator
    {
        public static (decimal VatAmount, decimal GrossAmount) Calculate(decimal netAmount, decimal? vatRate)
        {
            if (vatRate == null)
            {
                return (0m, netAmount);
            }

            var vat = System.Math.Round(netAmount * vatRate.Value, 2, System.MidpointRounding.AwayFromZero);
            return (vat, netAmount + vat);
        }

        /// <summary>
        /// The inverse: split a VAT-inclusive figure into net + VAT using the tax fraction (r / 1+r), so
        /// net + VAT is the gross that went in — exactly, to the cent, for every input. Dividing by (1+r)
        /// and re-grossing does not have that property, and a claw-back that recovers a cent more or less
        /// than the discount it reverses leaves a balance no one can explain.
        /// </summary>
        public static (decimal VatAmount, decimal NetAmount) CalculateFromGross(decimal grossAmount, decimal? vatRate)
        {
            if (vatRate == null || vatRate.Value == 0m)
            {
                return (0m, grossAmount);
            }

            var vat = System.Math.Round(grossAmount * vatRate.Value / (1m + vatRate.Value), 2, System.MidpointRounding.AwayFromZero);
            return (vat, grossAmount - vat);
        }
    }
}
