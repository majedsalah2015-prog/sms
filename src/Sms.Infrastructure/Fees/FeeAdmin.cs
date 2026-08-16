using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.Numbering;
using Sms.Domain.Fees;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Fees
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class FeeAdmin : IFeeAdmin
    {
        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;

        public FeeAdmin(AppDbContext db, INumberIssuer numberIssuer, IClock clock)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
        }

        public async Task<FeeCategory> DefineCategoryAsync(
            string nameAr, string nameEn, decimal? vatRate, bool isMandatory, bool isRefundable, bool isServiceLinked,
            string? glExportCode = null, CancellationToken cancellationToken = default)
        {
            var category = new FeeCategory
            {
                NameAr = nameAr,
                NameEn = nameEn,
                VatRate = vatRate,
                IsMandatory = isMandatory,
                IsRefundable = isRefundable,
                IsServiceLinked = isServiceLinked,
                GlExportCode = glExportCode,
            };
            _db.FeeCategories.Add(category);

            await _db.SaveChangesAsync(cancellationToken);
            return category;
        }

        public async Task<FeeStructureLine> DefineStructureLineAsync(
            int gradeYearProfileId, int feeCategoryId, decimal amount, CancellationToken cancellationToken = default)
        {
            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == gradeYearProfileId, cancellationToken);

            var line = new FeeStructureLine
            {
                AcademicYearId = profile.AcademicYearId,
                GradeYearProfileId = gradeYearProfileId,
                FeeCategoryId = feeCategoryId,
                Amount = amount,
                Status = FeeStructureLineStatus.Draft,
            };
            _db.FeeStructureLines.Add(line);

            await _db.SaveChangesAsync(cancellationToken);
            return line;
        }

        public async Task ApproveStructureLineAsync(int feeStructureLineId, CancellationToken cancellationToken = default)
        {
            var line = await _db.FeeStructureLines.SingleAsync(l => l.Id == feeStructureLineId, cancellationToken);
            if (!FeeStructureLineStatusTransitions.CanTransition(line.Status, FeeStructureLineStatus.Approved))
            {
                throw new InvalidFeeStructureLineStatusTransitionException(line.Status, FeeStructureLineStatus.Approved);
            }

            line.Status = FeeStructureLineStatus.Approved;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Charge> PostChargeAsync(
            int studentId, int payerId, int gradeYearProfileId, int feeCategoryId, ChargeSourceType sourceType,
            CancellationToken cancellationToken = default)
        {
            var line = await _db.FeeStructureLines
                .Where(l => l.GradeYearProfileId == gradeYearProfileId && l.FeeCategoryId == feeCategoryId && l.Status == FeeStructureLineStatus.Approved)
                .SingleOrDefaultAsync(cancellationToken);
            if (line == null)
            {
                throw new FeeStructureLineNotApprovedException(gradeYearProfileId, feeCategoryId);
            }

            var category = await _db.FeeCategories.SingleAsync(c => c.Id == feeCategoryId, cancellationToken);
            return await PostChargeInternalAsync(studentId, payerId, line.AcademicYearId, feeCategoryId, category.VatRate, line.Amount, sourceType, cancellationToken);
        }

        public async Task<Charge> PostManualChargeAsync(
            int studentId, int payerId, int feeCategoryId, decimal amount, CancellationToken cancellationToken = default)
        {
            var category = await _db.FeeCategories.SingleAsync(c => c.Id == feeCategoryId, cancellationToken);
            var academicYearId = await _db.Enrollments
                .Where(e => e.StudentId == studentId)
                .OrderByDescending(e => e.Id)
                .Select(e => e.AcademicYearId)
                .FirstAsync(cancellationToken);

            return await PostChargeInternalAsync(studentId, payerId, academicYearId, feeCategoryId, category.VatRate, amount, ChargeSourceType.Manual, cancellationToken);
        }

        private async Task<Charge> PostChargeInternalAsync(
            int studentId, int payerId, int academicYearId, int feeCategoryId, decimal? vatRate, decimal netAmount,
            ChargeSourceType sourceType, CancellationToken cancellationToken)
        {
            var (vatAmount, grossAmount) = VatCalculator.Calculate(netAmount, vatRate);
            var chargeNo = await _numberIssuer.IssueAsync("INV", cancellationToken);
            var previousHash = await _db.Charges.OrderByDescending(c => c.Id).Select(c => c.InvoiceHash).FirstOrDefaultAsync(cancellationToken);

            var invoiceUuid = Guid.NewGuid();
            var postedAtUtc = _clock.UtcNow;
            var invoiceHash = InvoiceHashChainBuilder.ComputeHash(invoiceUuid.ToString(), grossAmount, postedAtUtc, previousHash);

            var charge = new Charge
            {
                AcademicYearId = academicYearId,
                StudentId = studentId,
                PayerId = payerId,
                FeeCategoryId = feeCategoryId,
                SourceType = sourceType,
                ChargeNo = chargeNo,
                NetAmount = netAmount,
                VatRateSnapshot = vatRate,
                VatAmount = vatAmount,
                GrossAmount = grossAmount,
                Status = ChargeStatus.Posted,
                PostedAtUtc = postedAtUtc,
                InvoiceUuid = invoiceUuid,
                InvoiceHash = invoiceHash,
                PreviousInvoiceHash = previousHash,
            };
            _db.Charges.Add(charge);

            await _db.SaveChangesAsync(cancellationToken);
            return charge;
        }

        public async Task<CreditNote> IssueCreditNoteAsync(int chargeId, decimal amount, string reason, CancellationToken cancellationToken = default)
        {
            var charge = await _db.Charges.SingleAsync(c => c.Id == chargeId, cancellationToken);
            if (charge.Status != ChargeStatus.Posted)
            {
                throw new ChargeNotPostedException(chargeId);
            }

            // EF Core's Sqlite provider can't translate Sum() over decimal to SQL (no native DECIMAL type) - materialize then sum in memory.
            var alreadyCredited = (await _db.CreditNotes.Where(n => n.ChargeId == chargeId).Select(n => n.Amount).ToListAsync(cancellationToken)).Sum();
            if (alreadyCredited + amount > charge.GrossAmount)
            {
                throw new CreditNoteExceedsChargeException(chargeId);
            }

            var creditNoteNo = await _numberIssuer.IssueAsync("CRN", cancellationToken);
            var creditNote = new CreditNote
            {
                ChargeId = chargeId,
                CreditNoteNo = creditNoteNo,
                Amount = amount,
                Reason = reason,
                IssuedAtUtc = _clock.UtcNow,
            };
            _db.CreditNotes.Add(creditNote);

            await _db.SaveChangesAsync(cancellationToken);
            return creditNote;
        }

        public async Task<decimal> ComputeStudentPositionAsync(int studentId, CancellationToken cancellationToken = default)
        {
            var chargeIds = await _db.Charges.Where(c => c.StudentId == studentId && c.Status == ChargeStatus.Posted).Select(c => c.Id).ToListAsync(cancellationToken);

            var totalCharges = (await _db.Charges.Where(c => chargeIds.Contains(c.Id)).Select(c => c.GrossAmount).ToListAsync(cancellationToken)).Sum();
            var totalCreditNotes = (await _db.CreditNotes.Where(n => chargeIds.Contains(n.ChargeId)).Select(n => n.Amount).ToListAsync(cancellationToken)).Sum();
            var totalAllocated = (await _db.PaymentAllocations.Where(a => chargeIds.Contains(a.ChargeId)).Select(a => a.AllocatedAmount).ToListAsync(cancellationToken)).Sum();

            return StudentFinancialPositionCalculator.Calculate(totalCharges, totalCreditNotes, totalAllocated);
        }
    }
}
