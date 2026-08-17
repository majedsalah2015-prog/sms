using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sms.Application.Certificates
{
    /// <summary>
    /// Pure BR-CRT-005: a deterministic verification code (SHA-256 over
    /// the certificate number + issuance timestamp, hex-encoded,
    /// truncated to a QR-friendly length) rather than a random GUID — so
    /// it stays a pure, testable function, same style as E-303's
    /// InvoiceHashChainBuilder. The actual public verification endpoint
    /// and QR rendering are screens/infrastructure, deferred.
    /// </summary>
    public static class CertificateVerificationCodeBuilder
    {
        public static string Build(string certificateNo, DateTime issuedAtUtc)
        {
            var payload = string.Join("|", certificateNo, issuedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(bytes).Substring(0, 16);
        }
    }
}
