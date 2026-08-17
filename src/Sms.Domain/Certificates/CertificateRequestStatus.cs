namespace Sms.Domain.Certificates
{
    /// <summary>BR-CRT-003 WF-09: Requested -> Approved -> Issued; Requested -> Rejected.</summary>
    public enum CertificateRequestStatus : short
    {
        Requested = 1,
        Approved = 2,
        Issued = 3,
        Rejected = 4,
    }
}
