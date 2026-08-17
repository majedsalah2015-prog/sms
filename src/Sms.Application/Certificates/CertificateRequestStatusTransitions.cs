using Sms.Domain.Certificates;

namespace Sms.Application.Certificates
{
    /// <summary>Pure BR-CRT-003 WF-09 spine.</summary>
    public static class CertificateRequestStatusTransitions
    {
        public static bool CanTransition(CertificateRequestStatus from, CertificateRequestStatus to)
        {
            return (from, to) switch
            {
                (CertificateRequestStatus.Requested, CertificateRequestStatus.Approved) => true,
                (CertificateRequestStatus.Requested, CertificateRequestStatus.Rejected) => true,
                (CertificateRequestStatus.Approved, CertificateRequestStatus.Issued) => true,
                _ => false,
            };
        }
    }
}
