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

        public async Task UpdateCategoryAsync(
            int feeCategoryId, string nameAr, string nameEn, decimal? vatRate, bool isMandatory, bool isRefundable, bool isServiceLinked,
            string? glExportCode = null, CancellationToken cancellationToken = default)
        {
            var category = await _db.FeeCategories.SingleAsync(c => c.Id == feeCategoryId, cancellationToken);
            category.NameAr = nameAr;
            category.NameEn = nameEn;
            category.VatRate = vatRate;
            category.IsMandatory = isMandatory;
            category.IsRefundable = isRefundable;
            category.IsServiceLinked = isServiceLinked;
            category.GlExportCode = glExportCode;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeactivateCategoryAsync(int feeCategoryId, CancellationToken cancellationToken = default)
        {
            var category = await _db.FeeCategories.SingleAsync(c => c.Id == feeCategoryId, cancellationToken);
            var lines = await _db.FeeStructureLines.CountAsync(l => l.FeeCategoryId == feeCategoryId, cancellationToken);
            var charges = await _db.Charges.CountAsync(c => c.FeeCategoryId == feeCategoryId, cancellationToken);
            if (lines > 0 || charges > 0)
            {
                throw new FeeCategoryInUseException(feeCategoryId, lines, charges);
            }

            category.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<FeeStructureLine> DefineStructureLineAsync(
            int gradeYearProfileId, int feeCategoryId, decimal amount, CancellationToken cancellationToken = default)
        {
            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == gradeYearProfileId, cancellationToken);
            if (await _db.FeeStructureLines.AnyAsync(l => l.GradeYearProfileId == gradeYearProfileId && l.FeeCategoryId == feeCategoryId, cancellationToken))
            {
                throw new FeeStructureLineAlreadyExistsException(gradeYearProfileId, feeCategoryId);
            }

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

        public async Task UpdateStructureLineAsync(int feeStructureLineId, decimal amount, CancellationToken cancellationToken = default)
        {
            var line = await _db.FeeStructureLines.SingleAsync(l => l.Id == feeStructureLineId, cancellationToken);
            if (line.Status != FeeStructureLineStatus.Draft)
            {
                throw new FeeStructureLineNotDraftException(feeStructureLineId);
            }

            line.Amount = amount;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteStructureLineAsync(int feeStructureLineId, CancellationToken cancellationToken = default)
        {
            var line = await _db.FeeStructureLines.SingleAsync(l => l.Id == feeStructureLineId, cancellationToken);
            if (line.Status != FeeStructureLineStatus.Draft)
            {
                throw new FeeStructureLineNotDraftException(feeStructureLineId);
            }

            _db.FeeStructureLines.Remove(line);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> CopyStructureAsync(int sourceAcademicYearId, int targetAcademicYearId, decimal upliftPercent, CancellationToken cancellationToken = default)
        {
            var sourceProfiles = await _db.GradeYearProfiles.IgnoreQueryFilters()
                .Where(p => p.AcademicYearId == sourceAcademicYearId && p.SchoolId == _db.CurrentSchoolId)
                .Select(p => new { p.Id, p.GradeLevelId }).ToListAsync(cancellationToken);
            var targetProfiles = await _db.GradeYearProfiles.IgnoreQueryFilters()
                .Where(p => p.AcademicYearId == targetAcademicYearId && p.SchoolId == _db.CurrentSchoolId && p.IsActive)
                .Select(p => new { p.Id, p.GradeLevelId }).ToListAsync(cancellationToken);
            var targetByGrade = targetProfiles.GroupBy(p => p.GradeLevelId).ToDictionary(g => g.Key, g => g.First().Id);
            var sourceIds = sourceProfiles.Select(p => p.Id).ToList();
            var targetIds = targetProfiles.Select(p => p.Id).ToList();

            var sourceLines = await _db.FeeStructureLines
                .Where(l => sourceIds.Contains(l.GradeYearProfileId) && l.Status == FeeStructureLineStatus.Approved)
                .ToListAsync(cancellationToken);
            var existing = await _db.FeeStructureLines
                .Where(l => targetIds.Contains(l.GradeYearProfileId))
                .Select(l => new { l.GradeYearProfileId, l.FeeCategoryId }).ToListAsync(cancellationToken);
            var existingSet = existing.Select(e => (e.GradeYearProfileId, e.FeeCategoryId)).ToHashSet();

            var factor = 1m + upliftPercent / 100m;
            var created = 0;
            foreach (var src in sourceLines)
            {
                var gradeLevelId = sourceProfiles.First(p => p.Id == src.GradeYearProfileId).GradeLevelId;
                if (!targetByGrade.TryGetValue(gradeLevelId, out var targetProfileId)) continue;
                if (!existingSet.Add((targetProfileId, src.FeeCategoryId))) continue;
                _db.FeeStructureLines.Add(new FeeStructureLine
                {
                    AcademicYearId = targetAcademicYearId,
                    GradeYearProfileId = targetProfileId,
                    FeeCategoryId = src.FeeCategoryId,
                    Amount = Math.Round(src.Amount * factor, 2, MidpointRounding.AwayFromZero),
                    Status = FeeStructureLineStatus.Draft,
                });
                created++;
            }

            if (created > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            return created;
        }

        public async Task<Payer> EnsurePayerForParentAsync(int parentId, CancellationToken cancellationToken = default)
        {
            var payer = await _db.Payers.FirstOrDefaultAsync(p => p.ParentId == parentId, cancellationToken);
            if (payer != null) return payer;

            if (!await _db.Parents.AnyAsync(p => p.Id == parentId, cancellationToken))
            {
                throw new InvalidOperationException($"Parent {parentId} does not exist — a payer must be backed by a real guardian record.");
            }

            payer = new Payer { Type = PayerType.Parent, ParentId = parentId };
            _db.Payers.Add(payer);
            await _db.SaveChangesAsync(cancellationToken);
            return payer;
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

        public async Task<Charge> PostManualGrossChargeAsync(
            int studentId, int payerId, int feeCategoryId, decimal grossAmount, decimal? vatRate, CancellationToken cancellationToken = default)
        {
            var academicYearId = await _db.Enrollments
                .Where(e => e.StudentId == studentId)
                .OrderByDescending(e => e.Id)
                .Select(e => e.AcademicYearId)
                .FirstAsync(cancellationToken);

            var (vatAmount, netAmount) = VatCalculator.CalculateFromGross(grossAmount, vatRate);
            var charge = await BuildChargeAsync(studentId, payerId, academicYearId, feeCategoryId, vatRate, netAmount, vatAmount,
                ChargeSourceType.Manual, sourceAcademicYearId: null, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return charge;
        }

        public async Task<Charge> PostOpeningBalanceAsync(
            int studentId, int payerId, int targetAcademicYearId, int sourceAcademicYearId, int feeCategoryId, decimal amount,
            CancellationToken cancellationToken = default)
        {
            if (amount <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "An opening balance carries a positive receivable.");
            }

            // Ambient: no VAT, no save — the caller commits it with its carry-forward credit notes (BR-AYR-009 hard check).
            return await BuildChargeAsync(studentId, payerId, targetAcademicYearId, feeCategoryId, vatRate: null, amount, vatAmount: 0m,
                ChargeSourceType.OpeningBalance, sourceAcademicYearId, cancellationToken);
        }

        private async Task<Charge> PostChargeInternalAsync(
            int studentId, int payerId, int academicYearId, int feeCategoryId, decimal? vatRate, decimal netAmount,
            ChargeSourceType sourceType, CancellationToken cancellationToken)
        {
            var (vatAmount, _) = VatCalculator.Calculate(netAmount, vatRate);
            var charge = await BuildChargeAsync(studentId, payerId, academicYearId, feeCategoryId, vatRate, netAmount, vatAmount, sourceType, sourceAcademicYearId: null, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return charge;
        }

        private async Task<Charge> BuildChargeAsync(
            int studentId, int payerId, int academicYearId, int feeCategoryId, decimal? vatRate, decimal netAmount, decimal vatAmount,
            ChargeSourceType sourceType, int? sourceAcademicYearId, CancellationToken cancellationToken)
        {
            var grossAmount = netAmount + vatAmount;
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
                SourceAcademicYearId = sourceAcademicYearId,
            };
            _db.Charges.Add(charge);
            return charge;
        }

        public async Task<CreditNote> IssueWriteOffCreditNoteAsync(int chargeId, decimal amount, string reason, CancellationToken cancellationToken = default)
        {
            var note = await BuildCreditNoteAsync(chargeId, amount, reason, cancellationToken);
            note.IsWriteOff = true;
            return note;
        }

        public async Task<CreditNote> IssueCarryForwardCreditNoteAsync(int chargeId, decimal amount, CancellationToken cancellationToken = default)
        {
            var note = await BuildCreditNoteAsync(chargeId, amount, CarryForwardReason, cancellationToken);
            note.IsCarryForward = true;
            return note;
        }

        /// <summary>Fixed reason text on every carry-forward note (BR-AYR-009) — the flag, not the text, is what readers key on.</summary>
        public const string CarryForwardReason = "Carry-forward to next academic year (BR-AYR-009)";

        public async Task<CreditNote> IssueCreditNoteAsync(int chargeId, decimal amount, string reason, CancellationToken cancellationToken = default)
        {
            var creditNote = await BuildCreditNoteAsync(chargeId, amount, reason, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return creditNote;
        }

        public async Task VoidChargeAsync(int chargeId, CancellationToken cancellationToken = default)
        {
            var charge = await _db.Charges.SingleAsync(c => c.Id == chargeId, cancellationToken);
            if (charge.Status != ChargeStatus.Posted)
            {
                throw new ChargeNotPostedException(chargeId);
            }

            var hasActivity = await _db.PaymentAllocations.AnyAsync(a => a.ChargeId == chargeId, cancellationToken)
                || await _db.CreditNotes.AnyAsync(n => n.ChargeId == chargeId, cancellationToken)
                || await _db.DiscountDocuments.AnyAsync(d => d.ChargeId == chargeId, cancellationToken);
            if (hasActivity)
            {
                throw new ChargeHasActivityException(chargeId);
            }

            charge.Status = ChargeStatus.Void;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<CreditNote> BuildCreditNoteAsync(int chargeId, decimal amount, string reason, CancellationToken cancellationToken)
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
            return creditNote;
        }

        public async Task<decimal> ComputeStudentPositionAsync(int studentId, CancellationToken cancellationToken = default)
        {
            var chargeIds = await _db.Charges.Where(c => c.StudentId == studentId && c.Status == ChargeStatus.Posted).Select(c => c.Id).ToListAsync(cancellationToken);

            var totalCharges = (await _db.Charges.Where(c => chargeIds.Contains(c.Id)).Select(c => c.GrossAmount).ToListAsync(cancellationToken)).Sum();
            var totalCreditNotes = (await _db.CreditNotes.Where(n => chargeIds.Contains(n.ChargeId)).Select(n => n.Amount).ToListAsync(cancellationToken)).Sum();
            var totalDiscounts = (await _db.DiscountDocuments.Where(d => chargeIds.Contains(d.ChargeId)).Select(d => d.Amount).ToListAsync(cancellationToken)).Sum();
            var totalAllocated = (await _db.PaymentAllocations.Where(a => chargeIds.Contains(a.ChargeId)).Select(a => a.AllocatedAmount).ToListAsync(cancellationToken)).Sum();

            return StudentFinancialPositionCalculator.Calculate(totalCharges, totalCreditNotes, totalDiscounts, totalAllocated);
        }
    }
}
