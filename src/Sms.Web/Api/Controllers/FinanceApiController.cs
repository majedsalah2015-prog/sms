using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.Installments;
using Sms.Application.Payments;
using Sms.Application.Security;
using Sms.Application.Statements;
using Sms.Domain.Fees;
using Sms.Domain.Payments;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Web.Api.Models;
using Sms.Web.Security;

namespace Sms.Web.Api.Controllers
{
    /// <summary>
    /// The school's own money — doc/Modules/19 (fees), 20 (instalments) and 21
    /// (payments) for the app, over the same ports the counter screens use.
    /// <para>
    /// Nothing here computes a balance of its own.
    /// <see cref="IStatementService"/> and
    /// <see cref="IFeeAdmin.ComputeStudentPositionAsync"/> are the single
    /// central computation BR-FEE-008 requires, and a second arithmetic on a
    /// second transport is how a phone and a printed statement start disagreeing
    /// about what a family owes.
    /// </para>
    /// <para>
    /// This is school finance, not accounting. The general ledger is behind
    /// <c>/api/v1/accounting</c> and is read-only.
    /// </para>
    /// </summary>
    [Route(V1 + "/finance")]
    public sealed class FinanceApiController : ApiControllerBase
    {
        private readonly IFeeAdmin _fees;
        private readonly IPaymentAdmin _payments;
        private readonly IInstallmentAdmin _installments;
        private readonly IStatementService _statements;
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IWorkingYearContext _workingYear;

        public FinanceApiController(
            IFeeAdmin fees,
            IPaymentAdmin payments,
            IInstallmentAdmin installments,
            IStatementService statements,
            AppDbContext db,
            IClock clock,
            IWorkingYearContext workingYear)
        {
            _fees = fees;
            _payments = payments;
            _installments = installments;
            _statements = statements;
            _db = db;
            _clock = clock;
            _workingYear = workingYear;
        }

        // ---------------------------------------------------------------- fee catalogue

