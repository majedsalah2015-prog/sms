using System;
using Sms.Domain.Certificates;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-CRT-003: the requested request status pair isn't a legal WF-09 move.</summary>
    public class InvalidCertificateRequestStatusTransitionException : InvalidOperationException
    {
        public InvalidCertificateRequestStatusTransitionException(CertificateRequestStatus from, CertificateRequestStatus to)
            : base($"Certificate request status cannot move from '{from}' to '{to}' (BR-CRT-003).")
        {
        }
    }

    /// <summary>BR-CRT-001/003: the type's required prerequisites (published results and/or fee clearance) aren't satisfied.</summary>
    public class CertificatePrerequisitesNotMetException : InvalidOperationException
    {
        public CertificatePrerequisitesNotMetException(int certificateRequestId)
            : base($"Certificate request {certificateRequestId}'s prerequisites are not met (BR-CRT-001/003).")
        {
        }
    }

    /// <summary>BR-CRT-008: the type's clearance check failed and no Principal override reason was supplied.</summary>
    public class CertificateFeeClearanceBlockedException : CertificatePrerequisitesNotMetException
    {
        public CertificateFeeClearanceBlockedException(int certificateRequestId, decimal position)
            : base(certificateRequestId)
        {
            Position = position;
        }

        /// <summary>The student's financial position at the time of the check (positive = owes) — the doc's "ClearanceBlocked" notification carries this summary.</summary>
        public decimal Position { get; }
    }

    /// <summary>BR-CRT-008: the country pack forbids withholding this document kind for unpaid fees, so a non-Disabled clearance rule can't be configured on it.</summary>
    public class CertificateKindNotGateableException : InvalidOperationException
    {
        public CertificateKindNotGateableException(CertificateKind kind)
            : base($"Certificate kind '{kind}' may not be gated on fee clearance under the active country pack (BR-CRT-008, doc/Modules/18 Q1).")
        {
        }
    }

    /// <summary>BR-CRT-008: FeeClearanceRule.NoOverdue can't be evaluated until charges carry due dates (E-303 deferred installment schedules).</summary>
    public class FeeClearanceRuleNotSupportedException : NotSupportedException
    {
        public FeeClearanceRuleNotSupportedException(FeeClearanceRule rule)
            : base($"Fee clearance rule '{rule}' cannot be evaluated yet — charges carry no due date (BR-CRT-008).")
        {
        }
    }

    /// <summary>BR-CRT-006: only an Issued certificate can be revoked.</summary>
    public class CertificateNotIssuedException : InvalidOperationException
    {
        public CertificateNotIssuedException(int certificateIssueId)
            : base($"Certificate issue {certificateIssueId} is not in Issued status (BR-CRT-006).")
        {
        }
    }
}
