using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sms.Application.Fees
{
    /// <summary>
    /// Pure BR-FEE-005: the hash-chain half of ZATCA-style e-invoicing
    /// readiness — each invoice's hash covers its own key fields plus the
    /// previous invoice's hash, so any retroactive edit breaks the chain
    /// (detectable, not preventable by this function alone). SHA-256,
    /// hex-encoded.
    /// </summary>
    public static class InvoiceHashChainBuilder
    {
        public static string ComputeHash(string invoiceUuid, decimal grossAmount, DateTime postedAtUtc, string? previousInvoiceHash)
        {
            var payload = string.Join(
                "|",
                invoiceUuid,
                grossAmount.ToString("F2", CultureInfo.InvariantCulture),
                postedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                previousInvoiceHash ?? string.Empty);

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(bytes);
        }
    }
}
