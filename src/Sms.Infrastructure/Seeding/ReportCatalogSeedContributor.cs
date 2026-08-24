using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Reports;
using Sms.Application.Security;
using Sms.Application.Seeding;
using Sms.Domain.Reports;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// Fills <c>core.ReportDefinition</c> from <c>docs/Reports/Report-Catalog.md</c>
    /// so the M30 report centre opens on a catalogue rather than on an empty tree —
    /// the platform has been complete since E-701 and no definition ever shipped
    /// with it, which reads to an operator as a broken screen rather than as an
    /// unfilled registry.
    /// <para>
    /// <b>Scope: the three operating loops plus the configuration circle, 71 of the
    /// catalogue's 228.</b> The daily loop (attendance, students), the financial loop
    /// (fees, installments, payments, discounts), the academic loop (grading), and
    /// doc/Modules/01 §10's own four — the reports a support engineer or an auditor
    /// asks for when a school is being onboarded or questioned. The long tail is E-701's own
    /// stated Phase 9 content deliverable, and it is also unseedable here for a
    /// structural reason: BR-RPT-001 binds every definition to a <c>sec.Permission</c>,
    /// so a report can only be registered once its module has a catalogued screen to
    /// gate it. Reports for the 10 modules with no screens are therefore skipped, not
    /// bound to a stand-in permission — a report gated by the wrong screen is worse
    /// than a report nobody can find.
    /// </para>
    /// <para>
    /// <b>What a seeded row does and does not do.</b> It makes the report listable,
    /// searchable, permission-gated (BR-RPT-002), runnable as a recorded execution,
    /// and subscribable (BR-RPT-006). It does <em>not</em> render report content:
    /// there is no query behind a definition yet (doc §14 T-5), so a run records who
    /// asked for what, under which permission, at what size — the same as before this
    /// seeder, only now over a real catalogue. That gap is the report engine's, and it
    /// is unchanged by this contributor.
    /// </para>
    /// <para>
    /// <b>Formats.</b> Operational, register and analytical reports offer HTML/XLSX/CSV;
    /// the catalogue's Type=Doc rows (official documents) offer HTML/PDF. The PDF
    /// engine decision (O6) is still open — QuestPDF failed bidi on .NET 5 — so a PDF
    /// run records the request and produces no file, exactly as an HTML one does today.
    /// </para>
    /// <para>
    /// <b>Parameters.</b> <c>RequiredParameterKeysCsv</c> uses the runner's standard
    /// parameter bar keys (<c>academicYearId</c>, <c>gradeLevelId</c>, <c>sectionId</c>,
    /// <c>dateFrom</c>, <c>dateTo</c>) so a required key always has a control behind it.
    /// The one exception is <c>studentId</c> on the three per-student documents: the bar
    /// has no student picker yet, but a statement of account or a transcript with no
    /// student named is not a report, so the key is required and the operator supplies
    /// it through the runner's ad-hoc parameter row until a picker lands.
    /// </para>
    /// <para>
    /// <b>Codes.</b> Report codes stay the catalogue's verbatim (BR-RPT-001's
    /// <c>RPT-&lt;MOD&gt;-###</c>), which is why discounts read <c>RPT-DIS-###</c> here
    /// while the product's module code for discounts is <c>DSC</c> (the catalogue spells
    /// discipline <c>RPT-DCP-###</c>, so the two do not collide). <c>OwningModuleCode</c>
    /// is always the product's <c>ModuleCatalog</c> code, so the centre groups reports
    /// under the same modules the sidebar names.
    /// </para>
    /// </summary>
    public class ReportCatalogSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;
        private readonly IReportAdmin _reports;

        public ReportCatalogSeedContributor(AppDbContext db, IReportAdmin reports)
        {
            _db = db;
            _reports = reports;
        }

        public string Name => "Report catalogue — the operating loops and the configuration circle (docs/Reports/Report-Catalog.md)";

        // After the permissions this binds to (21) and the numbering catalogue (30),
        // before the demo tenant (45): a real school gets the catalogue too.
        public int Order => 35;

        // ------------------------------------------------------------------ shorthand

        private const string Set = ScreenCatalog.Modules.Setup;
        private const string Att = ScreenCatalog.Modules.Attendance;
        private const string Stu = ScreenCatalog.Modules.Students;
        private const string Fee = ScreenCatalog.Modules.Fees;
        private const string Ins = ScreenCatalog.Modules.Installments;
        private const string Pay = ScreenCatalog.Modules.Payments;
        private const string Dsc = ScreenCatalog.Modules.Discounts;
        private const string Gra = ScreenCatalog.Modules.Grading;

        private const ReportSensitivity Std = ReportSensitivity.Normal;
        private const ReportSensitivity Pd = ReportSensitivity.PersonalData;
        private const ReportSensitivity Locked = ReportSensitivity.Restricted;

        /// <summary>A report read on screen or taken away as a sheet.</summary>
        private const OutputFormat Data = OutputFormat.Html | OutputFormat.Xlsx | OutputFormat.Csv;

        /// <summary>The catalogue's Type=Doc rows — an official layout, printed.</summary>
        private const OutputFormat Document = OutputFormat.Html | OutputFormat.Pdf;

        private const string Period = "dateFrom,dateTo";
        private const string Year = "academicYearId";
        private const string OneSection = "sectionId";
        private const string OneStudent = "studentId";
        /// <summary>
        /// A report the operator scopes from the screen, with nothing mandatory.
        /// Deliberately <c>static readonly</c> rather than <c>const</c>: a constant
        /// field whose value is null crashes NetArchTest's constant scan, and the
        /// layer and ERP-boundary tests are built on it.
        /// </summary>
        private static readonly string? Unscoped = null;

        private sealed record Entry(
            string Code, string Module, string Screen, string TitleEn, string TitleAr,
            ReportSensitivity Sensitivity, OutputFormat Formats, string? Parameters);

        // ------------------------------------------------------------------ the catalogue

        private static readonly Entry[] Catalogue =
        {
            // --- the configuration circle (doc/Modules/01 §10). Not an operating loop
            // but the module's own §10, and the four an auditor or a support engineer
            // asks for first. RPT-SET-002 is the one with an engine already behind it
            // (ILookupUsageQuery); the other three read settings, audit and checklist
            // rows a query layer can reach without anything new.
            new("RPT-SET-001", Set, ScreenCatalog.Setup.Settings, "Configuration snapshot", "لقطة الإعدادات", Std, Data, Unscoped),
            new("RPT-SET-002", Set, ScreenCatalog.Setup.Lookups, "Lookup usage", "استخدام القوائم المرجعية", Std, Data, Unscoped),
            new("RPT-SET-003", Set, ScreenCatalog.Setup.Settings, "Settings change history", "سجل تغييرات الإعدادات", Std, Data, Period),
            new("RPT-SET-004", Set, ScreenCatalog.Setup.Wizard, "Setup completeness", "اكتمال الإعداد", Std, Data, Unscoped),

            // --- the daily loop: attendance (docs/Reports §"Academic operations")
            new("RPT-ATD-001", Att, ScreenCatalog.Attendance.Analytics, "Daily absence report (9 a.m.)", "تقرير الغياب اليومي (التاسعة صباحاً)", Pd, Data, Period),
            new("RPT-ATD-002", Att, ScreenCatalog.Attendance.Analytics, "Section monthly register", "سجل الشعبة الشهري", Std, Document, OneSection + "," + Period),
            new("RPT-ATD-003", Att, ScreenCatalog.Attendance.Analytics, "Repeat and chronic absentees", "المتغيبون المتكررون والمزمنون", Pd, Data, Period),
            new("RPT-ATD-004", Att, ScreenCatalog.Attendance.Analytics, "Lateness patterns", "أنماط التأخر الصباحي", Pd, Data, Period),
            new("RPT-ATD-005", Att, ScreenCatalog.Attendance.Justifications, "Justification outcomes register", "سجل نتائج الأعذار", Pd, Data, Period),
            new("RPT-ATD-006", Att, ScreenCatalog.Attendance.Gate, "Leave-pass register", "سجل أذونات المغادرة", Pd, Data, Period),
            new("RPT-ATD-007", Att, ScreenCatalog.Attendance.Analytics, "Attendance percentage for report cards", "نسبة الحضور لبطاقات النتائج", Std, Data, Year),
            new("RPT-ATD-008", Att, ScreenCatalog.Attendance.Analytics, "Truancy statutory formats", "صيغ التسرب الرسمية", Std, Document, Period),
            new("RPT-ATD-009", Att, ScreenCatalog.Attendance.Corrections, "Correction register (WF-14)", "سجل تصحيحات الحضور (WF-14)", Std, Data, Period),

            // --- the daily loop: the student register
            //
            // Guardians and Enrollment are catalogued without a View verb (they are
            // edit surfaces reached from the file), so nothing here binds to them: a
            // gate that does not exist would silently drop the report. Custody
            // exceptions take the SocialProfile gate rather than the directory's,
            // because that screen is already the restricted one (BR-GLB-072).
            new("RPT-STU-001", Stu, ScreenCatalog.Students.File, "Student profile sheet (full file)", "بطاقة الطالب (الملف الكامل)", Locked, Document, OneStudent),
            new("RPT-STU-002", Stu, ScreenCatalog.Students.Directory, "Students register by grade, section and status", "سجل الطلاب حسب الصف والشعبة والحالة", Pd, Data, Unscoped),
            new("RPT-STU-003", Stu, ScreenCatalog.Students.Directory, "Family composition (siblings)", "تركيبة الأسرة (الإخوة)", Pd, Data, Unscoped),
            new("RPT-STU-004", Stu, ScreenCatalog.Students.Directory, "New and withdrawn students per period", "الملتحقون والمنسحبون خلال الفترة", Std, Data, Period),
            new("RPT-STU-005", Stu, ScreenCatalog.Students.SocialProfile, "Guardianship and custody exceptions", "استثناءات الولاية والحضانة", Locked, Data, Unscoped),
            new("RPT-STU-006", Stu, ScreenCatalog.Students.File, "Subject-exemptions register", "سجل الإعفاءات من المواد", Pd, Data, Year),
            new("RPT-STU-007", Stu, ScreenCatalog.Students.Directory, "Data-quality report (missing documents, photos, contacts)", "تقرير جودة البيانات (مستندات وصور وبيانات اتصال ناقصة)", Std, Data, Unscoped),
            new("RPT-STU-008", Stu, ScreenCatalog.Students.Directory, "Data-retention due list", "قائمة البيانات المستحقة للإتلاف", Locked, Data, Unscoped),
            new("RPT-STU-009", Stu, ScreenCatalog.Students.Directory, "Statistical summaries (nationality, gender, age)", "ملخصات إحصائية (الجنسية والجنس والعمر)", Std, Data, Unscoped),

            // --- the financial loop: fees
            new("RPT-FEE-001", Fee, ScreenCatalog.Fees.Structure, "Fee structure book", "كتيّب هيكل الرسوم", Std, Document, Year),
            new("RPT-FEE-002", Fee, ScreenCatalog.Fees.Charges, "Charges register", "سجل الرسوم المحمَّلة", Std, Data, Period),
            new("RPT-FEE-003", Fee, ScreenCatalog.Fees.Charges, "Expected vs posted revenue (leakage)", "الإيراد المتوقَّع مقابل المحمَّل (التسرّب)", Std, Data, Year),
            new("RPT-FEE-004", Fee, ScreenCatalog.Fees.Position, "Aged receivables by payer, grade and section", "أعمار الذمم حسب الدافع والصف والشعبة", Pd, Data, Unscoped),
            new("RPT-FEE-005", Fee, ScreenCatalog.Fees.Charges, "Credit note register", "سجل الإشعارات الدائنة", Std, Data, Period),
            new("RPT-FEE-006", Fee, ScreenCatalog.Fees.Charges, "Late-fee register (proposed, posted, waived)", "سجل غرامات التأخير (مقترحة ومحمَّلة ومعفاة)", Std, Data, Period),
            new("RPT-FEE-007", Fee, ScreenCatalog.Fees.Charges, "Pro-ration audit report", "تدقيق احتساب الرسوم النسبية", Std, Data, Year),
            new("RPT-FEE-008", Fee, ScreenCatalog.Fees.Charges, "VAT summary per period", "ملخص ضريبة القيمة المضافة للفترة", Std, Data, Period),
            new("RPT-FEE-009", Fee, ScreenCatalog.Fees.GlExport, "GL export journal summary", "ملخص قيود التصدير إلى الأستاذ العام", Std, Data, Period),
            new("RPT-FEE-010", Fee, ScreenCatalog.Fees.Position, "Opening-balance reconciliation", "مطابقة الأرصدة الافتتاحية", Std, Data, Year),

            // --- the financial loop: installment plans
            new("RPT-INS-001", Ins, ScreenCatalog.Installments.Schedule, "Collection calendar (cashflow forecast)", "تقويم التحصيل (توقّع التدفق النقدي)", Std, Data, Period),
            new("RPT-INS-002", Ins, ScreenCatalog.Installments.Cases, "Overdue installments by aging bucket", "الأقساط المتأخرة حسب شريحة التقادم", Pd, Data, Unscoped),
            new("RPT-INS-003", Ins, ScreenCatalog.Installments.Schedule, "Reschedule register", "سجل إعادة الجدولة", Pd, Data, Period),
            new("RPT-INS-004", Ins, ScreenCatalog.Installments.Cases, "Promise-to-pay outcomes", "نتائج وعود الدفع", Pd, Data, Period),
            new("RPT-INS-005", Ins, ScreenCatalog.Installments.Dunning, "Dunning effectiveness", "فاعلية المطالبات", Std, Data, Period),
            new("RPT-INS-006", Ins, ScreenCatalog.Installments.Cases, "PDC-covered vs open exposure", "المغطّى بشيكات آجلة مقابل المكشوف", Std, Data, Unscoped),
            new("RPT-INS-007", Ins, ScreenCatalog.Installments.Templates, "Plan distribution", "توزيع خطط التقسيط", Std, Data, Year),
            new("RPT-INS-008", Ins, ScreenCatalog.Installments.Cases, "Suspension-candidate list", "قائمة المرشحين لإيقاف الخدمات", Locked, Data, Unscoped),

            // --- the financial loop: payments
            new("RPT-PAY-001", Pay, ScreenCatalog.Payments.Till, "Daily collection by till and method", "التحصيل اليومي حسب الصندوق ووسيلة الدفع", Std, Data, Period),
            new("RPT-PAY-002", Pay, ScreenCatalog.Payments.Cashier, "Receipt continuity register (gap-free)", "سجل تسلسل الإيصالات (بلا فجوات)", Std, Data, Period),
            new("RPT-PAY-003", Pay, ScreenCatalog.Payments.Pdc, "PDC maturity ladder", "سلّم استحقاق الشيكات الآجلة", Std, Data, Unscoped),
            new("RPT-PAY-004", Pay, ScreenCatalog.Payments.Pdc, "Bounce register (repeat offenders)", "سجل الشيكات المرتدة (المتكررون)", Locked, Data, Period),
            new("RPT-PAY-005", Pay, ScreenCatalog.Payments.Refunds, "Refund register with chains", "سجل الاستردادات وسلاسلها", Std, Data, Period),
            new("RPT-PAY-006", Pay, ScreenCatalog.Payments.Allocations, "Advance balances list", "قائمة أرصدة الدفعات المقدَّمة", Pd, Data, Unscoped),
            new("RPT-PAY-007", Pay, ScreenCatalog.Payments.Till, "Deposit and reconciliation status", "حالة الإيداع والمطابقة البنكية", Std, Data, Period),
            new("RPT-PAY-008", Pay, ScreenCatalog.Payments.Till, "Cashier variance history", "سجل فروق جرد الصناديق", Std, Data, Period),
            new("RPT-PAY-009", Pay, ScreenCatalog.Payments.Cashier, "Collection vs expected overlay", "التحصيل مقابل المتوقَّع", Std, Data, Period),
            new("RPT-PAY-010", Pay, ScreenCatalog.Payments.Allocations, "Statement of account (payer or student)", "كشف حساب (الدافع أو الطالب)", Pd, Document, OneStudent),

            // --- the financial loop: discounts (catalogue prefix DIS, product module DSC)
            new("RPT-DIS-001", Dsc, ScreenCatalog.Discounts.Grants, "Discount register (the owner report)", "سجل الخصومات (تقرير المالك)", Locked, Data, Year),
            new("RPT-DIS-002", Dsc, ScreenCatalog.Discounts.Grants, "Revenue impact summary (gross, discount, net)", "أثر الخصومات على الإيراد (الإجمالي والخصم والصافي)", Std, Data, Year),
            new("RPT-DIS-003", Dsc, ScreenCatalog.Discounts.Grants, "Sibling discount audit", "تدقيق خصم الإخوة", Pd, Data, Year),
            new("RPT-DIS-004", Dsc, ScreenCatalog.Discounts.Grants, "Staff discount vs employment reconciliation", "مطابقة خصم أبناء الموظفين مع حالة التوظيف", Pd, Data, Year),
            new("RPT-DIS-005", Dsc, ScreenCatalog.Discounts.Scholarships, "Scholarship envelope consumption", "استهلاك مظروف المنح", Std, Data, Year),
            new("RPT-DIS-006", Dsc, ScreenCatalog.Discounts.Waivers, "Waiver register", "سجل الإعفاءات", Std, Data, Period),
            new("RPT-DIS-007", Dsc, ScreenCatalog.Discounts.Renewals, "Renewal decisions register", "سجل قرارات التجديد", Std, Data, Year),
            new("RPT-DIS-008", Dsc, ScreenCatalog.Discounts.Types, "Stacking exceptions", "استثناءات تجميع الخصومات", Std, Data, Year),
            new("RPT-DIS-009", Dsc, ScreenCatalog.Discounts.Grants, "Threshold-approval analysis", "تحليل الموافقات حسب الحدود", Std, Data, Period),

            // --- the academic loop: grading
            new("RPT-GRA-001", Gra, ScreenCatalog.Grading.Results, "Section result broadsheet", "كشف نتائج الشعبة", Pd, Data, OneSection),
            new("RPT-GRA-002", Gra, ScreenCatalog.Grading.Results, "Pass and fail statistics (year on year)", "إحصاءات النجاح والرسوب (مقارنة سنوية)", Std, Data, Unscoped),
            new("RPT-GRA-003", Gra, ScreenCatalog.Grading.Results, "Subject difficulty analysis", "تحليل صعوبة المواد", Std, Data, Year),
            new("RPT-GRA-004", Gra, ScreenCatalog.Grading.Results, "Honor lists (top performers)", "لوحات الشرف (الأوائل)", Std, Document, Year),
            new("RPT-GRA-005", Gra, ScreenCatalog.Grading.Results, "Failure and makeup candidates", "الراسبون ومرشحو الإكمال", Pd, Data, Year),
            new("RPT-GRA-006", Gra, ScreenCatalog.Grading.Results, "Promotion proposals register", "سجل مقترحات الترقية", Pd, Data, Year),
            new("RPT-GRA-007", Gra, ScreenCatalog.Grading.Results, "GPA distribution", "توزيع المعدلات", Std, Data, Year),
            new("RPT-GRA-008", Gra, ScreenCatalog.Grading.Marksheets, "Mark-corrections register (WF-08)", "سجل تصحيح الدرجات (WF-08)", Std, Data, Period),
            new("RPT-GRA-009", Gra, ScreenCatalog.Grading.Results, "Appeals register and outcomes", "سجل الاعتراضات ونتائجها", Pd, Data, Year),
            new("RPT-GRA-010", Gra, ScreenCatalog.Grading.Results, "Ministry result formats", "صيغ النتائج الوزارية", Std, Document, Year),
            new("RPT-GRA-011", Gra, ScreenCatalog.Grading.ReportCard, "Report cards (batch)", "بطاقات النتائج (دفعة)", Std, Document, OneSection),
            new("RPT-GRA-012", Gra, ScreenCatalog.Grading.ReportCard, "Transcripts", "كشوف الدرجات التراكمية", Std, Document, OneStudent),
        };

        // ------------------------------------------------------------------ seeding

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!await _db.Schools.AnyAsync(cancellationToken))
            {
                return;
            }

            // IgnoreQueryFilters so a deactivated definition still counts as present:
            // re-running the seeder must not resurrect a report the school retired.
            var existing = await _db.ReportDefinitions.IgnoreQueryFilters().AsNoTracking()
                .Where(d => d.SchoolId == _db.CurrentSchoolId)
                .Select(d => d.Code)
                .ToListAsync(cancellationToken);
            var have = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var permissions = await _db.Permissions.AsNoTracking()
                .Where(p => p.Action == ActionVerb.View)
                .Select(p => new { p.Id, p.ModuleCode, p.ScreenCode })
                .ToListAsync(cancellationToken);
            var gates = permissions.ToDictionary(
                p => p.ModuleCode + "/" + p.ScreenCode, p => p.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var entry in Catalogue)
            {
                if (have.Contains(entry.Code))
                {
                    continue;
                }

                // No catalogued screen means no gate to bind to. Skip rather than
                // guess: BR-RPT-002 is only as good as the permission it names.
                if (!gates.TryGetValue(entry.Module + "/" + entry.Screen, out var permissionId))
                {
                    continue;
                }

                await _reports.DefineReportAsync(
                    entry.Code, entry.Module, entry.TitleAr, entry.TitleEn,
                    entry.Formats, entry.Sensitivity, permissionId, entry.Parameters, cancellationToken);

                // DefineReportAsync saves per row; keep the tracker from growing
                // across 67 of them.
                _db.ChangeTracker.Clear();
            }
        }
    }
}
