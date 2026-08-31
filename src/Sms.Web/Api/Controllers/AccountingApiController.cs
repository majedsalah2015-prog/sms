using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.GlExport;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;
using Sms.Web.Api.Models;
using Sms.Web.Security;

namespace Sms.Web.Api.Controllers
{
    /// <summary>
    /// The attached ledger, read-only — the chart of accounts, the trial balance
    /// headline, what the books say the school earned and spent, and the entries
    /// lately posted.
    /// <para>
    /// <b>Read-only, and that is the design rather than a first slice.</b> This
    /// system bills and collects; the accounting product keeps the books. The
    /// one write that crosses the line is the GL export batch, which is built
    /// from this system's own documents and already has its own screen. A
    /// journal entry authored from the school's side belongs in the accounting
    /// product's own screens, and adding a posting endpoint here would put two
    /// products in charge of one ledger.
    /// </para>
    /// <para>
    /// <b>Permissions.</b> These endpoints reuse <c>Fees/GlExport</c> and
    /// <c>Dashboards/Statistics</c> rather than declaring accounting permissions
    /// of their own. That is deliberate: whoever may build and export the GL
    /// batch is exactly who may read the ledger it lands in, and the result and
    /// trend figures are the ones the statistics screen already shows to the
    /// same audience. It also means no new <c>ScreenCatalog</c> entry, and
    /// therefore no re-run of the seeder before these work — a new permission is
    /// 404 for everybody, system administrator included, until the catalogue is
    /// seeded.
    /// </para>
    /// <para>
    /// <b>When no ledger is attached</b> — a standalone school system without
    /// <c>Sms.Erp.Bridge</c> — every endpoint here answers <c>503</c> with
    /// <c>ledger_not_attached</c>. It must never answer zero: "the books are
    /// empty" and "nobody asked the books" are different statements and only one
    /// of them is ever true.
    /// </para>
    /// </summary>
    [Route(V1 + "/accounting")]
    public sealed class AccountingApiController : ApiControllerBase
    {
        /// <summary>Null when the bridge is not registered. See the class summary.</summary>
        private readonly IGlLedgerInsight? _ledger;

        private readonly IGlLedgerSummary? _summary;
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IWorkingYearContext _workingYear;

        public AccountingApiController(
            AppDbContext db,
            IClock clock,
            IWorkingYearContext workingYear,
            IGlLedgerInsight? ledger = null,
            IGlLedgerSummary? summary = null)
        {
            _db = db;
            _clock = clock;
            _workingYear = workingYear;
            _ledger = ledger;
            _summary = summary;
        }

        /// <summary>
        /// Whether a ledger is attached at all, so the app can hide its
        /// accounting tab rather than show a section that answers 503.
        /// </summary>
        [HttpGet("status")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.View)]
        public ActionResult<ApiLedgerStatus> Status() => new ApiLedgerStatus
        {
            IsAttached = _ledger != null,
            SupportsResultSummary = _summary != null,
        };

        /// <summary>Every active, postable account — codes, names and classification.</summary>
        [HttpGet("accounts")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiGlAccount>>> Accounts()
        {
            if (_ledger == null)
            {
                return NotAttached();
            }

            var accounts = await _ledger.GetChartAsync(Ct);
            return accounts
                .Select(a => new ApiGlAccount
                {
                    Code = a.Code,
                    Name = a.Name,
                    Nature = a.Nature.ToString(),
                })
                .ToList();
        }

        /// <summary>
        /// The trial balance's two column totals as of a date. Double entry means
        /// they must agree; <c>isBalanced</c> is reported rather than asserted so
        /// a reader can trust the figure in front of them.
        /// </summary>
        [HttpGet("trial-balance")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.View)]
        public async Task<ActionResult<ApiTrialBalance>> TrialBalance(DateTime? asOf = null)
        {
            if (_ledger == null)
            {
                return NotAttached();
            }

            var totals = await _ledger.GetTrialBalanceAsync(asOf ?? _clock.UtcNow.Date, Ct);
            return new ApiTrialBalance
            {
                AsOf = asOf ?? _clock.UtcNow.Date,
                Debit = totals.Debit,
                Credit = totals.Credit,
                Difference = totals.Difference,
                IsBalanced = totals.IsBalanced,
                AccountCount = totals.AccountCount,
                Currency = await CurrencyAsync(),
            };
        }

