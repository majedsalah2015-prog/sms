using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Discounts;
using Sms.Application.Fees;
using Sms.Application.Installments;
using Sms.Domain.Fees;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Fees
{
    /// <summary>
    /// Composes the three money modules into the single gesture doc/Modules/19 §8.7 is read
    /// with at a counter. It holds no pricing, scheduling or discount rule of its own — every
    /// figure comes from the module that owns it — and its whole contribution is the order and
    /// the transaction.
    /// </summary>
    public class StudentFeeFileService : IStudentFeeFileService
    {
        private readonly AppDbContext _db;
        private readonly IFeeAdmin _fees;
        private readonly IInstallmentAdmin _installments;
        private readonly IDiscountAdmin _discounts;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _currentUser;
        private readonly IAuditContext _audit;

        public StudentFeeFileService(
            AppDbContext db, IFeeAdmin fees, IInstallmentAdmin installments, IDiscountAdmin discounts,
            IWorkingYearContext workingYear, ICurrentUser currentUser, IAuditContext audit)
        {
            _db = db;
            _fees = fees;
            _installments = installments;
            _discounts = discounts;
            _workingYear = workingYear;
            _currentUser = currentUser;
            _audit = audit;
        }

        public async Task<StudentFeeFileCommitResult> CommitAsync(StudentFeeFileCommit request, CancellationToken cancellationToken = default)
        {
            var categoryIds = (request.FeeCategoryIds ?? Array.Empty<int>()).Distinct().ToList();
            if (categoryIds.Count == 0 && request.ExtraItem == null && request.PlanTemplateId == null && request.Discount == null)
            {
                throw new EmptyFeeFileCommitException();
            }

            var yearId = _workingYear.AcademicYearId;
            var enrollment = await _db.Enrollments
                .Where(e => e.StudentId == request.StudentId && e.AcademicYearId == yearId
                    && e.Status == EnrollmentStatus.Active && e.ExitDate == null)
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (enrollment == null && categoryIds.Count > 0)
            {
                throw new StudentNotEnrolledForFeeFileException(request.StudentId);
            }

            // The screen hides an item it has already billed, so a duplicate arriving here is a
            // stale form — two clerks on the same child, or a back button. Checked before anything
            // is written so the refusal costs nothing and the basket stays all-or-nothing either way.
            if (categoryIds.Count > 0)
            {
                var billed = await _db.Charges
                    .Where(c => c.StudentId == request.StudentId && c.AcademicYearId == yearId
                        && c.Status == ChargeStatus.Posted && categoryIds.Contains(c.FeeCategoryId))
                    .Select(c => c.FeeCategoryId)
                    .ToListAsync(cancellationToken);
                if (billed.Count > 0)
                {
                    throw new FeeItemAlreadyBilledException(billed[0]);
                }
            }

            // One transaction over three services that each save themselves. Without it a basket
            // could bill the family and then fail to schedule what it billed, and the screen would
            // have reported a refusal over money that had already moved.
            using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            var payer = await _fees.EnsurePayerForParentAsync(request.ParentId, cancellationToken);

            var postedIds = new List<int>();
            var postedGross = 0m;
            foreach (var categoryId in categoryIds)
            {
                // Billed at the approved structure price, never at a figure typed on this screen:
                // BR-FEE-002 makes an approved amount immutable, and a basket that could quietly
                // reprice it would make the price list advisory.
                var charge = await _fees.PostChargeAsync(
                    request.StudentId, payer.Id, enrollment!.GradeYearProfileId, categoryId,
                    ChargeSourceType.Registration, cancellationToken);
                postedIds.Add(charge.Id);
                postedGross += charge.GrossAmount;
            }

            if (request.ExtraItem is ManualFeeItem extra)
            {
                _audit.Reason = extra.Reason;
                var charge = await _fees.PostManualChargeAsync(
                    request.StudentId, payer.Id, extra.FeeCategoryId, extra.Amount, cancellationToken);
                postedIds.Add(charge.Id);
                postedGross += charge.GrossAmount;
            }

            int? planAssignmentId = null;
            var installmentCount = 0;
            if (request.PlanTemplateId is int templateId)
            {
                // After the charges: BR-INS-002 generates the schedule from what is posted, so a
                // plan chosen in the same basket must see the items chosen beside it.
                var assignment = await _installments.AssignPlanAsync(
                    request.StudentId, payer.Id, templateId, request.WeekendDays, cancellationToken: cancellationToken);
                planAssignmentId = assignment.Id;
                installmentCount = assignment.Installments.Count;
            }

            int? grantId = null;
            var discountApplied = 0m;
            if (request.Discount is DiscountRequest discount)
            {
                // Last: approving the grant issues its discount documents against the charges
                // above (BR-DIS-005) and reduces the forward installments the plan just created
                // (BR-INS-003). Proposing and approving together is what makes the number the
                // clerk was shown the number the family is charged — a grant left Proposed
                // reduces nothing at all.
                _audit.Reason = discount.Reason;
                var grant = await _discounts.ProposeManualGrantAsync(
                    request.StudentId, discount.DiscountTypeId, discount.BasisValue, discount.Reason,
                    _currentUser.UserId, discount.HasHardshipDocumentation, cancellationToken);
                await _discounts.ApproveGrantAsync(grant.Id, _currentUser.UserId, cancellationToken: cancellationToken);
                grantId = grant.Id;

                // Sum() over a decimal column throws on Sqlite; materialize, then add in memory.
                discountApplied = (await _db.DiscountDocuments
                    .Where(d => d.DiscountGrantId == grant.Id)
                    .Select(d => d.Amount)
                    .ToListAsync(cancellationToken)).Sum();
            }

            await transaction.CommitAsync(cancellationToken);
            return new StudentFeeFileCommitResult(postedIds, postedGross, planAssignmentId, installmentCount, grantId, discountApplied);
        }

        public async Task<CreditNote> AdjustItemAsync(int chargeId, decimal newGrossAmount, string reason, CancellationToken cancellationToken = default)
        {
            if (newGrossAmount < 0m)
            {
                throw new FeeItemAdjustmentNotLowerException(chargeId);
            }

            var relievable = await RelievableAsync(chargeId, cancellationToken);
            var reduction = relievable - newGrossAmount;
            if (reduction <= 0m)
            {
                throw new FeeItemAdjustmentNotLowerException(chargeId);
            }

            _audit.Reason = reason;
            return await _fees.IssueCreditNoteAsync(chargeId, reduction, reason, cancellationToken);
        }

        public async Task<CreditNote> RemoveItemAsync(int chargeId, string reason, CancellationToken cancellationToken = default)
        {
            var relievable = await RelievableAsync(chargeId, cancellationToken);
            if (relievable <= 0m)
            {
                throw new ChargeAlreadyFullyRelievedException(chargeId);
            }

            _audit.Reason = reason;
            return await _fees.IssueCreditNoteAsync(chargeId, relievable, reason, cancellationToken);
        }

        /// <summary>
        /// What is still standing on the item: gross, less what credit notes and discount
        /// documents have already taken off it.
        /// <para>
        /// The discount half matters. <c>IssueCreditNoteAsync</c> caps a note at gross less prior
        /// notes, which is right for it and wrong here: crediting the gross of an item a
        /// scholarship already discounted would relieve it twice and leave the family in credit by
        /// the discount. Payments are deliberately not subtracted — money received is a fact about
        /// the payer's account, not a reduction of what was billed, and netting it off here would
        /// silently shrink the reversal a paid item deserves.
        /// </para>
        /// </summary>
        private async Task<decimal> RelievableAsync(int chargeId, CancellationToken cancellationToken)
        {
            var charge = await _db.Charges.AsNoTracking().SingleOrDefaultAsync(c => c.Id == chargeId, cancellationToken);
            if (charge == null || charge.Status != ChargeStatus.Posted)
            {
                throw new ChargeNotPostedException(chargeId);
            }

            var credited = (await _db.CreditNotes.Where(n => n.ChargeId == chargeId).Select(n => n.Amount).ToListAsync(cancellationToken)).Sum();
            var discounted = (await _db.DiscountDocuments.Where(d => d.ChargeId == chargeId).Select(d => d.Amount).ToListAsync(cancellationToken)).Sum();
            return charge.GrossAmount - credited - discounted;
        }
    }
}
