using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Payments;
using Sms.Domain.Fees;
using Sms.Domain.Parents;
using Sms.Domain.Payments;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;
using Sms.Web.Models;

namespace Sms.Web.Finance
{
    /// <summary>
    /// Read-side helpers shared by FeesController and PaymentsController:
    /// payer cards (payer → parent → children), open-charge rows with the
    /// same subtraction set the engine uses (gross − credit notes − discounts
    /// − allocations, BR-FEE-008 / BR-DIS-005), and the advance balance
    /// (AdvanceBalanceCalculator). Pure queries — nothing here saves.
    /// </summary>
    public static class FinanceQueries
    {
        /// <summary>Payers matching a free-text query (parent name / file no / mobile / student no / student name), or all when blank.</summary>
        public static async Task<IReadOnlyList<PayerCard>> SearchPayersAsync(AppDbContext db, string? q, int take = 50, bool includeChildren = true)
        {
            q = q?.Trim();
            var parents = db.Parents.IgnoreQueryFilters().AsNoTracking().Where(p => p.SchoolId == db.CurrentSchoolId);
            HashSet<int>? parentFilter = null;
            if (!string.IsNullOrEmpty(q))
            {
                var byParent = await parents.Where(p => p.NameAr.Contains(q) || p.NameEn.Contains(q) || p.ParentFileNo.Contains(q) || p.PrimaryMobile.Contains(q)).Select(p => p.Id).ToListAsync();
                var byStudent = await db.Students.IgnoreQueryFilters().AsNoTracking()
                    .Where(s => s.SchoolId == db.CurrentSchoolId && (s.StudentNo.Contains(q) || s.FirstNameAr.Contains(q) || s.FamilyNameAr.Contains(q) || s.FirstNameEn.Contains(q) || s.FamilyNameEn.Contains(q)))
                    .Select(s => s.Id).ToListAsync();
                var viaStudents = byStudent.Count == 0 ? new List<int>() : await db.StudentGuardianLinks.AsNoTracking().Where(l => byStudent.Contains(l.StudentId) && l.EffectiveToUtc == null).Select(l => l.ParentId).ToListAsync();
                parentFilter = byParent.Concat(viaStudents).ToHashSet();
                if (parentFilter.Count == 0) return Array.Empty<PayerCard>();
            }

            var payers = await db.Payers.AsNoTracking().Where(p => parentFilter == null || (p.ParentId != null && parentFilter.Contains(p.ParentId.Value))).OrderByDescending(p => p.Id).Take(take).ToListAsync();
            return await CardsAsync(db, payers, includeChildren);
        }

        public static async Task<PayerCard?> CardAsync(AppDbContext db, int payerId)
        {
            var payer = await db.Payers.AsNoTracking().SingleOrDefaultAsync(p => p.Id == payerId);
            if (payer == null) return null;
            return (await CardsAsync(db, new[] { payer }, includeChildren: true)).FirstOrDefault();
        }

