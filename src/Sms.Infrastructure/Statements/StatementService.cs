using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Numbering;
using Sms.Application.Statements;
using Sms.Domain.Discounts;
using Sms.Domain.Fees;
using Sms.Domain.Payments;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Statements
{
    /// <summary>Assembles a payer's statement from every Module 19/21/22 document type; BR-DIS-010 keeps discounts a separate line kind.</summary>
    public class StatementService : IStatementService
    {
        public const string StatementSeriesCode = "STM";

        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;

        public StatementService(AppDbContext db, INumberIssuer numberIssuer, IClock clock)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
        }

        public async Task<PayerStatement> BuildAsync(int payerId, DateTime? asOfUtc = null, CancellationToken cancellationToken = default)
        {
            var charges = await _db.Charges.Where(c => c.PayerId == payerId && c.Status == ChargeStatus.Posted).ToListAsync(cancellationToken);
            var chargeIds = charges.Select(c => c.Id).ToList();
            var chargeNoById = charges.ToDictionary(c => c.Id, c => c.ChargeNo);

            var creditNotes = await _db.CreditNotes.Where(n => chargeIds.Contains(n.ChargeId)).ToListAsync(cancellationToken);
            var discounts = await _db.DiscountDocuments.Where(d => chargeIds.Contains(d.ChargeId)).ToListAsync(cancellationToken);
            var receipts = await _db.Receipts.Where(r => r.PayerId == payerId && r.Status == ReceiptStatus.Posted && r.Purpose == ReceiptPurpose.FeePayment).ToListAsync(cancellationToken);
            var refunds = await _db.RefundVouchers.Where(v => v.PayerId == payerId && v.Status == RefundVoucherStatus.Paid).ToListAsync(cancellationToken);

            var lines = new List<StatementLine>();
            lines.AddRange(charges.Select(c => new StatementLine(c.PostedAtUtc, StatementLineKind.Charge, c.ChargeNo, "Charge", c.GrossAmount, 0m)));
            lines.AddRange(creditNotes.Select(n => new StatementLine(n.IssuedAtUtc, StatementLineKind.CreditNote, n.CreditNoteNo, $"Credit note on {chargeNoById[n.ChargeId]}", 0m, n.Amount)));
            lines.AddRange(discounts.Select(d => new StatementLine(d.IssuedAtUtc, StatementLineKind.Discount, d.DocumentNo, $"Discount on {chargeNoById[d.ChargeId]}", 0m, d.Amount)));
            lines.AddRange(receipts.Select(r => new StatementLine(r.IssuedAtUtc, StatementLineKind.Payment, r.ReceiptNo, $"Payment ({r.Method})", 0m, r.Amount)));
            lines.AddRange(refunds.Select(v => new StatementLine(v.ModifiedAtUtc ?? v.CreatedAtUtc, StatementLineKind.Refund, v.VoucherNo, "Refund", v.Amount, 0m)));

            return StatementBuilder.Build(lines, asOfUtc);
        }

        public async Task<PayerStatement> BuildForStudentAsync(int studentId, DateTime? asOfUtc = null, CancellationToken cancellationToken = default)
        {
            var charges = await _db.Charges.Where(c => c.StudentId == studentId && c.Status == ChargeStatus.Posted).ToListAsync(cancellationToken);
            var chargeIds = charges.Select(c => c.Id).ToList();
            var chargeNoById = charges.ToDictionary(c => c.Id, c => c.ChargeNo);

            var creditNotes = await _db.CreditNotes.Where(n => chargeIds.Contains(n.ChargeId)).ToListAsync(cancellationToken);
            var discounts = await _db.DiscountDocuments.Where(d => chargeIds.Contains(d.ChargeId)).ToListAsync(cancellationToken);

            // The payer statement lists receipts; this one lists what those receipts were allocated
            // to, so the credit side of a child's statement can never exceed what was billed against
            // that child. Voided receipts drop out with their allocations — the join is inner.
            var allocations = await _db.PaymentAllocations.Where(a => chargeIds.Contains(a.ChargeId)).ToListAsync(cancellationToken);
            var receiptIds = allocations.Select(a => a.ReceiptId).Distinct().ToList();
            var receipts = await _db.Receipts
                .Where(r => receiptIds.Contains(r.Id) && r.Status == ReceiptStatus.Posted && r.Purpose == ReceiptPurpose.FeePayment)
                .ToListAsync(cancellationToken);

            var lines = new List<StatementLine>();
            lines.AddRange(charges.Select(c => new StatementLine(c.PostedAtUtc, StatementLineKind.Charge, c.ChargeNo, "Charge", c.GrossAmount, 0m)));
            lines.AddRange(creditNotes.Select(n => new StatementLine(n.IssuedAtUtc, StatementLineKind.CreditNote, n.CreditNoteNo, $"Credit note on {chargeNoById[n.ChargeId]}", 0m, n.Amount)));
            lines.AddRange(discounts.Select(d => new StatementLine(d.IssuedAtUtc, StatementLineKind.Discount, d.DocumentNo, $"Discount on {chargeNoById[d.ChargeId]}", 0m, d.Amount)));
            foreach (var allocation in allocations)
            {
                var receipt = receipts.FirstOrDefault(r => r.Id == allocation.ReceiptId);
                if (receipt == null) continue;
                lines.Add(new StatementLine(receipt.IssuedAtUtc, StatementLineKind.Payment, receipt.ReceiptNo,
                    $"Payment ({receipt.Method}) allocated to {chargeNoById[allocation.ChargeId]}", 0m, allocation.AllocatedAmount));
            }

            return StatementBuilder.Build(lines, asOfUtc);
        }

        public async Task<StatementIssue> IssueAsync(int payerId, CancellationToken cancellationToken = default)
        {
            var asOf = _clock.UtcNow;
            var statement = await BuildAsync(payerId, asOf, cancellationToken);
            var issue = new StatementIssue
            {
                PayerId = payerId,
                StatementNo = await _numberIssuer.IssueAsync(StatementSeriesCode, cancellationToken),
                AsOfUtc = asOf,
                SnapshotJson = JsonSerializer.Serialize(new
                {
                    statement.GrossCharges, statement.Discounts, statement.CreditNotes, statement.Payments, statement.Refunds, statement.NetCharges, statement.ClosingBalance,
                    Lines = statement.Lines.Select(l => new { l.DateUtc, Kind = l.Kind.ToString(), l.DocumentNo, l.Debit, l.Credit, l.RunningBalance }),
                }),
                ClosingBalance = statement.ClosingBalance,
            };
            _db.StatementIssues.Add(issue);
            await _db.SaveChangesAsync(cancellationToken);
            return issue;
        }
    }
}