        /// <summary>The net balance of the named accounts as of a date. Unknown codes contribute nothing.</summary>
        [HttpGet("accounts/balance")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.View)]
        public async Task<ActionResult<ApiMoney>> AccountsBalance([FromQuery] string[] codes, DateTime? asOf = null)
        {
            if (_ledger == null)
            {
                return NotAttached();
            }

            if (codes == null || codes.Length == 0)
            {
                return Refuse(422, "no_account_codes",
                    "Name at least one account code.", "حدّد رمز حساب واحداً على الأقل.");
            }

            var balance = await _ledger.GetAccountsBalanceAsync(codes, asOf ?? _clock.UtcNow.Date, Ct);
            return new ApiMoney(balance, await CurrencyAsync());
        }

        /// <summary>The most recently dated posted entries.</summary>
        [HttpGet("entries/recent")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiGlEntry>>> RecentEntries(int count = 20)
        {
            if (_ledger == null)
            {
                return NotAttached();
            }

            var entries = await _ledger.GetRecentEntriesAsync(Clamp(count), Ct);
            return await DescribeAsync(entries);
        }

        /// <summary>Entries somebody started and nobody posted, oldest first — what has been waiting longest.</summary>
        [HttpGet("entries/drafts")]
        [RequirePermission(ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.GlExport, ActionVerb.View)]
        public async Task<ActionResult<IReadOnlyList<ApiGlEntry>>> DraftEntries(int count = 20)
        {
            if (_ledger == null)
            {
                return NotAttached();
            }

            var entries = await _ledger.GetDraftEntriesAsync(Clamp(count), Ct);
            return await DescribeAsync(entries);
        }

        /// <summary>
        /// What the books say the school earned and spent over a period, and the
        /// same by month — the figures the statistics screen shows, which is why
        /// this one carries the statistics permission rather than the GL export
        /// one. Only posted entries count: a draft is an intention, and letting
        /// one move a headline number would make this disagree with the trial
        /// balance.
        /// </summary>
        [HttpGet("result")]
        [RequirePermission(ScreenCatalog.Modules.Dashboards, ScreenCatalog.Dashboards.Statistics, ActionVerb.View)]
        public async Task<ActionResult<ApiLedgerResult>> Result(DateTime? from = null, DateTime? to = null, int months = 12)
        {
            if (_summary == null)
            {
                return NotAttached();
            }

            // Defaulted to the working academic year rather than to a calendar year:
            // a school reads its own result against the year it is teaching.
            var year = await _db.AcademicYears.AsNoTracking()
                .Where(y => y.Id == _workingYear.AcademicYearId)
                .Select(y => new { y.StartDate, y.EndDate })
                .FirstOrDefaultAsync(Ct);

            var fromDate = from ?? year?.StartDate ?? new DateTime(_clock.UtcNow.Year, 1, 1);
            var toDate = to ?? year?.EndDate ?? _clock.UtcNow.Date;

            var result = await _summary.GetResultAsync(fromDate, toDate, Ct);
            var buckets = await _summary.GetMonthlyResultAsync(
                new DateTime(fromDate.Year, fromDate.Month, 1), months <= 0 ? 12 : Math.Min(months, 36), Ct);

            return new ApiLedgerResult
            {
                FromDate = fromDate,
                ToDate = toDate,
                Currency = await CurrencyAsync(),
                Revenue = result.Revenue,
                Expenses = result.Expenses,
                Net = result.Net,
                Months = buckets
                    .Select(b => new ApiLedgerMonth
                    {
                        Year = b.Year,
                        Month = b.Month,
                        Revenue = b.Revenue,
                        Expenses = b.Expenses,
                        Net = b.Net,
                    })
                    .ToList(),
            };
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// 503, not 404 and not an empty list. The endpoint exists and the caller
        /// is allowed to ask; the deployment simply has no ledger behind it, and
        /// that is a fact the app should show rather than a number it should
        /// invent.
        /// </summary>
        private ObjectResult NotAttached()
            => Refuse(503, "ledger_not_attached",
                "No accounting ledger is attached to this deployment.",
                "لا يوجد دفتر أستاذ محاسبي مرتبط بهذا التركيب.");

        private static int Clamp(int count) => count < 1 ? 20 : count > 100 ? 100 : count;

        private async Task<List<ApiGlEntry>> DescribeAsync(IReadOnlyList<GlEntrySummary> entries)
        {
            var currency = await CurrencyAsync();
            return entries
                .Select(e => new ApiGlEntry
                {
                    Number = e.Number,
                    EntryDate = e.EntryDate,
                    Description = e.Description,
                    Reference = e.Reference,
                    SourceModule = e.SourceModule,
                    Amount = e.Amount,
                    Currency = currency,
                    State = e.State.ToString(),
                    CreatedBy = e.CreatedBy,
                })
                .ToList();
        }

        private async Task<string> CurrencyAsync()
            => await _db.Schools.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == _db.CurrentSchoolId)
                .Select(s => s.CurrencyCode)
                .SingleOrDefaultAsync(Ct) ?? string.Empty;
    }
}
