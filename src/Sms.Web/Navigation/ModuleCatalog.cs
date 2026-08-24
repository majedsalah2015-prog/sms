using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Web.Navigation
{
    /// <summary>
    /// The 36 product modules (docs/Modules/01..36) as sidebar-facing
    /// metadata: bilingual title, icon, the epic that built its engine, and
    /// whether any real screen exists yet. Every module routes to
    /// ModulesController.Index until its own screens land; the landing page
    /// tells the operator what is built vs. pending instead of a dead link.
    /// </summary>
    public static class ModuleCatalog
    {
        public sealed record ModuleInfo(
            string Code,
            string Number,
            string TitleEn,
            string TitleAr,
            string Icon,
            string Group,
            string Epic,
            string DocPath,
            string? ScreenController = null,
            string? ScreenAction = null);

        private static readonly ModuleInfo[] All =
        {
            // S1 — Structure
            M("SET", "01", "System Setup", "إعداد النظام", "bi-sliders", "structure", "E-101", "01-System-Setup.md", "Setup", "Index"),
            M("SCH", "02", "Schools", "المدارس", "bi-building", "structure", "E-102", "02-Schools.md", "School", "Profile"),
            M("AYR", "03", "Academic Years", "الأعوام الدراسية", "bi-calendar3", "structure", "E-102", "03-Academic-Years.md", "AcademicYears", "Index"),
            M("CAL", "04", "Academic Calendar", "التقويم الدراسي", "bi-calendar-event", "structure", "E-103", "04-Academic-Calendar.md", "Calendar", "Index"),
            M("GRD", "05", "Grades", "الصفوف", "bi-layers", "structure", "E-103", "05-Grades.md", "Grades", "Index"),
            M("SEC", "06", "Sections", "الشعب", "bi-grid-3x3-gap", "structure", "E-103", "06-Sections.md", "Sections", "Index"),
            M("SUB", "07", "Subjects", "المواد الدراسية", "bi-journal-bookmark", "structure", "E-104", "07-Subjects.md", "Subjects", "Index"),
            M("CLS", "08", "Classrooms", "القاعات الدراسية", "bi-door-open", "structure", "E-104", "08-Classrooms.md", "Rooms", "Index"),

            // S2 — People
            M("ADM", "09", "Admissions", "القبول والتسجيل", "bi-person-plus", "people", "E-201", "09-Admissions.md", "Admissions", "Index"),
            M("STU", "10", "Students", "الطلاب", "bi-mortarboard", "people", "E-202", "10-Student-Management.md", "Students", "Index"),
            M("PAR", "11", "Parents", "أولياء الأمور", "bi-people", "people", "E-202", "11-Parent-Management.md", "Parents", "Index"),
            M("EMP", "12", "Employees", "الموظفون", "bi-person-badge", "people", "E-203", "12-Employees.md", "Employees", "Index"),
            M("TCH", "13", "Teachers", "المعلمون", "bi-person-workspace", "people", "E-203", "13-Teachers.md", "Teachers", "Index"),

            // S3 — Academic operations
            M("ATT", "14", "Attendance", "الحضور والغياب", "bi-check2-square", "academics", "E-301", "14-Attendance.md", "Attendance", "Index"),
            M("TTB", "15", "Timetable", "الجدول الدراسي", "bi-table", "academics", "E-401", "15-Timetable.md", "Timetable", "Builder"),
            M("EXM", "16", "Examinations", "الاختبارات", "bi-pencil-square", "academics", "E-402", "16-Examinations.md"),
            M("GRA", "17", "Grading", "الدرجات والتقييم", "bi-bar-chart-line", "academics", "E-302", "17-Grading.md", "Grading", "Index"),
            M("CRT", "18", "Certificates", "الشهادات", "bi-award", "academics", "E-403", "18-Certificates.md"),

            // Finance
            M("FEE", "19", "Fees", "الرسوم الدراسية", "bi-receipt", "finance", "E-303", "19-Fees.md", "Fees", "Index"),
            M("INS", "20", "Installment Plans", "خطط التقسيط", "bi-calendar-check", "finance", "E-304", "20-Installment-Plans.md", "Installments", "Index"),
            M("PAY", "21", "Payments", "المدفوعات", "bi-cash-coin", "finance", "E-303", "21-Payments.md", "Payments", "Index"),
            M("DSC", "22", "Discounts", "الخصومات والمنح", "bi-percent", "finance", "E-304", "22-Discounts.md", "Discounts", "Index"),

            // Services
            M("TRN", "23", "Transportation", "النقل المدرسي", "bi-bus-front", "services", "E-501", "23-Transportation.md", "Transport", "Index"),
            M("HLT", "24", "Health", "الصحة المدرسية", "bi-heart-pulse", "services", "E-502", "24-Health.md"),
            M("DIS", "25", "Discipline", "السلوك والانضباط", "bi-shield-exclamation", "services", "E-503", "25-Discipline.md", "Discipline", "Index"),
            M("LIB", "26", "Library", "المكتبة", "bi-book", "services", "E-601", "26-Library.md"),
            M("CAF", "27", "Cafeteria", "المقصف", "bi-cup-hot", "services", "E-602", "27-Cafeteria.md", "Cafeteria", "Index"),
            M("STO", "28", "School Store", "المتجر المدرسي", "bi-bag", "services", "E-603", "28-School-Store.md"),
            M("ACT", "29", "Activities", "الأنشطة", "bi-trophy", "services", "E-607", "29-Activities.md"),

            // Platform
            M("RPT", "30", "Reports", "التقارير", "bi-file-earmark-bar-graph", "platform", "E-701", "30-Reports.md", "Reports", "Index"),
            M("DSH", "31", "Dashboards", "لوحات المعلومات", "bi-speedometer2", "platform", "E-702", "31-Dashboards.md", "Dashboards", "Index"),
            M("MSG", "32", "Messaging", "المراسلات", "bi-chat-dots", "platform", "E-703", "32-Messaging.md"),
            M("NTF", "33", "Notifications", "الإشعارات", "bi-bell", "platform", "E-703", "33-Notifications.md"),
            M("AUD", "34", "Audit", "التدقيق", "bi-clipboard-data", "platform", "E-704", "34-Audit.md"),
            M("BAK", "35", "Backup", "النسخ الاحتياطي", "bi-hdd-stack", "platform", "E-704", "35-Backup.md"),
            M("SYS", "36", "System Administration", "إدارة النظام", "bi-gear", "platform", "E-704", "36-System-Administration.md", "Security", "Index"),
        };

        private static readonly (string Key, string TitleEn, string TitleAr, string Icon)[] Groups =
        {
            ("structure", "Structure", "الهيكل الأكاديمي", "bi-diagram-3"),
            ("people", "People", "الأشخاص", "bi-people-fill"),
            ("academics", "Academics", "العمليات الأكاديمية", "bi-easel"),
            ("finance", "Finance", "المالية", "bi-wallet2"),
            ("services", "Services", "الخدمات", "bi-box-seam"),
            ("platform", "Platform", "المنصة", "bi-cpu"),
        };

        public static IReadOnlyList<ModuleInfo> Modules => All;

        public static ModuleInfo? Find(string code) =>
            All.FirstOrDefault(m => string.Equals(m.Code, code, System.StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Sidebar tree: Home leaf, then one collapsible group per stage.
        /// <paramref name="isVisible"/> drops modules whose feature toggle is off (BR-SET-006);
        /// <paramref name="isPermitted"/> drops the ones this user cannot open a single screen of
        /// (BR-SEC-010 - unauthorized surface disappears rather than errors). A group left empty by
        /// either filter goes with it, so nobody is shown a heading over nothing.
        /// </summary>
        public static IReadOnlyList<NavItem> BuildSidebar(
            Func<ModuleInfo, bool>? isVisible = null,
            Func<ModuleInfo, bool>? isPermitted = null,
            bool canExportToLedger = true,
            IReadOnlyList<NavItem>? erpGroups = null)
        {
            var items = new List<NavItem>
            {
                new NavItem("home", "Home", "الرئيسية", "bi-house-door", "Home", "Index"),
            };

            foreach (var (key, titleEn, titleAr, icon) in Groups)
            {
                var group = new NavItem(key, titleEn, titleAr, icon);
                foreach (var m in All.Where(x => x.Group == key && (isVisible == null || isVisible(x)) && (isPermitted == null || isPermitted(x))))
                {
                    group.Items.Add(m.ScreenController == null
                        ? new NavItem(m.Code, m.TitleEn, m.TitleAr, m.Icon, "Modules", "Index", new { code = m.Code })
                        : new NavItem(m.Code, m.TitleEn, m.TitleAr, m.Icon, m.ScreenController, m.ScreenAction));
                }

                if (group.Items.Count > 0)
                {
                    items.Add(group);
                }
            }

            var accounting = BuildAccountingGroup(canExportToLedger, erpGroups);
            if (accounting.HasChildren)
            {
                items.Add(accounting);
            }

            return items;
        }

        /// <summary>
        /// The accounting section: this system's GL export seam, and under it every screen the
        /// embedded ERP publishes — stores, buying, selling, the till, and the money that moves
        /// against them (docs/Integration/01-Embedded-Accounting-Plan.md §7).
        /// <para>
        /// The ERP's screens are not <see cref="ModuleInfo"/> entries because they are not this
        /// system's modules: they have no BR document, no feature toggle, and no epic — they belong
        /// to a subsystem hosted here, and modelling them as school modules would make the catalogue
        /// lie about what this product contains. They arrive as ready-made groups from
        /// <see cref="ErpNavigationSource"/>, which reads the ERP's own navigation providers, so this
        /// file never lists an ERP screen and can never go stale against one.
        /// </para>
        /// <para>
        /// One accounting section rather than seven top-level ones is the shape the owner asked for:
        /// the school's own menu keeps its length, and everything financial is reached by opening one
        /// entry. It costs a third level of nesting, which is why <c>_Sidebar.cshtml</c> renders
        /// groups within groups.
        /// </para>
        /// </summary>
        private static NavItem BuildAccountingGroup(bool canExportToLedger, IReadOnlyList<NavItem>? erpGroups)
        {
            var group = new NavItem("accounting", "Accounting", "المحاسبة", "bi-calculator");

            // This system's own screen, not an ERP area: it is the seam where the school's
            // documents become one journal entry, so it sits first in the accounting group.
            if (canExportToLedger)
            {
                group.Items.Add(new NavItem("acc-glexport", "GL export", "الترحيل المحاسبي", "bi-arrow-left-right", "GlExport", "Index"));
            }

            if (erpGroups != null)
            {
                group.Items.AddRange(erpGroups);
            }

            return group;
        }

        private static ModuleInfo M(string code, string number, string en, string ar, string icon, string group, string epic, string doc, string? screenController = null, string? screenAction = null) =>
            new(code, number, en, ar, icon, group, epic, doc, screenController, screenAction);
    }
}