        public static async Task<IReadOnlyList<PayerCard>> CardsAsync(AppDbContext db, IReadOnlyCollection<Payer> payers, bool includeChildren)
        {
            var parentIds = payers.Where(p => p.ParentId != null).Select(p => p.ParentId!.Value).Distinct().ToList();
            var parents = await db.Parents.IgnoreQueryFilters().AsNoTracking().Where(p => parentIds.Contains(p.Id)).ToListAsync();
            var links = includeChildren
                ? await db.StudentGuardianLinks.AsNoTracking().Where(l => parentIds.Contains(l.ParentId) && l.EffectiveToUtc == null).ToListAsync()
                : new List<StudentGuardianLink>();
            var studentIds = links.Select(l => l.StudentId).Distinct().ToList();
            var students = await db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => studentIds.Contains(s.Id)).ToListAsync();
            // Charges can also name students the parent isn't (any longer) linked to — include them so the card is complete.
            if (includeChildren)
            {
                var payerIds = payers.Select(p => p.Id).ToList();
                var chargedStudentIds = await db.Charges.AsNoTracking().Where(c => payerIds.Contains(c.PayerId)).Select(c => new { c.PayerId, c.StudentId }).Distinct().ToListAsync();
                var missing = chargedStudentIds.Select(x => x.StudentId).Except(studentIds).ToList();
                if (missing.Count > 0) students.AddRange(await db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => missing.Contains(s.Id)).ToListAsync());
                return payers.Select(p =>
                {
                    var parent = parents.FirstOrDefault(x => x.Id == p.ParentId);
                    var kids = links.Where(l => l.ParentId == p.ParentId).Select(l => l.StudentId)
                        .Concat(chargedStudentIds.Where(x => x.PayerId == p.Id).Select(x => x.StudentId)).Distinct()
                        .Select(id => students.FirstOrDefault(s => s.Id == id)).Where(s => s != null).Select(s => s!).OrderBy(s => s.StudentNo).ToList();
                    return new PayerCard(p, parent, kids);
                }).ToList();
            }
            return payers.Select(p => new PayerCard(p, parents.FirstOrDefault(x => x.Id == p.ParentId), Array.Empty<Student>())).ToList();
        }

        /// <summary>Posted charges of a payer (or a student) with their remaining balance — the cashier's allocation targets.</summary>
        public static async Task<IReadOnlyList<OpenChargeRow>> ChargeRowsAsync(AppDbContext db, int? payerId = null, int? studentId = null, bool openOnly = true)
        {
            var query = db.Charges.AsNoTracking().Where(c => c.Status == ChargeStatus.Posted);
            if (payerId != null) query = query.Where(c => c.PayerId == payerId);
            if (studentId != null) query = query.Where(c => c.StudentId == studentId);
            var charges = await query.OrderBy(c => c.PostedAtUtc).ToListAsync();
            return await RowsAsync(db, charges, openOnly);
        }

        public static async Task<IReadOnlyList<OpenChargeRow>> RowsAsync(AppDbContext db, IReadOnlyList<Charge> charges, bool openOnly)
        {
            var ids = charges.Select(c => c.Id).ToList();
            var credits = (await db.CreditNotes.AsNoTracking().Where(n => ids.Contains(n.ChargeId)).Select(n => new { n.ChargeId, n.Amount }).ToListAsync()).GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var discounts = (await db.DiscountDocuments.AsNoTracking().Where(d => ids.Contains(d.ChargeId)).Select(d => new { d.ChargeId, d.Amount }).ToListAsync()).GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var allocations = (await db.PaymentAllocations.AsNoTracking().Where(a => ids.Contains(a.ChargeId)).Select(a => new { a.ChargeId, a.AllocatedAmount }).ToListAsync()).GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));
            var catIds = charges.Select(c => c.FeeCategoryId).Distinct().ToList();
            var cats = await db.FeeCategories.IgnoreQueryFilters().AsNoTracking().Where(c => catIds.Contains(c.Id)).ToListAsync();
            var stIds = charges.Select(c => c.StudentId).Distinct().ToList();
            var students = await db.Students.IgnoreQueryFilters().AsNoTracking().Where(s => stIds.Contains(s.Id)).ToListAsync();

            var rows = charges.Select(c => new OpenChargeRow(c,
                cats.FirstOrDefault(x => x.Id == c.FeeCategoryId) ?? new FeeCategory { NameAr = "?", NameEn = "?" },
                students.FirstOrDefault(s => s.Id == c.StudentId) ?? new Student { StudentNo = "?" },
                credits.GetValueOrDefault(c.Id), discounts.GetValueOrDefault(c.Id), allocations.GetValueOrDefault(c.Id))).ToList();
            return openOnly ? rows.Where(r => r.Remaining > 0).ToList() : rows;
        }

        /// <summary>Receipts − allocations for a payer (FeePayment purpose only, Posted only) — mirrors PaymentAdmin.ComputeRefundablePositionAsync's first half.</summary>
        public static async Task<decimal> AdvanceBalanceAsync(AppDbContext db, int payerId)
        {
            var receipts = await db.Receipts.AsNoTracking().Where(r => r.PayerId == payerId && r.Status == ReceiptStatus.Posted && r.Purpose == ReceiptPurpose.FeePayment).Select(r => new { r.Id, r.Amount }).ToListAsync();
            var ids = receipts.Select(r => r.Id).ToList();
            var allocated = (await db.PaymentAllocations.AsNoTracking().Where(a => ids.Contains(a.ReceiptId)).Select(a => a.AllocatedAmount).ToListAsync()).Sum();
            return AdvanceBalanceCalculator.Calculate(receipts.Sum(r => r.Amount), allocated);
        }

        /// <summary>Refunds already requested/approved/paid (anything not Rejected) — the engine subtracts these from the refundable position.</summary>
        public static async Task<decimal> CommittedRefundsAsync(AppDbContext db, int payerId)
            => (await db.RefundVouchers.AsNoTracking().Where(v => v.PayerId == payerId && v.Status != RefundVoucherStatus.Rejected).Select(v => v.Amount).ToListAsync()).Sum();

        /// <summary>Runs the engine's oldest-first allocation against the open rows without saving — the cashier's allocation preview.</summary>
        public static (IReadOnlyList<(OpenChargeRow Row, decimal Amount)> Lines, decimal Leftover) PreviewAllocation(decimal amount, IReadOnlyList<OpenChargeRow> open)
        {
            var (allocs, leftover) = PaymentAllocationEngine.Allocate(amount, open.Select(r => new PaymentAllocationEngine.AllocationTarget(r.Charge.Id, r.Remaining, r.Charge.PostedAtUtc)));
            return (allocs.Select(a => (open.First(r => r.Charge.Id == a.ChargeId), a.Amount)).ToList(), leftover);
        }
    }
}
