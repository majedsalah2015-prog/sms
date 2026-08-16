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
    }
}
