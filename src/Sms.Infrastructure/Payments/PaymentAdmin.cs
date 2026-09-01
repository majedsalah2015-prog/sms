using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Numbering;
using Sms.Application.Payments;
using Sms.Domain.Payments;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Payments
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class PaymentAdmin : IPaymentAdmin
    {
        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;

        public PaymentAdmin(AppDbContext db, INumberIssuer numberIssuer, IClock clock)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
        }

        public async Task<TillSession> OpenTillSessionAsync(int cashierUserId, string? tillCode, decimal floatAmount, CancellationToken cancellationToken = default)
        {
            // Both guards live here rather than on the screen: the console and the mobile API open
            // the same drawer, and a rule that only one of them applies is not a rule. They read
            // the whole open set once, which the assignment below needs anyway.
            var openSessions = await _db.TillSessions.AsNoTracking()
                .Where(s => s.Status == TillSessionStatus.Open)
                .Select(s => new { s.CashierUserId, s.TillCode })
                .ToListAsync(cancellationToken);

            var alreadyMine = openSessions.FirstOrDefault(s => s.CashierUserId == cashierUserId);
            if (alreadyMine != null)
            {
                throw new CashierAlreadyHasOpenTillException(cashierUserId, alreadyMine.TillCode);
            }

            var openCodes = openSessions.Select(s => s.TillCode).ToList();
            var requested = tillCode?.Trim();

            if (string.IsNullOrEmpty(requested))
            {
                requested = await ResolveTillCodeAsync(cashierUserId, openCodes, cancellationToken);
            }
            else if (openCodes.Contains(requested, StringComparer.OrdinalIgnoreCase))
            {
                // Ordinal-insensitive, not the provider's collation: on Sqlite "TILL-1" and "till-1"
                // are two drawers and the guard would wave the second cashier through.
                throw new TillAlreadyOpenException(requested);
            }

            var session = new TillSession
            {
                CashierUserId = cashierUserId,
                TillCode = requested,
                FloatAmount = floatAmount,
                OpenedAtUtc = _clock.UtcNow,
                Status = TillSessionStatus.Open,
            };
            _db.TillSessions.Add(session);

            await _db.SaveChangesAsync(cancellationToken);
            return session;
        }

        public async Task<string> NextTillCodeForAsync(int cashierUserId, CancellationToken cancellationToken = default)
        {
            var openCodes = await _db.TillSessions.AsNoTracking()
                .Where(s => s.Status == TillSessionStatus.Open)
                .Select(s => s.TillCode)
                .ToListAsync(cancellationToken);

            return await ResolveTillCodeAsync(cashierUserId, openCodes, cancellationToken);
        }

        private async Task<string> ResolveTillCodeAsync(int cashierUserId, IReadOnlyCollection<string> openCodes, CancellationToken cancellationToken)
        {
            // Their last drawer, open or closed — so a cashier stays on one code across days and the
            // by-till reports keep meaning something (doc/Modules/21 §8.2).
            var lastOwn = await _db.TillSessions.AsNoTracking()
                .Where(s => s.CashierUserId == cashierUserId)
                .OrderByDescending(s => s.OpenedAtUtc).ThenByDescending(s => s.Id)
                .Select(s => s.TillCode)
                .FirstOrDefaultAsync(cancellationToken);

            // Every code the school has recorded, not just the open ones: a minted code must not
            // land on a closed session's till and inherit its variance history.
            var everUsed = await _db.TillSessions.AsNoTracking().Select(s => s.TillCode).Distinct().ToListAsync(cancellationToken);

            return TillCodeGenerator.Resolve(lastOwn, openCodes, everUsed);
        }

        public async Task CloseTillSessionAsync(int tillSessionId, decimal countedTotal, string? varianceReason = null, CancellationToken cancellationToken = default)
        {
            var session = await _db.TillSessions.SingleAsync(s => s.Id == tillSessionId, cancellationToken);
            if (session.Status != TillSessionStatus.Open)
            {
                throw new TillSessionNotOpenException(tillSessionId);
            }

            // EF Core's Sqlite provider can't translate Sum() over decimal to SQL (no native DECIMAL type) - materialize then sum in memory.
            var systemTotal = (await _db.Receipts
                .Where(r => r.TillSessionId == tillSessionId && r.Status == ReceiptStatus.Posted)
                .Select(r => r.Amount)
                .ToListAsync(cancellationToken)).Sum();

            session.SystemTotal = systemTotal;
            session.CountedTotal = countedTotal;
            session.VarianceReason = varianceReason;
            session.ClosedAtUtc = _clock.UtcNow;
            session.Status = TillSessionStatus.Closed;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Receipt> CaptureReceiptAsync(
            int payerId, PaymentMethod method, decimal amount, int? tillSessionId = null, string? methodRefNo = null,
            CancellationToken cancellationToken = default)
        {
            if (tillSessionId.HasValue)
            {
                var session = await _db.TillSessions.SingleAsync(s => s.Id == tillSessionId.Value, cancellationToken);
                if (session.Status != TillSessionStatus.Open)
                {
                    throw new TillSessionNotOpenException(tillSessionId.Value);
                }
            }

            var receiptNo = await _numberIssuer.IssueAsync("RCP", cancellationToken);
            var receipt = new Receipt
            {
                PayerId = payerId,
                TillSessionId = tillSessionId,
                ReceiptNo = receiptNo,
                Method = method,
                MethodRefNo = methodRefNo,
                Amount = amount,
                Status = ReceiptStatus.Posted,
                IssuedAtUtc = _clock.UtcNow,
            };
            _db.Receipts.Add(receipt);
            await _db.SaveChangesAsync(cancellationToken);

            await AllocateReceiptAsync(receipt, cancellationToken);
            return receipt;
        }

        private async Task AllocateReceiptAsync(Receipt receipt, CancellationToken cancellationToken)
        {
            var charges = await _db.Charges
                .Where(c => c.PayerId == receipt.PayerId && c.Status == Domain.Fees.ChargeStatus.Posted)
                .ToListAsync(cancellationToken);
            var chargeIds = charges.Select(c => c.Id).ToList();

            // EF Core's Sqlite provider can't translate Sum() over decimal to SQL (no native DECIMAL type) - materialize the raw rows, group and sum in memory.
            var creditRows = await _db.CreditNotes
                .Where(n => chargeIds.Contains(n.ChargeId))
                .Select(n => new { n.ChargeId, n.Amount })
                .ToListAsync(cancellationToken);
            var creditedByCharge = creditRows.GroupBy(n => n.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            // S5/E-502: Module 22 discount documents reduce a charge's remaining balance exactly like credit notes (BR-DIS-005).
            var discountRows = await _db.DiscountDocuments
                .Where(d => chargeIds.Contains(d.ChargeId))
                .Select(d => new { d.ChargeId, d.Amount })
                .ToListAsync(cancellationToken);
            var discountedByCharge = discountRows.GroupBy(d => d.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            var allocationRows = await _db.PaymentAllocations
                .Where(a => chargeIds.Contains(a.ChargeId))
                .Select(a => new { a.ChargeId, a.AllocatedAmount })
                .ToListAsync(cancellationToken);
            var allocatedByCharge = allocationRows.GroupBy(a => a.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));

            var targets = charges
                .Select(c =>
                {
                    var credited = creditedByCharge.TryGetValue(c.Id, out var cAmt) ? cAmt : 0m;
                    var allocated = allocatedByCharge.TryGetValue(c.Id, out var aAmt) ? aAmt : 0m;
                    var discounted = discountedByCharge.TryGetValue(c.Id, out var dAmt) ? dAmt : 0m;
                    var remaining = c.GrossAmount - credited - discounted - allocated;
                    return new PaymentAllocationEngine.AllocationTarget(c.Id, remaining, c.PostedAtUtc);
                })
                .Where(t => t.RemainingBalance > 0);

            var (allocations, _) = PaymentAllocationEngine.Allocate(receipt.Amount, targets);
            foreach (var allocation in allocations)
            {
                _db.PaymentAllocations.Add(new PaymentAllocation
                {
                    ReceiptId = receipt.Id,
                    ChargeId = allocation.ChargeId,
                    AllocatedAmount = allocation.Amount,
                });
            }

            if (allocations.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<Pdc> LodgePdcAsync(int payerId, string bankName, string chequeNo, DateTime chequeDate, decimal amount, CancellationToken cancellationToken = default)
        {
            var pdc = new Pdc
            {
                PayerId = payerId,
                BankName = bankName,
                ChequeNo = chequeNo,
                ChequeDate = chequeDate,
                Amount = amount,
                Status = PdcStatus.Lodged,
            };
            _db.Pdcs.Add(pdc);

            await _db.SaveChangesAsync(cancellationToken);
            return pdc;
        }

        public async Task ChangePdcStatusAsync(int pdcId, PdcStatus newStatus, DateTime whenUtc, CancellationToken cancellationToken = default)
        {
            var pdc = await _db.Pdcs.SingleAsync(p => p.Id == pdcId, cancellationToken);
            if (!PdcStatusTransitions.CanTransition(pdc.Status, newStatus))
            {
                throw new InvalidPdcStatusTransitionException(pdc.Status, newStatus);
            }

            pdc.Status = newStatus;

            if (newStatus == PdcStatus.Cleared)
            {
                var receiptNo = await _numberIssuer.IssueAsync("RCP", cancellationToken);
                var receipt = new Receipt
                {
                    PayerId = pdc.PayerId,
                    ReceiptNo = receiptNo,
                    Method = PaymentMethod.Pdc,
                    MethodRefNo = pdc.ChequeNo,
                    Amount = pdc.Amount,
                    Status = ReceiptStatus.Posted,
                    IssuedAtUtc = whenUtc,
                };
                _db.Receipts.Add(receipt);
                await _db.SaveChangesAsync(cancellationToken);

                await AllocateReceiptAsync(receipt, cancellationToken);
                pdc.ClearedReceiptId = receipt.Id;
            }

            if (newStatus == PdcStatus.Bounced)
            {
                // BR-INS-009 (S5/E-501): a bounced cheque no longer covers anything — un-cover the
                // installments it was suppressing dunning for, so the ladder resumes on the next run.
                // (E-303 deferred exactly this until Module 20 existed.) Bounce-fee charge generation
                // is still Module 19 policy, not wired.
                var covered = await _db.Installments.Where(i => i.CoveringPdcId == pdc.Id).ToListAsync(cancellationToken);
                foreach (var installment in covered)
                {
                    installment.CoveringPdcId = null;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<decimal> ComputeRefundablePositionAsync(int payerId, CancellationToken cancellationToken)
        {
            // EF Core's Sqlite provider can't translate Sum() over decimal to SQL (no native DECIMAL type) - materialize then sum in memory.
            var totalReceipts = (await _db.Receipts
                .Where(r => r.PayerId == payerId && r.Status == ReceiptStatus.Posted && r.Purpose == ReceiptPurpose.FeePayment)
                .Select(r => r.Amount)
                .ToListAsync(cancellationToken)).Sum();
            var receiptIds = await _db.Receipts
                .Where(r => r.PayerId == payerId && r.Status == ReceiptStatus.Posted && r.Purpose == ReceiptPurpose.FeePayment)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);
            var totalAllocated = (await _db.PaymentAllocations
                .Where(a => receiptIds.Contains(a.ReceiptId))
                .Select(a => a.AllocatedAmount)
                .ToListAsync(cancellationToken)).Sum();
            var advanceBalance = AdvanceBalanceCalculator.Calculate(totalReceipts, totalAllocated);

            var totalCommittedRefunds = (await _db.RefundVouchers
                .Where(v => v.PayerId == payerId && v.Status != RefundVoucherStatus.Rejected)
                .Select(v => v.Amount)
                .ToListAsync(cancellationToken)).Sum();

            return advanceBalance - totalCommittedRefunds;
        }

        public async Task<RefundVoucher> RequestRefundAsync(int payerId, decimal amount, PaymentMethod method, string reason, CancellationToken cancellationToken = default)
        {
            var refundablePosition = await ComputeRefundablePositionAsync(payerId, cancellationToken);
            if (!RefundAmountGuard.IsWithinRefundablePosition(amount, refundablePosition))
            {
                throw new RefundExceedsPositionException(payerId);
            }

            var voucherNo = await _numberIssuer.IssueAsync("RFD", cancellationToken);
            var voucher = new RefundVoucher
            {
                PayerId = payerId,
                VoucherNo = voucherNo,
                Amount = amount,
                Method = method,
                Reason = reason,
                Status = RefundVoucherStatus.Requested,
            };
            _db.RefundVouchers.Add(voucher);

            await _db.SaveChangesAsync(cancellationToken);
            return voucher;
        }

        public async Task ChangeRefundVoucherStatusAsync(int refundVoucherId, RefundVoucherStatus newStatus, CancellationToken cancellationToken = default)
        {
            var voucher = await _db.RefundVouchers.SingleAsync(v => v.Id == refundVoucherId, cancellationToken);
            if (!RefundVoucherStatusTransitions.CanTransition(voucher.Status, newStatus))
            {
                throw new InvalidRefundVoucherStatusTransitionException(voucher.Status, newStatus);
            }

            voucher.Status = newStatus;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
