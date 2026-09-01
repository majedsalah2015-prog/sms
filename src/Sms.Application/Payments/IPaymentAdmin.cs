using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Payments;

namespace Sms.Application.Payments
{
    /// <summary>
    /// doc/Modules/21 §8 Cashier screen / Till session console / PDC
    /// registry / Refund desk screens backing (screens deferred, the
    /// operations are core).
    /// </summary>
    public interface IPaymentAdmin
    {
        /// <summary>
        /// Opens a cashier's session (BR-PAY-001). Leave <paramref name="tillCode"/> null or blank
        /// and the drawer is assigned by <see cref="TillCodeGenerator"/> — the cashier's own till if
        /// they have one, otherwise the next unused code.
        /// <para>
        /// Enforces both halves of cashier × till: throws
        /// <see cref="Common.Exceptions.CashierAlreadyHasOpenTillException"/> when this cashier is
        /// already at a drawer, and <see cref="Common.Exceptions.TillAlreadyOpenException"/> when an
        /// explicitly named till is in someone else's hands.
        /// </para>
        /// </summary>
        Task<TillSession> OpenTillSessionAsync(int cashierUserId, string? tillCode, decimal floatAmount, CancellationToken cancellationToken = default);

        /// <summary>
        /// The code <see cref="OpenTillSessionAsync"/> would assign this cashier — so the console can
        /// show the drawer before it is opened instead of a box nobody knows what to type into.
        /// </summary>
        Task<string> NextTillCodeForAsync(int cashierUserId, CancellationToken cancellationToken = default);

        /// <summary>Sums this session's receipts as the system total. Throws if the session isn't Open.</summary>
        Task CloseTillSessionAsync(int tillSessionId, decimal countedTotal, string? varianceReason = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Issues a real RCP number and auto-allocates oldest-due-first across
        /// the payer's open charges (BR-PAY-003); any leftover becomes advance
        /// balance. Throws <see cref="Common.Exceptions.TillSessionNotOpenException"/>
        /// when a till session id is given but that session isn't Open.
        /// </summary>
        Task<Receipt> CaptureReceiptAsync(
            int payerId, PaymentMethod method, decimal amount, int? tillSessionId = null, string? methodRefNo = null,
            CancellationToken cancellationToken = default);

        Task<Pdc> LodgePdcAsync(int payerId, string bankName, string chequeNo, DateTime chequeDate, decimal amount, CancellationToken cancellationToken = default);

        /// <summary>
        /// Throws <see cref="Common.Exceptions.InvalidPdcStatusTransitionException"/>.
        /// Moving to Cleared issues a real receipt (numbered at clearance date) and allocates it like any other payment.
        /// </summary>
        Task ChangePdcStatusAsync(int pdcId, PdcStatus newStatus, DateTime whenUtc, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.RefundExceedsPositionException"/> if the amount exceeds the payer's advance balance.</summary>
        Task<RefundVoucher> RequestRefundAsync(int payerId, decimal amount, PaymentMethod method, string reason, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidRefundVoucherStatusTransitionException"/>.</summary>
        Task ChangeRefundVoucherStatusAsync(int refundVoucherId, RefundVoucherStatus newStatus, CancellationToken cancellationToken = default);
    }
}
