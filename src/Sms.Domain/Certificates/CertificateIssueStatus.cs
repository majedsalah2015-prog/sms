namespace Sms.Domain.Certificates
{
    /// <summary>BR-CRT-006: revoked numbers stay in the register (BR-NUM-002) — never deleted, verification returns Revoked.</summary>
    public enum CertificateIssueStatus : short
    {
        Issued = 1,
        Revoked = 2,
    }
}
