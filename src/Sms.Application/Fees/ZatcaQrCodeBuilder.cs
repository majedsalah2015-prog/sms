using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Sms.Application.Fees
{
    /// <summary>
    /// Pure BR-FEE-005: ZATCA (KSA e-invoicing) Phase-1 simplified tax
    /// invoice QR payload — the well-known 5-field TLV (Tag-Length-Value)
    /// encoding, Base64-wrapped. This is the actual compliance-relevant
    /// data encoding (real, implementable offline, no external service or
    /// package needed) — it is NOT the same thing as rendering a scannable
    /// QR barcode image, which needs a QR-image library that hasn't been
    /// chosen (a separate, smaller decision than O6's report-card PDF
    /// engine spike). Live submission to ZATCA's platform (Phase 2) is
    /// explicitly out of scope per doc/Modules/19's own framing
    /// ("design here, implementation gated").
    /// </summary>
    public static class ZatcaQrCodeBuilder
    {
        private const byte SellerNameTag = 1;
        private const byte VatRegistrationNumberTag = 2;
        private const byte TimestampTag = 3;
        private const byte InvoiceTotalTag = 4;
        private const byte VatTotalTag = 5;

        public static string BuildBase64Payload(
            string sellerName, string vatRegistrationNumber, DateTime timestampUtc, decimal invoiceTotalWithVat, decimal vatTotal)
        {
            using var stream = new MemoryStream();
            WriteTlv(stream, SellerNameTag, sellerName);
            WriteTlv(stream, VatRegistrationNumberTag, vatRegistrationNumber);
            WriteTlv(stream, TimestampTag, timestampUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            WriteTlv(stream, InvoiceTotalTag, invoiceTotalWithVat.ToString("F2", CultureInfo.InvariantCulture));
            WriteTlv(stream, VatTotalTag, vatTotal.ToString("F2", CultureInfo.InvariantCulture));

            return Convert.ToBase64String(stream.ToArray());
        }

        private static void WriteTlv(Stream stream, byte tag, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "ZATCA TLV value exceeds the single-byte length field (255 bytes).");
            }

            stream.WriteByte(tag);
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
