using System;
using System.Collections.Generic;
using System.Linq;
using Sc = Sms.Application.Security.ScreenCatalog;

namespace Sms.Web.Navigation
{
    /// <summary>
    /// The departments a school is actually staffed as — finance, student affairs, the secretariat,
    /// the teaching staff, reports, the timetable, and the daily cover rota — and the screens that
    /// belong to each.
    /// <para>
    /// This is a <b>second</b> taxonomy over the same screens, not a replacement for
    /// <see cref="ModuleCatalog"/>, and the difference is the point. The module catalogue answers
    /// "what does this product contain", which is how it is built, documented and tested. This one
    /// answers "what is my job", which is how it is used: a cashier does not think of Fees,
    /// Instalments, Payments and Discounts as four modules, and an admissions clerk does not care
    /// that the calendar belongs to a different stage of the build than the application form.
    /// </para>
    /// <para>
    /// The two are allowed to overlap and are meant to. Parent records appear under both the students
    /// and the secretariat department because two people reach them from two directions. A partition
    /// would force a choice the school itself does not make.
    /// </para>
    /// <para>
    /// Every link names the same <c>(module, screen)</c> pair as the <c>[RequirePermission]</c>
    /// attribute on the action it opens — through the same constants, so a rename upstream is a
    /// compile error rather than a tile that quietly stops appearing. What the launcher shows and
    /// what the server allows therefore cannot drift: a link this user cannot open is not rendered,
    /// rather than rendered and then answered with 404 (BR-SEC-010).
    /// </para>
    /// </summary>
    public static class WorkspaceCatalog
    {
        /// <summary>
        /// One screen inside a department. <see cref="ModuleCode"/> and <see cref="ScreenCode"/> are
        /// the permission gate; <see cref="Controller"/> and <see cref="Action"/> are where it goes.
        /// Both are stated because they are not the same thing — <c>Timetable/Conflicts</c> and
        /// <c>Timetable/Validation</c> are two screens behind one permission.
        /// </summary>
        public sealed record WorkspaceLink(
            string ModuleCode,
            string ScreenCode,
            string TitleEn,
            string TitleAr,
            string Icon,
            string Controller,
            string Action);

        /// <summary>
        /// A department. <see cref="Accounting"/> marks the one that also shows the embedded ERP's
        /// groups — finance is where the ledger, the stores and the till belong, and re-listing those
        /// screens by hand is exactly the mistake §7.1 of the integration plan removed from the
        /// sidebar.
        /// </summary>
        public sealed record WorkspaceInfo(
            string Key,
            string TitleEn,
            string TitleAr,
            string Icon,
            string BlurbEn,
            string BlurbAr,
            IReadOnlyList<WorkspaceLink> Links,
            bool Accounting = false);

