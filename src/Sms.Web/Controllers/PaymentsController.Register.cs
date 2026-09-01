using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Security;
using Sms.Domain.Grades;
using Sms.Domain.Parents;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Sections;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Web.Finance;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// doc/Modules/21 §10 "Receipt register" / "Daily collection report" as a screen — what the
    /// school collected between two dates, and for whose children.
    /// <para>
    /// The four screens already built answer one payer at a time: the cashier takes a payment, the
    /// receipt shows one document, the allocation explorer walks one payer's history. None of them
    /// answers a period, which is the question the finance office is actually asked — by the
    /// principal at month end, by an auditor asking whether the series is continuous, and by a
    /// parent whose payment "never arrived". Until now the only way to answer it was to read the
    /// database.
    /// </para>
    /// <para>
    /// The filters the request named — student, guardian, grade, section — are three different
    /// kinds of thing, and only one of them is a fact about a receipt. A receipt is addressed to a
    /// payer (BR-FEE-004), so <i>guardian</i> filters the document itself. A receipt is not
    /// addressed to a student at all: BR-PAY-003 spreads it oldest-first across everything that
    /// payer owes, siblings included, so <i>student</i>, <i>grade</i> and <i>section</i> can only
    /// filter what the allocation engine put against each child's invoices. The register therefore
    /// lists a row per receipt per student, and the money a receipt left unallocated — the family's
    /// credit balance — carries its own row with no student rather than being silently attributed
    /// to whichever child was billed first. Filtering by student, grade or section drops those rows,
    /// and the screen says so above the totals instead of letting a narrowed figure be read as the
    /// period's takings.
    /// </para>
    /// <para>
    /// <b>Not built here</b>, and still deferred from this module's §8: the day-close and bank
    /// reconciliation workbench (§8.6) — the register reports what was collected, it does not close
    /// a day or match a bank statement — and receipt void, which is why <see cref="ReceiptStatus.Void"/>
    /// rows can be rendered but never produced. There is no PDF: the official-document class needs
    /// the print engine that is still an open owner decision (Phase 9 T-5, docs/Status), so what
    /// leaves this screen is a CSV, gated behind its own Export right per BR-SEC-021.
    /// </para>
    /// </summary>
    public partial class PaymentsController
    {
        // ================================================================== §10 register — the period

        [HttpGet("register")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Register, ActionVerb.View)]
        public async Task<IActionResult> Register(
            int? year = null, DateTime? from = null, DateTime? to = null,
            string? q = null, string? guardian = null, int? grade = null, int? section = null)
        {
            var m = await BuildRegisterAsync(year, from, to, q, guardian, grade, section, PaymentRegister.PageSize);
            m.CanExport = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Register, ActionVerb.Export, HttpContext.RequestAborted);
            m.CanOpenReceipt = await _permissions.HasPermissionAsync(
                ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Cashier, ActionVerb.View, HttpContext.RequestAborted);
            return View(m);
        }

        /// <summary>
        /// The same register as a file. Uncapped on purpose — the cap on screen exists so a grid
        /// stays readable, and a file that silently stopped at the same 300 rows would be a
        /// reconciliation that quietly did not balance.
        /// </summary>
        [HttpGet("register/export.csv")]
        [RequirePermission(ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Register, ActionVerb.Export)]
        public async Task<IActionResult> RegisterCsv(
            int? year = null, DateTime? from = null, DateTime? to = null,
            string? q = null, string? guardian = null, int? grade = null, int? section = null)
        {
            var m = await BuildRegisterAsync(year, from, to, q, guardian, grade, section, take: null);
            var arabic = IsArabic;

            var csv = new StringBuilder();
            csv.AppendLine(string.Join(",", new[]
            {
                T("Date", "التاريخ"), T("Receipt no.", "رقم السند"), T("Method", "طريقة الدفع"),
                T("Reference", "المرجع"), T("Guardian", "ولي الأمر"), T("Student no.", "رقم الطالب"),
                T("Student", "الطالب"), T("Grade", "الصف"), T("Section", "الشعبة"),
                T("Amount", "المبلغ"), T("Receipt total", "إجمالي السند"), T("Status", "الحالة"),
            }.Select(Csv)));

            foreach (var r in m.Rows)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Csv(r.Receipt.IssuedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    Csv(r.Receipt.ReceiptNo),
                    Csv(FinanceLabels.Method(r.Receipt.Method, arabic)),
                    Csv(r.Receipt.MethodRefNo ?? string.Empty),
                    Csv(FinanceLabels.ParentName(r.Payer, arabic)),
                    Csv(r.Student?.StudentNo ?? string.Empty),
                    Csv(r.Student == null ? T("(unallocated — family credit)", "(غير مخصّص — رصيد الأسرة)") : FinanceLabels.StudentName(r.Student, arabic)),
                    Csv(r.GradeName ?? string.Empty),
                    Csv(r.SectionName ?? string.Empty),
                    CsvMoney(r.Amount),
                    CsvMoney(r.Receipt.Amount),
                    Csv(r.IsVoid ? T("Void", "ملغى") : T("Posted", "مرحّل")),
                }));
            }

            csv.AppendLine(string.Join(",", new[]
            {
                Csv(string.Empty), Csv(T("Total", "الإجمالي")), Csv(string.Empty), Csv(string.Empty), Csv(string.Empty),
                Csv(string.Empty), Csv(string.Empty), Csv(string.Empty), Csv(string.Empty),
                CsvMoney(m.TotalCollected), Csv(string.Empty), Csv(string.Empty),
            }));

            // The byte-order mark is not decoration: the first thing anyone does with this file is
            // open it in Excel, and Excel without the mark reads every Arabic name as mojibake —
            // which looks like the system mangled the register rather than like three missing bytes.
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

            // Invariant, not a plain interpolated string: the dates inside the file are written
            // invariant too, and a filename formatted through CurrentCulture would name the file in
            // whatever calendar and digit shapes the reader's culture happens to default to — which
            // is a property of the culture data, not of this screen, and not something a file a
            // school archives should depend on. doc/UI/02 §2: Gregorian is the stored reading, Hijri
            // is a sub-display, and neither ever swaps silently.
            var stamp = FormattableString.Invariant($"payments-{m.From:yyyy-MM-dd}-to-{m.To:yyyy-MM-dd}.csv");
            return File(bytes, "text/csv", stamp);
        }

        // ================================================================== the read

        /// <summary>
        /// One query set answering the screen and the file. <paramref name="take"/> caps the rendered
        /// rows; the totals beside them are always computed over the whole match, because a footer
        /// that totals a page is a wrong number rather than a partial one.
        /// </summary>
        private async Task<PaymentRegisterViewModel> BuildRegisterAsync(
            int? year, DateTime? from, DateTime? to, string? q, string? guardian, int? grade, int? section, int? take)
        {
            var (rangeFrom, rangeTo) = PaymentRegister.Range(from, to, _clock.UtcNow);
            var endExclusive = PaymentRegister.EndExclusive(rangeTo);
            var m = new PaymentRegisterViewModel
            {
                From = rangeFrom, To = rangeTo,
                Q = Trimmed(q), Guardian = Trimmed(guardian), GradeId = grade, SectionId = section,
            };

            // The year is the range's, not the working year's: a register read for last March must
            // show last March's classes, and a picker defaulted to "now" would relabel every row.
            var years = await _db.AcademicYears.AsNoTracking().OrderByDescending(y => y.StartDate).ToListAsync();
            m.Years = years;
            m.Year = years.FirstOrDefault(y => y.Id == year)
                ?? years.FirstOrDefault(y => y.StartDate <= rangeTo && rangeTo <= y.EndDate)
                ?? years.FirstOrDefault(y => y.Status == AcademicYearStatus.Active)
                ?? years.FirstOrDefault();
            var yid = m.Year?.Id;

            // The picker offers the grades that exist; the lookup behind the column reads through
            // IgnoreQueryFilters, because a grade retired since a payment was taken still has to
            // name itself on that payment's row.
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.SchoolId == _db.CurrentSchoolId).ToListAsync();
            m.Grades = grades.Where(g => g.IsActive).OrderBy(g => g.SequenceOrder).ToList();

            var profiles = yid == null
                ? new List<GradeYearProfile>()
                : await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking().Where(p => p.AcademicYearId == yid).ToListAsync();
            var sections = yid == null
                ? new List<Section>()
                : await _db.Sections.AsNoTracking().Where(s => s.AcademicYearId == yid).ToListAsync();
            if (grade != null)
            {
                // The profile ids are narrowed in memory rather than inside the query: EF Core 5 will
                // not translate a filtered projection of a local list that closes over a query
                // parameter, and the same shape threw "could not be translated" on the student
                // finance roll the first time anyone picked a grade.
                var gradeProfileIds = profiles.Where(p => p.GradeLevelId == grade).Select(p => p.Id).ToHashSet();
                m.Sections = sections.Where(s => gradeProfileIds.Contains(s.GradeYearProfileId))
                    .OrderBy(s => IsArabic ? s.NameAr : s.NameEn).ToList();
            }

            var receipts = await _db.Receipts.AsNoTracking()
                .Where(r => r.IssuedAtUtc >= rangeFrom && r.IssuedAtUtc < endExclusive)
                .ToListAsync();
            if (receipts.Count == 0) return m;

            // Joined through the receipt rather than through a list of ids: a term's receipts would
            // otherwise become an IN clause thousands of ids long, which SQL Server plans badly and
            // some providers refuse outright.
            var allocations = await (
                from a in _db.PaymentAllocations.AsNoTracking()
                join r in _db.Receipts.AsNoTracking() on a.ReceiptId equals r.Id
                where r.IssuedAtUtc >= rangeFrom && r.IssuedAtUtc < endExclusive
                select new { a.ReceiptId, a.ChargeId, a.AllocatedAmount }).ToListAsync();

            var chargeStudents = await (
                from c in _db.Charges.AsNoTracking()
                join a in _db.PaymentAllocations.AsNoTracking() on c.Id equals a.ChargeId
                join r in _db.Receipts.AsNoTracking() on a.ReceiptId equals r.Id
                where r.IssuedAtUtc >= rangeFrom && r.IssuedAtUtc < endExclusive
                select new { c.Id, c.StudentId }).Distinct().ToListAsync();
            var studentOfCharge = chargeStudents.ToDictionary(c => c.Id, c => c.StudentId);

            var payerIds = receipts.Select(r => r.PayerId).Distinct().ToList();
            var payers = await _db.Payers.AsNoTracking().Where(p => payerIds.Contains(p.Id)).ToListAsync();
            var parentIds = payers.Where(p => p.ParentId != null).Select(p => p.ParentId!.Value).Distinct().ToList();
            var parents = await _db.Parents.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.SchoolId == _db.CurrentSchoolId && parentIds.Contains(p.Id)).ToListAsync();
            var parentOfPayer = payers.ToDictionary(
                p => p.Id,
                p => p.ParentId == null ? null : parents.FirstOrDefault(x => x.Id == p.ParentId.Value));

            var studentIds = studentOfCharge.Values.Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _db.CurrentSchoolId && studentIds.Contains(s.Id)).ToListAsync();

            // Enrollment without a status filter on purpose: a child who has since withdrawn still
            // paid, and their row must still say which class the money was for.
            var enrollments = yid == null
                ? new List<Enrollment>()
                : await _db.Enrollments.AsNoTracking()
                    .Where(e => e.AcademicYearId == yid && studentIds.Contains(e.StudentId)).ToListAsync();
            var enrollmentIds = enrollments.Select(e => e.Id).ToList();
            var memberships = enrollmentIds.Count == 0
                ? new List<SectionMembership>()
                : await _db.SectionMemberships.AsNoTracking().Where(x => enrollmentIds.Contains(x.EnrollmentId)).ToListAsync();

            // ------------------------------------------------------------ one row per receipt per student

            // Indexed before the loop, not scanned inside it. A year's export is thousands of
            // receipts against tens of thousands of allocations, and a Where() per receipt makes the
            // build quadratic — the same shape that took a 1,020-student rollover past ten minutes.
            var byReceipt = allocations.Where(a => studentOfCharge.ContainsKey(a.ChargeId))
                .GroupBy(a => a.ReceiptId)
                .ToDictionary(g => g.Key, g => g.Select(a => (StudentId: studentOfCharge[a.ChargeId], a.AllocatedAmount)).ToList());
            var studentById = students.ToDictionary(s => s.Id);
            var classOf = new Dictionary<int, (string? Grade, string? Section)>();

            var lines = new List<PaymentRegisterViewModel.Row>();
            foreach (var receipt in receipts)
            {
                var mine = byReceipt.TryGetValue(receipt.Id, out var found)
                    ? found.Select(a => (a.StudentId, a.AllocatedAmount))
                    : Enumerable.Empty<(int, decimal)>();
                var payer = parentOfPayer.TryGetValue(receipt.PayerId, out var p) ? p : null;

                foreach (var (studentId, amount) in PaymentRegister.Split(receipt.Amount, mine))
                {
                    Student? student = null;
                    if (studentId != null) studentById.TryGetValue(studentId.Value, out student);

                    if (student != null && !classOf.ContainsKey(student.Id))
                    {
                        classOf[student.Id] = ClassOf(student.Id, enrollments, memberships, profiles, grades, sections);
                    }
                    var (gradeName, sectionName) = student == null ? (null, null) : classOf[student.Id];

                    lines.Add(new PaymentRegisterViewModel.Row(receipt, payer, student, gradeName, sectionName, amount));
                }
            }

            // ------------------------------------------------------------ the filters

            if (!string.IsNullOrWhiteSpace(m.Guardian))
            {
                var g = m.Guardian.Trim();
                lines = lines.Where(l => l.Payer != null && (
                    Has(l.Payer.NameAr, g) || Has(l.Payer.NameEn, g)
                    || Has(l.Payer.ParentFileNo, g) || Has(l.Payer.PrimaryMobile, g))).ToList();
            }

            if (!string.IsNullOrWhiteSpace(m.Q))
            {
                var t = m.Q.Trim();
                lines = lines.Where(l => l.Student != null && (
                    Has(l.Student.StudentNo, t)
                    || Has(l.Student.FirstNameAr, t) || Has(l.Student.FatherNameAr, t) || Has(l.Student.FamilyNameAr, t)
                    || Has(l.Student.FirstNameEn, t) || Has(l.Student.FatherNameEn, t) || Has(l.Student.FamilyNameEn, t))).ToList();
            }

            if (grade != null || section != null)
            {
                var inGrade = grade == null
                    ? null
                    : enrollments
                        .Where(e => profiles.Any(pr => pr.Id == e.GradeYearProfileId && pr.GradeLevelId == grade))
                        .Select(e => e.StudentId).ToHashSet();
                var inSection = section == null
                    ? null
                    : memberships.Where(x => x.SectionId == section && x.EffectiveToUtc == null)
                        .Join(enrollments, x => x.EnrollmentId, e => e.Id, (_, e) => e.StudentId).ToHashSet();

                lines = lines.Where(l => l.Student != null
                    && (inGrade == null || inGrade.Contains(l.Student.Id))
                    && (inSection == null || inSection.Contains(l.Student.Id))).ToList();
            }

            // ------------------------------------------------------------ totals over the whole match

            var counted = lines.Where(l => !l.IsVoid).ToList();
            m.MatchCount = lines.Count;
            m.ReceiptCount = lines.Select(l => l.Receipt.Id).Distinct().Count();
            m.StudentCount = lines.Where(l => l.Student != null).Select(l => l.Student!.Id).Distinct().Count();
            m.TotalCollected = counted.Sum(l => l.Amount);
            m.TotalUnallocated = counted.Where(l => l.IsUnallocated).Sum(l => l.Amount);
            m.TotalVoided = lines.Where(l => l.IsVoid).Sum(l => l.Amount);

            var ordered = lines
                .OrderByDescending(l => l.Receipt.IssuedAtUtc)
                .ThenByDescending(l => l.Receipt.Id)
                .ThenBy(l => l.Student?.StudentNo, StringComparer.OrdinalIgnoreCase)
                .ToList();
            m.Rows = take == null ? ordered : ordered.Take(take.Value).ToList();
            m.IsTruncated = m.Rows.Count < ordered.Count;
            return m;
        }

        // ================================================================== helpers

        /// <summary>
        /// The class a student was in during the register's year — their grade through the year
        /// profile, and their section through the open membership, falling back to the last one they
        /// held so a transferred child still names a section rather than a dash.
        /// </summary>
        private (string? Grade, string? Section) ClassOf(
            int studentId,
            IReadOnlyList<Enrollment> enrollments,
            IReadOnlyList<SectionMembership> memberships,
            IReadOnlyList<GradeYearProfile> profiles,
            IReadOnlyList<GradeLevel> grades,
            IReadOnlyList<Section> sections)
        {
            var enrollment = enrollments.Where(e => e.StudentId == studentId)
                .OrderByDescending(e => e.EnrollmentDate).FirstOrDefault();
            if (enrollment == null) return (null, null);

            var profile = profiles.FirstOrDefault(p => p.Id == enrollment.GradeYearProfileId);
            var gradeLevel = profile == null ? null : grades.FirstOrDefault(g => g.Id == profile.GradeLevelId);

            var mine = memberships.Where(x => x.EnrollmentId == enrollment.Id).ToList();
            var membership = mine.FirstOrDefault(x => x.EffectiveToUtc == null)
                ?? mine.OrderByDescending(x => x.EffectiveFromUtc).FirstOrDefault();
            var sec = membership == null ? null : sections.FirstOrDefault(s => s.Id == membership.SectionId);

            return (
                gradeLevel == null ? null : (IsArabic ? gradeLevel.Name.NameAr : gradeLevel.Name.NameEn),
                sec == null ? null : (IsArabic ? sec.NameAr : sec.NameEn));
        }

        private static bool Has(string? haystack, string needle)
            => !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

        private static string? Trimmed(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static string Csv(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        /// <summary>Two decimals, invariant, no thousands separator — the cell is read by a spreadsheet, not by a person.</summary>
        private static string CsvMoney(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
