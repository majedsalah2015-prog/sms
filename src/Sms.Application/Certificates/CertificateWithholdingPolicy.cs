using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Certificates;

namespace Sms.Application.Certificates
{
    /// <summary>
    /// Pure BR-CRT-008 legal gate: which document kinds a country pack
    /// permits gating for unpaid fees. There is no CountryPack entity yet
    /// (E-101 never started), so — same precedent as
    /// <see cref="Fees.KsaVatRates"/> — the KSA-01 answer ships as a
    /// constant here rather than as a database row.
    ///
    /// KSA-01 default: transfer certificates may NOT be withheld for fees
    /// (the doc's own cited example of ministry-prohibited withholding);
    /// every other kind may be gated per school config. This is a
    /// PROVISIONAL default pending doc/Modules/18 open question Q1's
    /// per-country legal review — it is deliberately the conservative
    /// reading (blocks gating on the one document the doc names as
    /// sensitive), and is a single set to change once counsel answers,
    /// not a scatter of per-type flags.
    /// </summary>
    public static class CertificateWithholdingPolicy
    {
        public static readonly IReadOnlyCollection<CertificateKind> Ksa01NonGateableKinds = new HashSet<CertificateKind>
        {
            CertificateKind.TransferCertificate,
        };

        public static bool MayBeGatedForFees(CertificateKind kind, IReadOnlyCollection<CertificateKind> nonGateableKinds)
        {
            return !nonGateableKinds.Contains(kind);
        }

        public static bool MayBeGatedForFees(CertificateKind kind) => MayBeGatedForFees(kind, Ksa01NonGateableKinds);
    }
}
