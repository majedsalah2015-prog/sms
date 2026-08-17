using System.Collections.Generic;
using Sms.Domain.Certificates;

namespace Sms.Application.Certificates
{
    /// <summary>BR-CRT-009: the outcome of one bulk-issuance batch — every issued certificate carries its own number; students who failed the prerequisite auto-check land in the exceptions queue with the reason.</summary>
    public sealed class CertificateBatchResult
    {
        public List<CertificateIssue> Issued { get; } = new();

        public List<CertificateBatchException> Exceptions { get; } = new();
    }

    public sealed record CertificateBatchException(int StudentId, int CertificateRequestId, string Reason);
}
