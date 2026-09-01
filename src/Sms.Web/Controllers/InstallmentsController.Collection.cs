using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Installments;
using Sms.Application.ReadModels;
using Sms.Application.Security;
using Sms.Domain.Installments;
using Sms.Domain.Schools;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/20 §8.5's other half — the collection follow-up roll — together
    /// with §10's "Overdue installments by payer/grade/bucket" and
    /// doc/Modules/19 §10's aged receivables, read from the student's side.
    /// <para>
    /// <b>What this screen is for.</b> <c>/installments/dunning</c> shows what the
    /// automatic ladder has already done. It cannot answer the question a school
    /// actually asks on the first of the month — "who owes us money that fell due
    /// between these two dates, and what are we going to do about it" — because it
    /// is a log, and a log lists events rather than families. This is the list, the
    /// file that leaves with it, and the two ways a human chases it:
    /// a printed notice, or one that lands in the family's portal inbox.
    /// </para>
    /// <para>
    /// <b>It does not touch the ladder.</b> <see cref="DunningStep"/> is the
    /// automatic sequence and <c>DunningLadderEvaluator</c> reads its highest fired
    /// step as the floor for the next one, so recording a hand-issued letter there
    /// would silently cancel the +3/+14/+30 notices BR-INS-008 requires. Manual
    /// notices go to their own append-only <c>ppl.CollectionNotice</c> log.
    /// </para>
    /// <para>
    /// <b>Deviation from doc/UI/02 §5 and BR-INS-008, stated not substituted.</b>
    /// The letter stage is specified as a numbered formal document in Module 18's
    /// pattern — a server-rendered PDF. The PDF engine remains an open owner
    /// decision (docs/Status gap O6: QuestPDF fails at bidi on .NET 5, the fix is
    /// net6+), and that plan's own fallback is "أو بمسار HTML قابل للطباعة". So the
    /// number is real and issued from series DUN, the log entry is real, and the
    /// sheet is a browser print — the same substitution the statement, the receipt
    /// and the report card already make. It is not a sealed document.
    /// </para>
    /// <para>
    /// Not built here, deliberately: the service-suspension list BR-INS-008 gates
    /// on per-school legal policy (doc §14 open question 2 — the product does not
    /// hold that policy, so a screen offering the action would be inventing it).
    /// </para>
    /// </summary>
    public partial class InstallmentsController
    {
        // ================================================================== §8.5 the roll

        [HttpGet("collection")]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Collection, ActionVerb.View)]
        public async Task<IActionResult> Collection(
            DateTime? from = null, DateTime? to = null, int? year = null,
            int? grade = null, int? section = null, string? q = null, AgingBucket? bucket = null, bool notifiable = false)
        {
            var m = await BuildCollectionAsync(from, to, year, grade, section, q, bucket, notifiable);
            return View(m);
        }

        // ================================================================== §8.5 the file

        /// <summary>
        /// The same roll as a file. CSV rather than a spreadsheet format because
        /// the destination is always somewhere else — a mail merge for the letters,
        /// a collection agency's list, the finance manager's own workbook — and all
        /// of them read CSV. Behind its own verb per doc/UI/02 P-LIST
        /// (BR-SEC-021): reading the school's arrears on screen and carrying them
        /// out of the building are different acts.
        /// </summary>
        [HttpGet("collection/export.csv")]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Collection, ActionVerb.Export)]
        public async Task<IActionResult> CollectionCsv(
            DateTime? from = null, DateTime? to = null, int? year = null,
            int? grade = null, int? section = null, string? q = null, AgingBucket? bucket = null, bool notifiable = false)
        {
            var filter = new CollectionFilter(from?.Date, to?.Date, year, grade, section, q, bucket, notifiable);
            CollectionRoll roll;
            try
            {
                // No page cap on the export: a truncated file is the one output nobody checks the
                // row count of, and an arrears list that silently stops at two hundred families is
                // worse than no file at all.
                roll = await _collection.GetRollAsync(filter, take: int.MaxValue, HttpContext.RequestAborted);
            }
            catch (InvalidCollectionWindowException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                return RedirectToAction(nameof(Collection), RouteFor(filter));
            }

            var ar = IsArabic;
            var records = new List<IEnumerable<string?>> { CollectionHeadings(ar) };
            records.AddRange(roll.Rows.Select(r => new[]
            {
                r.StudentNo,
                ar ? r.StudentNameAr : r.StudentNameEn,
                (ar ? r.GradeNameAr : r.GradeNameEn) ?? string.Empty,
                (ar ? r.SectionNameAr : r.SectionNameEn) ?? string.Empty,
                (ar ? r.GuardianNameAr : r.GuardianNameEn) ?? string.Empty,
                r.GuardianMobile ?? string.Empty,
                r.Position.ItemCount.ToString(CultureInfo.InvariantCulture),
                r.Position.OldestDueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                FinanceLabels.Aging(r.Bucket, ar),

                // Money leaves as invariant decimals, never as the culture's formatting. A
                // thousands separator turns one column into two the moment the file is opened.
                r.Position.Due.ToString("0.00", CultureInfo.InvariantCulture),
                r.Position.Outstanding.ToString("0.00", CultureInfo.InvariantCulture),
                r.Position.Notifiable.ToString("0.00", CultureInfo.InvariantCulture),
                r.LastNoticeAtUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                r.LastNoticeChannel == null ? string.Empty : CollectionLabels.Channel(r.LastNoticeChannel.Value, ar),
            }.AsEnumerable()));

            return File(
                CollectionExport.Bytes(records),
                "text/csv",
                $"collection-{_clock.UtcNow:yyyyMMdd-HHmm}.csv");
        }

        private static IReadOnlyList<string> CollectionHeadings(bool ar) => new[]
        {
            ar ? "رقم الطالب" : "Student no.",
            ar ? "الاسم" : "Name",
            ar ? "الصف" : "Grade",
            ar ? "الشعبة" : "Section",
            ar ? "ولي الأمر" : "Guardian",
            ar ? "الجوال" : "Mobile",
            ar ? "عدد المستحقات" : "Items due",
            ar ? "أقدم استحقاق" : "Oldest due date",
            ar ? "فئة التقادم" : "Aging bucket",
            ar ? "المستحق" : "Due",
            ar ? "المتبقي" : "Outstanding",
            ar ? "القابل للمطالبة" : "Chaseable",
            ar ? "آخر إشعار" : "Last notice",
            ar ? "قناة آخر إشعار" : "Last notice channel",
        };

        // ================================================================== §8.5 the notices

        /// <summary>
        /// Issues a batch of paper notices and renders them for the printer. A POST
        /// and not a GET: it mints numbers from series DUN and writes an
        /// append-only log row per family, so it must not be reachable by a link,
        /// a refresh or a crawler.
        /// </summary>
        [HttpPost("collection/notices")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Collection, ActionVerb.Print)]
        public Task<IActionResult> CollectionNotices(
            int[] studentIds, DateTime? from = null, DateTime? to = null, int? year = null,
            int? grade = null, int? section = null, string? q = null, AgingBucket? bucket = null, bool notifiable = false)
            => IssueAsync(CollectionNoticeChannel.Paper, studentIds, from, to, year, grade, section, q, bucket, notifiable);

        /// <summary>
        /// The same batch, sent to the portal instead of the printer — one in-app
        /// delivery per guardian with a portal sign-in, through doc 09's engine on
        /// the <c>InstallmentOverdue</c> event, so a school's own subscription
        /// rules and quiet hours govern it exactly as they govern the automatic
        /// ladder.
        /// <para>
        /// Behind <c>Post</c>, which is the dedicated grant BR-GLB-102 requires for
        /// bulk messaging to parents. Every send is retained: the notice log records
        /// what was issued, and the delivery rows record what was actually queued
        /// and rendered.
        /// </para>
        /// </summary>
        [HttpPost("collection/portal-notices")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Collection, ActionVerb.Post)]
        public Task<IActionResult> CollectionPortalNotices(
            int[] studentIds, DateTime? from = null, DateTime? to = null, int? year = null,
            int? grade = null, int? section = null, string? q = null, AgingBucket? bucket = null, bool notifiable = false)
            => IssueAsync(CollectionNoticeChannel.Portal, studentIds, from, to, year, grade, section, q, bucket, notifiable);

        private async Task<IActionResult> IssueAsync(
            CollectionNoticeChannel channel, int[] studentIds, DateTime? from, DateTime? to, int? year,
            int? grade, int? section, string? q, AgingBucket? bucket, bool notifiable)
        {
            var filter = new CollectionFilter(from?.Date, to?.Date, year, grade, section, q, bucket, notifiable);
            var selected = (studentIds ?? Array.Empty<int>()).Distinct().ToList();
            if (selected.Count == 0)
            {
                TempData["Error"] = T("Select at least one student first.", "اختر طالباً واحداً على الأقل أولاً.");
                return RedirectToAction(nameof(Collection), RouteFor(filter));
            }

            NoticeBatch batch;
            try
            {
                batch = await _collection.IssueNoticesAsync(selected, channel, filter, HttpContext.RequestAborted);
            }
            catch (InvalidCollectionWindowException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
                return RedirectToAction(nameof(Collection), RouteFor(filter));
            }

            // The skips are said out loud rather than left to be inferred from a smaller number of
            // letters than families selected. Each has a different remedy and the officer can only
            // apply it if they know which one happened.
            var skipped = new List<string>();
            if (batch.SkippedNothingOutstanding > 0)
            {
                skipped.Add(T($"{batch.SkippedNothingOutstanding} already settled", $"{batch.SkippedNothingOutstanding} سُدّد بالفعل"));
            }

            if (batch.SkippedPdcCovered > 0)
            {
                skipped.Add(T(
                    $"{batch.SkippedPdcCovered} covered by post-dated cheques",
                    $"{batch.SkippedPdcCovered} مغطّى بشيكات آجلة"));
            }

            if (batch.SkippedNoPortalAccount > 0)
            {
                skipped.Add(T(
                    $"{batch.SkippedNoPortalAccount} with no portal account",
                    $"{batch.SkippedNoPortalAccount} بلا حساب على البوابة"));
            }

            if (channel == CollectionNoticeChannel.Portal)
            {
                TempData["Flash"] = batch.Issued.Count == 0
                    ? T("No portal notification was sent.", "لم يُرسَل أي إشعار عبر البوابة.")
                    : T($"{batch.Issued.Count} portal notification(s) sent.", $"أُرسل {batch.Issued.Count} إشعاراً عبر البوابة.");
                if (skipped.Count > 0)
                {
                    TempData["Error"] = T($"Skipped: {string.Join("، ", skipped)}.", $"استُثني: {string.Join("، ", skipped)}.");
                }

                return RedirectToAction(nameof(Collection), RouteFor(filter));
            }

            if (batch.Issued.Count == 0)
            {
                TempData["Error"] = skipped.Count > 0
                    ? T($"Nothing to print — skipped: {string.Join("، ", skipped)}.", $"لا شيء للطباعة — استُثني: {string.Join("، ", skipped)}.")
                    : T("Nothing to print.", "لا شيء للطباعة.");
                return RedirectToAction(nameof(Collection), RouteFor(filter));
            }

            var school = await _db.Schools.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == _db.CurrentSchoolId, HttpContext.RequestAborted);

            return View("CollectionNotices", new CollectionNoticesViewModel
            {
                Notices = batch.Issued,
                SchoolNameAr = school?.NameAr ?? string.Empty,
                SchoolNameEn = school?.NameEn ?? string.Empty,
                PrintedAtUtc = _clock.UtcNow,
                WindowFrom = filter.From,
                WindowTo = filter.To,
                SkippedNothingOutstanding = batch.SkippedNothingOutstanding,
                SkippedPdcCovered = batch.SkippedPdcCovered,
            });
        }

        // ================================================================== helpers

        /// <summary>The filter as route values, so a redirect after a refusal comes back to the same list.</summary>
        private static object RouteFor(CollectionFilter f) => new
        {
            from = f.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            to = f.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            year = f.AcademicYearId,
            grade = f.GradeLevelId,
            section = f.SectionId,
            q = f.Query,
            bucket = f.Bucket,
            notifiable = f.NotifiableOnly,
        };

        private async Task<CollectionRollViewModel> BuildCollectionAsync(
            DateTime? from, DateTime? to, int? year, int? grade, int? section, string? q, AgingBucket? bucket, bool notifiable)
        {
            var filter = new CollectionFilter(from?.Date, to?.Date, year, grade, section, q, bucket, notifiable);
            var m = new CollectionRollViewModel { Filter = filter };

            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            m.Years = years;
            m.Year = years.FirstOrDefault(y => y.Id == (year ?? _workingYear.AcademicYearId))
                ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active)
                ?? years.FirstOrDefault();

            m.CanExport = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Collection, ActionVerb.Export, HttpContext.RequestAborted);
            m.CanPrint = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Collection, ActionVerb.Print, HttpContext.RequestAborted);
            m.CanSend = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Collection, ActionVerb.Post, HttpContext.RequestAborted);

            if (m.Year == null)
            {
                return m;
            }

            var yid = m.Year.Id;

            // The grade picker offers what a school teaches now; the roll's own grade *labels* are
            // resolved inside the query through IgnoreQueryFilters, because a retired grade still
            // names last year's arrears (SoftActiveLookupTests' distinction).
            var grades = await _db.GradeLevels.AsNoTracking().OrderBy(g => g.SequenceOrder).ToListAsync();
            m.Grades = grades;

            if (grade != null)
            {
                // Materialised before the query for the reason FeesController records: EF Core 5 will
                // not translate a filtered projection of a local list that closes over a query
                // parameter, and this exact shape threw the first time anyone picked a grade.
                var profileIds = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking()
                    .Where(p => p.AcademicYearId == yid && p.GradeLevelId == grade).Select(p => p.Id).ToListAsync();
                m.Sections = await _db.Sections.AsNoTracking()
                    .Where(s => s.AcademicYearId == yid && profileIds.Contains(s.GradeYearProfileId))
                    .OrderBy(s => s.NameEn).ToListAsync();
            }

            try
            {
                var roll = await _collection.GetRollAsync(filter with { AcademicYearId = yid }, cancellationToken: HttpContext.RequestAborted);
                m.Rows = roll.Rows;
                m.MatchCount = roll.MatchCount;
                m.IsTruncated = roll.IsTruncated;
                m.TotalOutstanding = roll.TotalOutstanding;
                m.TotalNotifiable = roll.TotalNotifiable;
            }
            catch (InvalidCollectionWindowException ex)
            {
                // Shown on the screen beside the two date boxes rather than as a flash on a redirect:
                // the dates that were refused are still in the form, which is where they get fixed.
                m.WindowError = UserMessage.For(ex, IsArabic);
            }

            return m;
        }
    }
}
