using System;
using Sms.Domain.Payments;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-PAY-002: every counter receipt belongs to an open cashier session.</summary>
    public class TillSessionNotOpenException : InvalidOperationException
    {
        public TillSessionNotOpenException(int tillSessionId)
            : base($"Till session {tillSessionId} is not open (BR-PAY-001).")
        {
        }
    }

    /// <summary>BR-PAY-004: the requested PDC status pair isn't a legal lifecycle move.</summary>
    public class InvalidPdcStatusTransitionException : InvalidOperationException
    {
        public InvalidPdcStatusTransitionException(PdcStatus from, PdcStatus to)
            : base($"PDC status cannot move from '{from}' to '{to}' (BR-PAY-004).")
        {
        }
    }

    /// <summary>BR-PAY-005: the requested refund voucher status pair isn't a legal WF-05 move.</summary>
    public class InvalidRefundVoucherStatusTransitionException : InvalidOperationException
    {
        public InvalidRefundVoucherStatusTransitionException(RefundVoucherStatus from, RefundVoucherStatus to)
            : base($"Refund voucher status cannot move from '{from}' to '{to}' (BR-PAY-005).")
        {
        }
    }

    /// <summary>BR-PAY-005: a refund can never exceed the payer's refundable (advance) position.</summary>
    public class RefundExceedsPositionException : InvalidOperationException
    {
        public RefundExceedsPositionException(int payerId)
            : base($"Refund amount exceeds payer {payerId}'s refundable position (BR-PAY-005).")
        {
        }
    }
}