        private static readonly WorkspaceInfo[] All =
        {
            new("finance", "Finance", "المالية", "bi-wallet2",
                "Fees, collection, instalments, discounts — and the ledger they post to.",
                "الرسوم والتحصيل والتقسيط والخصومات — والأستاذ الذي تُرحَّل إليه.",
                new WorkspaceLink[]
                {
                    new(Sc.Modules.Fees,         Sc.Fees.Charges,           "Fee charges",        "الرسوم المستحقة",     "bi-receipt",             "Fees",         "Index"),
                    new(Sc.Modules.Fees,         Sc.Fees.Structure,         "Fee structure",      "هيكل الرسوم",         "bi-diagram-3",           "Fees",         "Structure"),
                    new(Sc.Modules.Fees,         Sc.Fees.Categories,        "Fee categories",     "فئات الرسوم",         "bi-tags",                "Fees",         "Categories"),
                    new(Sc.Modules.Fees,         Sc.Fees.Position,          "Financial position", "الموقف المالي",       "bi-clipboard-data",      "Fees",         "Position"),
                    new(Sc.Modules.Fees,         Sc.Fees.StudentFinance,    "Student finance",    "مالية الطلاب",        "bi-person-vcard",        "Fees",         "StudentFinance"),
                    new(Sc.Modules.Payments,     Sc.Payments.Cashier,       "Cashier",            "الصندوق",             "bi-cash-coin",           "Payments",     "Index"),
                    new(Sc.Modules.Payments,     Sc.Payments.Till,          "Till session",       "وردية الصندوق",       "bi-safe",                "Payments",     "Till"),
                    new(Sc.Modules.Payments,     Sc.Payments.Pdc,           "Post-dated cheques", "الشيكات الآجلة",      "bi-calendar-check",      "Payments",     "Pdc"),
                    new(Sc.Modules.Payments,     Sc.Payments.Refunds,       "Refunds",            "الاستردادات",         "bi-arrow-return-left",   "Payments",     "Refunds"),
                    new(Sc.Modules.Payments,     Sc.Payments.Allocations,   "Allocations",        "تخصيص المدفوعات",     "bi-shuffle",             "Payments",     "Allocations"),
                    new(Sc.Modules.Installments, Sc.Installments.Templates, "Instalment plans",   "خطط التقسيط",         "bi-calendar-range",      "Installments", "Index"),
                    new(Sc.Modules.Installments, Sc.Installments.Cases,     "Reschedule cases",   "حالات إعادة الجدولة", "bi-exclamation-triangle","Installments", "Cases"),
                    new(Sc.Modules.Installments, Sc.Installments.Dunning,   "Dunning",            "المطالبات",           "bi-envelope-exclamation","Installments", "Dunning"),
                    new(Sc.Modules.Discounts,    Sc.Discounts.Grants,       "Discounts",          "الخصومات",            "bi-percent",             "Discounts",    "Index"),
                    new(Sc.Modules.Discounts,    Sc.Discounts.Scholarships, "Scholarships",       "المنح الدراسية",      "bi-award",               "Discounts",    "Scholarships"),
                    new(Sc.Modules.Discounts,    Sc.Discounts.Types,        "Discount types",     "أنواع الخصم",         "bi-tag",                 "Discounts",    "Types"),
                    new(Sc.Modules.Discounts,    Sc.Discounts.Renewals,     "Renewals",           "تجديد الخصومات",      "bi-arrow-repeat",        "Discounts",    "Renewals"),
                    new(Sc.Modules.Discounts,    Sc.Discounts.Waivers,      "Waivers",            "الإعفاءات",           "bi-file-earmark-minus",  "Discounts",    "Waivers"),
                    new(Sc.Modules.Fees,         Sc.Fees.GlExport,          "GL export",          "الترحيل المحاسبي",    "bi-arrow-left-right",    "GlExport",     "Index"),
                    new(Sc.Modules.Fees,         Sc.Fees.GlMapping,         "GL mapping",         "ربط الحسابات",        "bi-diagram-2",           "GlExport",     "Mappings"),
                },
                Accounting: true),

            new("students", "Students", "الطلاب", "bi-mortarboard",
                "The student body: who is enrolled, who is here today, and who owes an excuse.",
                "شؤون الطلاب: من المسجَّل، ومن حضر اليوم، ومن عليه عذر.",
                new WorkspaceLink[]
                {
                    new(Sc.Modules.Students,   Sc.Students.Directory,        "Student directory",    "دليل الطلاب",         "bi-mortarboard",       "Students",   "Index"),
                    new(Sc.Modules.Students,   Sc.Students.Enrollment,       "Bulk placement",       "الإسناد الجماعي",     "bi-people-fill",       "Students",   "BulkPlacement"),
                    new(Sc.Modules.Parents,    Sc.Parents.Directory,         "Parent directory",     "دليل أولياء الأمور",  "bi-people",            "Parents",    "Index"),
                    new(Sc.Modules.Sections,   Sc.Sections.Sections_,        "Sections",             "الشعب",               "bi-grid-3x3-gap",      "Sections",   "Index"),
                    new(Sc.Modules.Sections,   Sc.Sections.Board,            "Assignment board",     "لوحة توزيع الطلاب",   "bi-columns-gap",       "Sections",   "Board"),
                    new(Sc.Modules.Attendance, Sc.Attendance.Capture,        "Attendance",           "الحضور والغياب",      "bi-check2-square",     "Attendance", "Index"),
                    new(Sc.Modules.Attendance, Sc.Attendance.Gate,           "Gate register",        "سجل البوابة",         "bi-door-open",         "Attendance", "Gate"),
                    new(Sc.Modules.Attendance, Sc.Attendance.Justifications, "Justifications",       "الأعذار",             "bi-file-earmark-text", "Attendance", "Justifications"),
                    new(Sc.Modules.Attendance, Sc.Attendance.Corrections,    "Corrections",          "تصويب الحضور",        "bi-pencil-square",     "Attendance", "Corrections"),
                    new(Sc.Modules.Attendance, Sc.Attendance.Analytics,      "Attendance analytics", "تحليلات الحضور",      "bi-graph-up",          "Attendance", "Analytics"),
                }),

            new("secretariat", "Secretariat", "السكرتاريا", "bi-inboxes",
                "The front office: admission, the school's own record, the year and its calendar.",
                "المكتب الأمامي: القبول، وسجل المدرسة، والعام الدراسي وتقويمه.",
                new WorkspaceLink[]
                {
                    new(Sc.Modules.Admissions,    Sc.Admissions.Applications, "Applications",        "طلبات القبول",        "bi-person-plus",     "Admissions",    "Index"),
                    new(Sc.Modules.Admissions,    Sc.Admissions.Board,        "Admissions board",    "لوحة القبول",         "bi-kanban",          "Admissions",    "Board"),
                    new(Sc.Modules.Admissions,    Sc.Admissions.Waitlist,     "Waitlist",            "قائمة الانتظار",      "bi-hourglass-split", "Admissions",    "WaitingList"),
                    new(Sc.Modules.Parents,       Sc.Parents.Dedup,           "Duplicate workbench", "معالجة التكرار",      "bi-files",           "Parents",       "Dedup"),
                    new(Sc.Modules.Schools,       Sc.Schools.Profile,         "School profile",      "ملف المدرسة",         "bi-building",        "School",        "Profile"),
                    new(Sc.Modules.Schools,       Sc.Schools.Signatories,     "Signatories",         "المفوّضون بالتوقيع",  "bi-pen",             "School",        "Signatories"),
                    new(Sc.Modules.AcademicYears, Sc.AcademicYears.Years,     "Academic years",      "الأعوام الدراسية",    "bi-calendar3",       "AcademicYears", "Index"),
                    new(Sc.Modules.Calendar,      Sc.Calendar.Calendar_,      "Academic calendar",   "التقويم الدراسي",     "bi-calendar-event",  "Calendar",      "Index"),
                    new(Sc.Modules.Classrooms,    Sc.Classrooms.Rooms,        "Classrooms",          "القاعات الدراسية",    "bi-door-closed",     "Rooms",         "Index"),
                    new(Sc.Modules.Setup,         Sc.Setup.Settings,          "System settings",     "إعدادات النظام",      "bi-sliders",         "Setup",         "Settings"),
                }),

            new("teachers", "Teaching staff", "المدرسون", "bi-person-workspace",
                "Who teaches what, how much of it, and the marks that come back.",
                "من يُدرِّس ماذا، وبأي نصاب، والدرجات العائدة منه.",
                new WorkspaceLink[]
                {
                    new(Sc.Modules.Teachers,  Sc.Teachers.Teachers_,      "Teachers",              "المعلمون",         "bi-person-workspace",  "Teachers",  "Index"),
                    new(Sc.Modules.Teachers,  Sc.Teachers.Assignments,    "Teaching assignments",  "الإسنادات",        "bi-grid",              "Teachers",  "Matrix"),
                    new(Sc.Modules.Teachers,  Sc.Teachers.Load,           "Teaching load",         "النصاب",           "bi-speedometer",       "Teachers",  "Load"),
                    new(Sc.Modules.Employees, Sc.Employees.Directory,     "Employees",             "الموظفون",         "bi-person-badge",      "Employees", "Index"),
                    new(Sc.Modules.Employees, Sc.Employees.OrgChart,      "Organization chart",    "الهيكل التنظيمي",  "bi-diagram-3",         "Employees", "Org"),
                    new(Sc.Modules.Employees, Sc.Employees.Contracts,     "Contracts",             "العقود",           "bi-file-earmark-text", "Employees", "Contracts"),
                    new(Sc.Modules.Employees, Sc.Employees.Payroll,       "Payroll",               "مسير الرواتب",     "bi-calendar3",         "Payroll",   "Index"),
                    new(Sc.Modules.Employees, Sc.Employees.Advances,      "Salary advances",       "سلف الموظفين",     "bi-cash-coin",         "Payroll",   "Advances"),
                    new(Sc.Modules.Subjects,  Sc.Subjects.Subjects_,      "Subjects",              "المواد الدراسية",  "bi-journal-bookmark",  "Subjects",  "Index"),
                    new(Sc.Modules.Subjects,  Sc.Subjects.CurriculumPlan, "Curriculum plan",       "الخطة الدراسية",   "bi-journals",          "Subjects",  "Plan"),
                    new(Sc.Modules.Grading,   Sc.Grading.Marksheets,      "Marksheets",            "كشوف الدرجات",     "bi-bar-chart-line",    "Grading",   "Index"),
                    new(Sc.Modules.Grading,   Sc.Grading.Blueprints,      "Assessment blueprints", "مخططات التقييم",   "bi-list-check",        "Grading",   "Blueprints"),
                    new(Sc.Modules.Grading,   Sc.Grading.Scales,          "Grading scales",        "سلالم التقدير",    "bi-rulers",            "Grading",   "Scales"),
                    new(Sc.Modules.Grading,   Sc.Grading.Criteria,        "Promotion criteria",    "معايير الترفيع",   "bi-check-circle",      "Grading",   "Criteria"),
                    new(Sc.Modules.Grading,   Sc.Grading.Results,         "Results",               "النتائج",          "bi-trophy",            "Grading",   "Results"),
                }),

            new("reports", "Reports", "التقارير", "bi-file-earmark-bar-graph",
                "The catalogue, what it has run, and the dashboards built on the same numbers.",
                "دليل التقارير، وما شُغِّل منه، ولوحات المعلومات المبنية على الأرقام نفسها.",
                new WorkspaceLink[]
                {
                    new(Sc.Modules.Reports,    Sc.Reports.Catalog,       "Report catalogue", "دليل التقارير",   "bi-files",         "Reports",    "Index"),
                    new(Sc.Modules.Reports,    Sc.Reports.Executions,    "Execution log",    "سجل التشغيل",     "bi-clock-history", "Reports",    "Log"),
                    new(Sc.Modules.Reports,    Sc.Reports.Subscriptions, "Subscriptions",    "الاشتراكات",      "bi-bell",          "Reports",    "Subscriptions"),
                    new(Sc.Modules.Dashboards, Sc.Dashboards.Dashboard,  "Dashboards",       "لوحات المعلومات", "bi-speedometer2",  "Dashboards", "Index"),
                    new(Sc.Modules.Dashboards, Sc.Dashboards.Statistics, "Statistics",       "الإحصائيات",      "bi-bar-chart-line", "Dashboards", "Statistics"),
                    new(Sc.Modules.Dashboards, Sc.Dashboards.Layouts,    "Layouts",          "تخطيطات اللوحات", "bi-columns-gap",   "Dashboards", "Layouts"),
                    new(Sc.Modules.Dashboards, Sc.Dashboards.Widgets,    "Widgets",          "عناصر اللوحات",   "bi-grid-1x2",      "Dashboards", "Widgets"),
                }),

            new("timetable", "Timetable", "الجدول الدراسي", "bi-table",
                "Building, checking and publishing the weekly schedule.",
                "بناء الجدول الأسبوعي والتحقق منه ونشره.",
                new WorkspaceLink[]
                {
                    new(Sc.Modules.Timetable, Sc.Timetable.Builder,    "Timetable builder", "بناء الجدول", "bi-table",                "Timetable", "Builder"),
                    new(Sc.Modules.Timetable, Sc.Timetable.Shape,      "Week shape",        "شكل الأسبوع", "bi-calendar-week",        "Timetable", "Shape"),
                    new(Sc.Modules.Timetable, Sc.Timetable.Validation, "Validation",        "التحقق",      "bi-check2-circle",        "Timetable", "Validation"),
                    new(Sc.Modules.Timetable, Sc.Timetable.Validation, "Conflicts",         "التعارضات",   "bi-exclamation-triangle", "Timetable", "Conflicts"),
                    new(Sc.Modules.Timetable, Sc.Timetable.Versions,   "Publish",           "نشر الجدول",  "bi-send",                 "Timetable", "Publish"),
                }),

            new("transport", "Transport", "المواصلات", "bi-bus-front",
                "The fleet, the routes, who rides them, and this morning's trips.",
                "الحافلات والمسارات ومن يركبها ورحلات هذا الصباح.",
                new WorkspaceLink[]
                {
                    // The trip console leads, not the fleet: this department is opened at 07:00 far
                    // more often than it is opened to register a bus.
                    new(Sc.Modules.Transport, Sc.Transport.Trips,         "Trip console",           "لوحة الرحلات",        "bi-clipboard-check",    "Transport", "Trips"),
                    new(Sc.Modules.Transport, Sc.Transport.Safety,        "Safety register",        "سجل السلامة",         "bi-shield-exclamation", "Transport", "Safety"),
                    new(Sc.Modules.Transport, Sc.Transport.Subscriptions, "Subscriptions",          "اشتراكات النقل",      "bi-people",             "Transport", "Subscriptions"),
                    new(Sc.Modules.Transport, Sc.Transport.Routes,        "Routes and stops",       "المسارات والمحطات",   "bi-signpost-split",     "Transport", "Routes"),
                    new(Sc.Modules.Transport, Sc.Transport.Fleet,         "Fleet and documents",    "الحافلات والوثائق",   "bi-bus-front",          "Transport", "Index"),
                    new(Sc.Modules.Transport, Sc.Transport.Staff,         "Drivers and attendants", "السائقون والمرافقون", "bi-person-badge",       "Transport", "Staff"),
                }),

            // One screen, so the tile opens it directly rather than a page holding a single card. It
            // is its own department because that is how a school runs it: the person covering a sick
            // teacher at 07:40 is not the person who built the timetable in August.
            new("cover", "Cover rota", "المناوبات", "bi-person-check",
                "Today's absences and who is covering them.",
                "غياب اليوم ومن يغطّيه.",
                new WorkspaceLink[]
                {
                    new(Sc.Modules.Timetable, Sc.Timetable.Cover, "Cover and substitution", "التغطية والمناوبات", "bi-person-check", "Timetable", "Cover"),
                }),

            // The system's own users. These two screens were the last cards of the secretariat's
            // department, on the reading that the back office holding the school's record and its
            // settings also holds its accounts. The owner asked for users, roles and permissions to
            // be findable as one named thing instead — the same ask that gave them a section in the
            // sidebar, and the launcher has to agree with the sidebar about where a screen lives or
            // neither is worth reading. Both are gated on their own permission, so a secretary who
            // held neither has lost nothing: they were never shown these cards.
            new("security", "Users & permissions", "المستخدمون والصلاحيات", "bi-shield-lock",
                "Who may sign in, which role each one holds, and what that role can reach.",
                "من يدخل النظام، وأي دور يحمله، وما الذي يبلغه ذلك الدور.",
                new WorkspaceLink[]
                {
                    new(Sc.Modules.SystemAdministration, Sc.SystemAdministration.UserRoles, "Users and roles",       "المستخدمون وأدوارهم", "bi-person-gear", "Security", "Users"),
                    new(Sc.Modules.SystemAdministration, Sc.SystemAdministration.Roles,     "Roles and permissions", "الأدوار والصلاحيات",  "bi-shield-lock", "Security", "Index"),
                }),
        };

        public static IReadOnlyList<WorkspaceInfo> Workspaces => All;

        public static WorkspaceInfo? Find(string key) =>
            All.FirstOrDefault(w => string.Equals(w.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
