namespace Sms.Domain.Certificates
{
    /// <summary>
    /// BR-CRT-001's cataloged document classes. Drives BR-CRT-008's
    /// country-pack legal gate (which kinds may lawfully be withheld for
    /// unpaid fees) — a school-defined <see cref="CertificateType"/> is
    /// always one of these kinds, however it's named.
    /// </summary>
    public enum CertificateKind : short
    {
        EnrollmentProof = 1,
        TransferCertificate = 2,
        Completion = 3,
        Transcript = 4,
        Conduct = 5,
        Honor = 6,
        CustomLetter = 7,
    }
}