        /// <summary>The school's fee categories.</summary>
        [HttpGet("fee-categories")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Categories, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiFeeCategory>>> FeeCategories(bool includeInactive = false)
        {
            // IgnoreQueryFilters when the caller asks for the retired ones too: the
            // filtered list answers "what may I charge today", the unfiltered one
            // answers "what does this old charge refer to". Different questions.
            var query = includeInactive
                ? _db.FeeCategories.IgnoreQueryFilters().AsNoTracking().Where(c => c.SchoolId == _db.CurrentSchoolId)
                : _db.FeeCategories.AsNoTracking();

            var categories = await query.OrderBy(c => c.NameEn).ToListAsync(Ct);

            return categories
                .Select(c => new ApiFeeCategory
                {
                    FeeCategoryId = c.Id,
                    NameAr = c.NameAr,
                    NameEn = c.NameEn,
                    VatRate = c.VatRate,
                    IsMandatory = c.IsMandatory,
                    IsRefundable = c.IsRefundable,
                    IsServiceLinked = c.IsServiceLinked,
                    IsActive = c.IsActive,
                })
                .ToList();
        }

        /// <summary>The fee structure for a year — what each grade is charged for each category.</summary>
        [HttpGet("fee-structure")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Structure, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiFeeStructureLine>>> FeeStructure(int? academicYearId = null, int? gradeYearProfileId = null)
        {
            var yearId = academicYearId ?? _workingYear.AcademicYearId;
            var query = _db.FeeStructureLines.AsNoTracking().Where(l => l.AcademicYearId == yearId);

            if (gradeYearProfileId.HasValue)
            {
                query = query.Where(l => l.GradeYearProfileId == gradeYearProfileId.Value);
            }

            var lines = await query.ToListAsync(Ct);
            if (lines.Count == 0)
            {
                return Array.Empty<ApiFeeStructureLine>();
            }

            var categories = await CategoryNamesAsync(lines.Select(l => l.FeeCategoryId).Distinct().ToList());
            var grades = await GradeNamesAsync(lines.Select(l => l.GradeYearProfileId).Distinct().ToList());
            var currency = await CurrencyAsync();

            return lines
                .Select(l =>
                {
                    categories.TryGetValue(l.FeeCategoryId, out var category);
                    grades.TryGetValue(l.GradeYearProfileId, out var grade);
                    return new ApiFeeStructureLine
                    {
                        FeeStructureLineId = l.Id,
                        AcademicYearId = l.AcademicYearId,
                        GradeYearProfileId = l.GradeYearProfileId,
                        GradeCode = grade.Code,
                        GradeName = grade.Name,
                        FeeCategoryId = l.FeeCategoryId,
                        CategoryNameAr = category.Ar ?? string.Empty,
                        CategoryNameEn = category.En ?? string.Empty,
                        Amount = l.Amount,
                        Currency = currency,
                        Status = l.Status.ToString(),
                    };
                })
                .OrderBy(l => l.GradeCode).ThenBy(l => l.CategoryNameEn)
                .ToList();
        }

        // ---------------------------------------------------------------- the student's money

        /// <summary>
        /// What this student owes — the single central computation of BR-FEE-008,
        /// asked of the fee port rather than recomputed here.
        /// </summary>
        [HttpGet("students/{studentId:int}/position")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.StudentFinance, ActionVerb.View)]
        public async Task<ActionResult<ApiMoney>> StudentPosition(int studentId)
        {
            var position = await _fees.ComputeStudentPositionAsync(studentId, Ct);
            return new ApiMoney(position, await CurrencyAsync());
        }

        /// <summary>
        /// The per-student statement (doc/Modules/19 §8.7). Payments appear as
        /// <b>allocations</b>, because allocating is the only step that says
        /// which child a riyal paid for — a receipt still unallocated is family
        /// money and belongs on the payer statement, not on a child's.
        /// </summary>
        [HttpGet("students/{studentId:int}/statement")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.StudentFinance, ActionVerb.View)]
        public async Task<ActionResult<ApiStatement>> StudentStatement(int studentId, DateTime? asOfUtc = null)
        {
            var statement = await _statements.BuildForStudentAsync(studentId, asOfUtc, Ct);
            var result = await DescribeAsync(statement, asOfUtc);
            result.StudentId = studentId;
            return result;
        }

        /// <summary>The payer statement — one family, one balance (BR-DIS-010).</summary>
        [HttpGet("payers/{payerId:int}/statement")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Position, ActionVerb.View)]
        public async Task<ActionResult<ApiStatement>> PayerStatement(int payerId, DateTime? asOfUtc = null)
        {
            var statement = await _statements.BuildAsync(payerId, asOfUtc, Ct);
            var result = await DescribeAsync(statement, asOfUtc);
            result.PayerId = payerId;
            return result;
        }

        /// <summary>A student's charges, newest first. Void charges are included and labelled — a voided charge is a fact the counter has to be able to explain.</summary>
        [HttpGet("students/{studentId:int}/charges")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.View)]
        public async Task<ActionResult<ApiPage<ApiCharge>>> Charges(int studentId, int? page = null, int? pageSize = null)
        {
            var (p, size) = ApiPaging.Clamp(page, pageSize);
            var query = _db.Charges.AsNoTracking().Where(c => c.StudentId == studentId);

            var total = await query.CountAsync(Ct);
            var charges = await query
                .OrderByDescending(c => c.PostedAtUtc).ThenByDescending(c => c.Id)
                .Skip(ApiPaging.Skip(p, size))
                .Take(size)
                .ToListAsync(Ct);

            var categories = await CategoryNamesAsync(charges.Select(c => c.FeeCategoryId).Distinct().ToList());
            var currency = await CurrencyAsync();

            var rows = charges
                .Select(c =>
                {
                    categories.TryGetValue(c.FeeCategoryId, out var category);
                    return new ApiCharge
                    {
                        ChargeId = c.Id,
                        ChargeNo = c.ChargeNo,
                        StudentId = c.StudentId,
                        PayerId = c.PayerId,
                        FeeCategoryId = c.FeeCategoryId,
                        CategoryNameAr = category.Ar ?? string.Empty,
                        CategoryNameEn = category.En ?? string.Empty,
                        NetAmount = c.NetAmount,
                        VatAmount = c.VatAmount,
                        GrossAmount = c.GrossAmount,
                        VatRateSnapshot = c.VatRateSnapshot,
                        Currency = currency,
                        Status = c.Status.ToString(),
                        PostedAtUtc = c.PostedAtUtc,
                    };
                })
                .ToList();

            return Page<ApiCharge>(rows, p, size, total);
        }

        /// <summary>
        /// Posts a charge from an approved fee structure line. Refused when no
        /// approved line covers the student's grade-year × category — an
        /// unapproved price is not a price.
        /// </summary>
        [HttpPost("charges")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.Post)]
        public async Task<ActionResult<ApiCharge>> PostCharge([FromBody] ApiPostChargeRequest request)
        {
            var line = await _db.FeeStructureLines.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == request.FeeStructureLineId, Ct);
            if (line == null)
            {
                return NotFoundError();
            }

            var payerId = await PayerForStudentAsync(request.StudentId);
            if (payerId == null)
            {
                return Refuse(409, "student_has_no_payer",
                    "This student has no financially responsible guardian to charge.",
                    "لا يوجد لهذا الطالب ولي أمر مسؤول مالياً لتحميله الرسم.");
            }

            // The same guard the counter screen applies. It lives in the controller
            // rather than in IFeeAdmin, so an API that skipped it would let a phone
            // double-charge a family where the browser refuses — two transports
            // disagreeing about the same rule is worse than either answer.
            var alreadyCharged = await _db.Charges.AsNoTracking().AnyAsync(
                c => c.StudentId == request.StudentId
                    && c.FeeCategoryId == line.FeeCategoryId
                    && c.AcademicYearId == line.AcademicYearId
                    && c.Status == ChargeStatus.Posted
                    && c.SourceType != ChargeSourceType.Manual,
                Ct);
            if (alreadyCharged)
            {
                return Refuse(409, "category_already_charged",
                    "This category is already charged for the student this year.",
                    "هذه الفئة مفوترة للطالب هذا العام مسبقاً.");
            }

            var source = Enum.TryParse<ChargeSourceType>(request.SourceType, ignoreCase: true, out var parsed)
                && parsed is ChargeSourceType.Registration or ChargeSourceType.ReRegistration or ChargeSourceType.ServiceAssignment
                ? parsed
                : ChargeSourceType.Registration;

            var charge = await _fees.PostChargeAsync(
                request.StudentId, payerId.Value, line.GradeYearProfileId, line.FeeCategoryId, source, Ct);

            var categories = await CategoryNamesAsync(new[] { charge.FeeCategoryId });
            categories.TryGetValue(charge.FeeCategoryId, out var category);

            return new ApiCharge
            {
                ChargeId = charge.Id,
                ChargeNo = charge.ChargeNo,
                StudentId = charge.StudentId,
                PayerId = charge.PayerId,
                FeeCategoryId = charge.FeeCategoryId,
                CategoryNameAr = category.Ar ?? string.Empty,
                CategoryNameEn = category.En ?? string.Empty,
                NetAmount = charge.NetAmount,
                VatAmount = charge.VatAmount,
                GrossAmount = charge.GrossAmount,
                VatRateSnapshot = charge.VatRateSnapshot,
                Currency = await CurrencyAsync(),
                Status = charge.Status.ToString(),
                PostedAtUtc = charge.PostedAtUtc,
            };
        }

        /// <summary>
        /// BR-FEE-003 / BR-GLB-062: a posted charge is immutable, so a
        /// correction is a credit note against it and never an edit.
        /// </summary>
        [HttpPost("charges/{chargeId:int}/credit-notes")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, ActionVerb.Deactivate)]
        public async Task<IActionResult> IssueCreditNote(int chargeId, [FromBody] ApiCreditNoteRequest request)
        {
            var note = await _fees.IssueCreditNoteAsync(chargeId, request.Amount, request.Reason.Trim(), Ct);
            return Ok(new { creditNoteId = note.Id });
        }

        // ---------------------------------------------------------------- instalments

        /// <summary>A student's instalment schedule. The status is derived (BR-INS-007), never stored.</summary>
        [HttpGet("students/{studentId:int}/installments")]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Schedule, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiInstallment>>> Installments(int studentId)
        {
            var assignment = await _db.PlanAssignments.AsNoTracking()
                .Where(a => a.StudentId == studentId && a.AcademicYearId == _workingYear.AcademicYearId)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(Ct);
            if (assignment == null)
            {
                return Array.Empty<ApiInstallment>();
            }

            var schedule = await _installments.GetScheduleAsync(assignment.Id, Ct);
            var currency = await CurrencyAsync();

            return schedule
                .Select(i => new ApiInstallment
                {
                    InstallmentId = i.InstallmentId,
                    SequenceNumber = i.SequenceNumber,
                    DueDate = i.DueDate,
                    Amount = i.Amount,
                    Paid = i.Paid,
                    Outstanding = i.Amount - i.Paid,
                    Status = i.Status.ToString(),
                    IsPdcCovered = i.IsPdcCovered,
                    Currency = currency,
                })
                .ToList();
        }

        // ---------------------------------------------------------------- the counter

        /// <summary>
        /// Takes a payment. BR-PAY-003: the receipt number is issued on this
        /// call's own commit and the money is auto-allocated oldest-due-first
        /// across the payer's open charges; anything left over becomes advance
        /// balance rather than being refused.
        /// </summary>
        [HttpPost("receipts")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Cashier, ActionVerb.Create)]
        public async Task<ActionResult<ApiReceipt>> CaptureReceipt([FromBody] ApiCaptureReceiptRequest request)
        {
            if (!Enum.TryParse<PaymentMethod>(request.Method, ignoreCase: true, out var method))
            {
                return Refuse(422, "invalid_payment_method",
                    "Payment method must be Cash, Card, BankTransfer, Cheque or Pdc.",
                    "طريقة الدفع يجب أن تكون نقداً أو بطاقة أو حوالة بنكية أو شيكاً أو شيكاً آجلاً.");
            }

            var receipt = await _payments.CaptureReceiptAsync(
                request.PayerId, method, request.Amount, request.TillSessionId, request.MethodRefNo,
                request.CollectionAccountId, Ct);

            return new ApiReceipt
            {
                ReceiptId = receipt.Id,
                ReceiptNo = receipt.ReceiptNo,
                PayerId = receipt.PayerId,
                Method = receipt.Method.ToString(),
                MethodRefNo = receipt.MethodRefNo,
                Amount = receipt.Amount,
                Currency = await CurrencyAsync(),
                Status = receipt.Status.ToString(),
                CollectionAccountId = receipt.CollectionAccountId,
                IssuedAtUtc = receipt.IssuedAtUtc,
            };
        }

        /// <summary>
        /// The school's own accounts a payment may be collected into — the bank
        /// accounts a parent transfers to, and the cash boxes the counter takes
        /// notes into (BR-PAY-002).
        /// <para>
        /// Retired accounts are excluded: they are kept so old receipts still
        /// read back, not so new money can be put in them. The account number
        /// travels with each one because a client's whole reason for asking is
        /// to tell a parent where to send the money.
        /// </para>
        /// </summary>
        [HttpGet("collection-accounts")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Cashier, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiCollectionAccount>>> CollectionAccounts()
        {
            var accounts = await _db.CollectionAccounts.AsNoTracking()
                .OrderByDescending(a => a.IsDefault).ThenBy(a => a.DisplayOrder).ThenBy(a => a.Code)
                .ToListAsync(Ct);

            // The bank is usually a catalogue value rather than typed text, and a client shown a
            // blank bank beside an IBAN has been told less than the screen tells. IgnoreQueryFilters
            // because a retired catalogue value must still be readable on an account that names it.
            var lookupIds = accounts.Where(a => a.BankLookupId != null).Select(a => a.BankLookupId!.Value).Distinct().ToList();
            var bankNames = lookupIds.Count == 0
                ? new Dictionary<int, string>()
                : (await _db.LookupValues.IgnoreQueryFilters().AsNoTracking()
                    .Where(v => lookupIds.Contains(v.Id) && v.SchoolId == _db.CurrentSchoolId)
                    .Select(v => new { v.Id, v.Name.NameAr, v.Name.NameEn })
                    .ToListAsync(Ct))
                    .ToDictionary(v => v.Id, v => string.IsNullOrWhiteSpace(v.NameAr) ? v.NameEn : v.NameAr);

            return accounts.Select(a => new ApiCollectionAccount
            {
                CollectionAccountId = a.Id,
                Code = a.Code,
                NameAr = a.NameAr,
                NameEn = a.NameEn,
                Kind = a.Kind.ToString(),
                BankName = a.BankLookupId != null && bankNames.TryGetValue(a.BankLookupId.Value, out var bank) ? bank : a.BankName,
                AccountNo = a.AccountNo,
                Iban = a.Iban,
                IsDefault = a.IsDefault,
            }).ToList();
        }

        /// <summary>A payer's receipts, newest first.</summary>
        [HttpGet("payers/{payerId:int}/receipts")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Cashier, ActionVerb.View)]
        public async Task<ActionResult<ApiPage<ApiReceipt>>> Receipts(int payerId, int? page = null, int? pageSize = null)
        {
            var (p, size) = ApiPaging.Clamp(page, pageSize);
            var query = _db.Receipts.AsNoTracking().Where(r => r.PayerId == payerId);

            var total = await query.CountAsync(Ct);
            var receipts = await query
                .OrderByDescending(r => r.IssuedAtUtc).ThenByDescending(r => r.Id)
                .Skip(ApiPaging.Skip(p, size))
                .Take(size)
                .ToListAsync(Ct);

            var currency = await CurrencyAsync();

            var rows = receipts
                .Select(r => new ApiReceipt
                {
                    ReceiptId = r.Id,
                    ReceiptNo = r.ReceiptNo,
                    PayerId = r.PayerId,
                    Method = r.Method.ToString(),
                    MethodRefNo = r.MethodRefNo,
                    Amount = r.Amount,
                    Currency = currency,
                    Status = r.Status.ToString(),
                    IssuedAtUtc = r.IssuedAtUtc,
                })
                .ToList();

            return Page<ApiReceipt>(rows, p, size, total);
        }

        /// <summary>
        /// Opens a till session. Defaults the cashier to the caller — a cashier opens their own
        /// drawer — and the till to the one the server assigns when the request names none
        /// (BR-PAY-001). Refuses a cashier who is already at a drawer, or a named till that is.
        /// </summary>
        [HttpPost("till/open")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Till, ActionVerb.Create)]
        public async Task<IActionResult> OpenTill([FromBody] ApiOpenTillRequest request)
        {
            var session = await _payments.OpenTillSessionAsync(
                request.CashierUserId ?? CurrentUserAccountId, request.TillCode, request.FloatAmount, Ct);

            return Ok(new { tillSessionId = session.Id, tillCode = session.TillCode });
        }

        /// <summary>Closes a till against a counted total. The system total is this session's receipts.</summary>
        [HttpPost("till/{tillSessionId:int}/close")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Till, ActionVerb.Post)]
        public async Task<IActionResult> CloseTill(int tillSessionId, [FromBody] ApiCloseTillRequest request)
        {
            await _payments.CloseTillSessionAsync(tillSessionId, request.CountedTotal, request.VarianceReason, Ct);
            return NoContent();
        }

        // ------------------------------------------------------------------ helpers

        private async Task<ApiStatement> DescribeAsync(PayerStatement statement, DateTime? asOfUtc)
            => new()
            {
                AsOfUtc = asOfUtc ?? _clock.UtcNow,
                Currency = await CurrencyAsync(),
                GrossCharges = statement.GrossCharges,
                Discounts = statement.Discounts,
                CreditNotes = statement.CreditNotes,
                Payments = statement.Payments,
                Refunds = statement.Refunds,
                NetCharges = statement.NetCharges,
                ClosingBalance = statement.ClosingBalance,
                Lines = statement.Lines
                    .Select(l => new ApiStatementLine
                    {
                        DateUtc = l.DateUtc,
                        Kind = l.Kind.ToString(),
                        DocumentNo = l.DocumentNo,
                        Description = l.Description,
                        Debit = l.Debit,
                        Credit = l.Credit,
                        RunningBalance = l.RunningBalance,
                    })
                    .ToList(),
            };

        /// <summary>
        /// The payer a charge lands on: the student's financially responsible
        /// guardian. BR-GLB-004 guarantees there is one on a live student, but a
        /// record part-way through data entry may not have it yet, and that is a
        /// refusal the counter can act on rather than a 500.
        /// </summary>
        private async Task<int?> PayerForStudentAsync(int studentId)
        {
            var parentId = await _db.StudentGuardianLinks.AsNoTracking()
                .Where(l => l.StudentId == studentId && l.EffectiveToUtc == null && l.IsFinanciallyResponsible)
                .Select(l => (int?)l.ParentId)
                .FirstOrDefaultAsync(Ct);
            if (parentId == null)
            {
                return null;
            }

            return await _db.Payers.AsNoTracking()
                .Where(p => p.ParentId == parentId.Value)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(Ct);
        }

        /// <summary>
        /// Category names read through <c>IgnoreQueryFilters</c>: a deactivated
        /// category still names every charge already posted under it, and
        /// reading it through the soft-active filter is how a statement dies the
        /// day a school retires one.
        /// </summary>
        private async Task<Dictionary<int, (string Ar, string En)>> CategoryNamesAsync(IReadOnlyList<int> ids)
        {
            if (ids.Count == 0)
            {
                return new Dictionary<int, (string, string)>();
            }

            var rows = await _db.FeeCategories.IgnoreQueryFilters().AsNoTracking()
                .Where(c => ids.Contains(c.Id) && c.SchoolId == _db.CurrentSchoolId)
                .Select(c => new { c.Id, c.NameAr, c.NameEn })
                .ToListAsync(Ct);

            return rows.ToDictionary(r => r.Id, r => (r.NameAr, r.NameEn));
        }

        /// <summary>Grade-year profile id → the grade's code and name, same soft-active reasoning.</summary>
        private async Task<Dictionary<int, (string? Code, string? Name)>> GradeNamesAsync(IReadOnlyList<int> profileIds)
        {
            var result = new Dictionary<int, (string?, string?)>();
            if (profileIds.Count == 0)
            {
                return result;
            }

            var profiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking()
                .Where(gp => profileIds.Contains(gp.Id))
                .Select(gp => new { gp.Id, gp.GradeLevelId })
                .ToListAsync(Ct);

            var gradeIds = profiles.Select(gp => gp.GradeLevelId).Distinct().ToList();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(g => gradeIds.Contains(g.Id) && g.SchoolId == _db.CurrentSchoolId)
                .Select(g => new { g.Id, g.Code, g.Name.NameAr, g.Name.NameEn })
                .ToListAsync(Ct);

            foreach (var profile in profiles)
            {
                var grade = grades.FirstOrDefault(g => g.Id == profile.GradeLevelId);
                result[profile.Id] = (grade?.Code, grade == null ? null : T(grade.NameEn, grade.NameAr));
            }

            return result;
        }

        private async Task<string> CurrencyAsync()
            => await _db.Schools.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == _db.CurrentSchoolId)
                .Select(s => s.CurrencyCode)
                .SingleOrDefaultAsync(Ct) ?? string.Empty;
    }
}
