using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Security;

namespace Sms.Application.Security
{
    /// <summary>
    /// Every screen this product exposes, and the verbs each one answers to
    /// (doc 06 §4: permission = Module × Screen × Action).
    /// <para>
    /// One table, three readers: the seeder catalogues it into
    /// <c>sec.Permission</c>, the controllers name entries from it in
    /// <c>[RequirePermission]</c>, and an architecture test fails the build when
    /// an action names a screen that is not here or names none at all. Deny by
    /// default (BR-GLB-070) only means anything if every screen is actually
    /// covered, and a list nobody checks is not coverage.
    /// </para>
    /// <para>
    /// Module codes are <c>ModuleCatalog</c>'s, so a permission reads back to a
    /// module in the sidebar without a translation table in between. The
    /// embedded ERP's permissions are not here: they arrive through
    /// <see cref="IExternalPermissionCatalog"/> under their own reserved module
    /// code, carrying their own names verbatim.
    /// </para>
    /// </summary>
    public static class ScreenCatalog
    {
        // ---------------------------------------------------------------- verb conventions
        //
        // The taxonomy is doc 06 §4.1's and is not extended here. What each verb
        // means on a screen, so two screens never disagree:
        //
        //   View        render the screen
        //   Create      add a record
        //   Edit        change a record
        //   Deactivate  deactivate, delete, void, remove, unlink, end, cancel
        //               (BR-GLB-005: there is no Delete verb, by design)
        //   Submit      raise something for someone else to decide
        //   Approve     decide it — approve, reject, activate, close, publish, lock
        //   Post        move money or run an engine that does (post a charge,
        //               close a till, generate a GL batch, run dunning)
        //   Print       printable render of a document
        //   Export      hand a file out of the system
        //   Import      bring content in
        //   Configure   change the system's own shape — settings, toggles,
        //               definitions — as opposed to the data it holds

        public sealed record ScreenDefinition(string ModuleCode, string ScreenCode, string TitleEn, string TitleAr, IReadOnlyList<ActionVerb> Verbs);

        // ---------------------------------------------------------------- module codes

        public static class Modules
        {
            public const string Setup = "SET";
            public const string Schools = "SCH";
            public const string AcademicYears = "AYR";
            public const string Calendar = "CAL";
            public const string Grades = "GRD";
            public const string Sections = "SEC";
            public const string Subjects = "SUB";
            public const string Classrooms = "CLS";
            public const string Admissions = "ADM";
            public const string Students = "STU";
            public const string Parents = "PAR";
            public const string Employees = "EMP";
            public const string Teachers = "TCH";
            public const string Attendance = "ATT";
            public const string Timetable = "TTB";
            public const string Grading = "GRA";
            public const string Fees = "FEE";
            public const string Installments = "INS";
            public const string Payments = "PAY";
            public const string Discounts = "DSC";
            public const string Reports = "RPT";
            public const string Dashboards = "DSH";
            public const string Cafeteria = "CAF";

            /// <summary>The parent/student portal. Not a <c>ModuleCatalog</c> entry — it is an audience, not a module — but it needs its own permission space so a portal grant can never widen into a staff one.</summary>
            public const string Portal = "POR";
        }

        // ---------------------------------------------------------------- screen codes

        public static class Setup
        {
            public const string Wizard = "Wizard";
            public const string Settings = "Settings";
            public const string Lookups = "Lookups";
            public const string Nationalities = "Nationalities";
            public const string Features = "Features";
            public const string ContentPack = "ContentPack";
        }

        public static class Schools
        {
            public const string Profile = "Profile";
            public const string Signatories = "Signatories";
            public const string Status = "Status";
        }

        public static class AcademicYears
        {
            public const string Years = "Years";
        }

        public static class Calendar
        {
            public const string Calendar_ = "Calendar";
        }

        public static class Grades
        {
            public const string Grades_ = "Grades";
            public const string Stages = "Stages";
            public const string Profiles = "Profiles";
        }

        public static class Sections
        {
            public const string Sections_ = "Sections";
            public const string Roster = "Roster";
        }

        public static class Subjects
        {
            public const string Subjects_ = "Subjects";
            public const string Departments = "Departments";
            public const string CurriculumPlan = "CurriculumPlan";
        }

        public static class Classrooms
        {
            public const string Rooms = "Rooms";
            public const string Buildings = "Buildings";
        }

        public static class Admissions
        {
            public const string Applications = "Applications";
            public const string Campaigns = "Campaigns";
            public const string Board = "Board";
            public const string Waitlist = "Waitlist";
        }

        public static class Students
        {
            public const string Directory = "Directory";
            public const string File = "File";
            public const string Guardians = "Guardians";
            public const string Enrollment = "Enrollment";

            /// <summary>BR-GLB-072 restricted category: mother's particulars, family circumstances, ration card, religion. Its own screen code so it can be withheld from roles that legitimately hold the rest of the file.</summary>
            public const string SocialProfile = "SocialProfile";
        }

        public static class Parents
        {
            public const string Directory = "Directory";
            public const string File = "File";
            public const string Dedup = "Dedup";
        }

        public static class Employees
        {
            public const string Directory = "Directory";
            public const string File = "File";
            public const string Contracts = "Contracts";
            public const string OrgChart = "OrgChart";
        }

        public static class Teachers
        {
            public const string Teachers_ = "Teachers";
            public const string Assignments = "Assignments";
            public const string Load = "Load";
        }

        public static class Attendance
        {
            public const string Capture = "Capture";
            public const string Gate = "Gate";
            public const string Corrections = "Corrections";
            public const string Justifications = "Justifications";
            public const string Analytics = "Analytics";
        }

        public static class Timetable
        {
            public const string Builder = "Builder";
            public const string Shape = "Shape";
            public const string Versions = "Versions";
            public const string Cover = "Cover";
            public const string Validation = "Validation";
        }

        public static class Grading
        {
            public const string Marksheets = "Marksheets";
            public const string Blueprints = "Blueprints";
            public const string Scales = "Scales";
            public const string Criteria = "Criteria";
            public const string Results = "Results";
            public const string ReportCard = "ReportCard";
        }

        public static class Fees
        {
            public const string Charges = "Charges";
            public const string Categories = "Categories";
            public const string Structure = "Structure";
            public const string Position = "Position";

            /// <summary>doc/Modules/19 §8 "GL export". Under Fees, not the accounting module: the batch is built from this system's documents, and the ledger only receives it.</summary>
            public const string GlExport = "GlExport";

            public const string GlMapping = "GlMapping";
        }

        public static class Installments
        {
            public const string Templates = "Templates";
            public const string Assignment = "Assignment";
            public const string Schedule = "Schedule";
            public const string Cases = "Cases";
            public const string Dunning = "Dunning";
        }

        public static class Payments
        {
            public const string Cashier = "Cashier";
            public const string Till = "Till";
            public const string Pdc = "Pdc";
            public const string Refunds = "Refunds";
            public const string Allocations = "Allocations";
        }

        public static class Discounts
        {
            public const string Grants = "Grants";
            public const string Types = "Types";
            public const string Scholarships = "Scholarships";
            public const string Renewals = "Renewals";
            public const string Waivers = "Waivers";
        }

        public static class Reports
        {
            public const string Catalog = "Catalog";
            public const string Executions = "Executions";
            public const string Subscriptions = "Subscriptions";
        }

        public static class Dashboards
        {
            public const string Dashboard = "Dashboard";
            public const string Layouts = "Layouts";
            public const string Widgets = "Widgets";
        }

        public static class Cafeteria
        {
            public const string Pos = "Pos";
        }

        public static class Portal
        {
            public const string Home = "Home";
            public const string Statement = "Statement";
            public const string Announcements = "Announcements";
            public const string Child = "Child";
        }

        // ---------------------------------------------------------------- the table

        private static readonly ActionVerb[] ReadOnly = { ActionVerb.View };
        private static readonly ActionVerb[] Crud = { ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate };
        private static readonly ActionVerb[] CrudApprove = { ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate, ActionVerb.Approve };

        private static readonly ScreenDefinition[] All =
        {
            // ---- Setup
            S(Modules.Setup, Setup.Wizard, "Setup wizard", "معالج الإعداد", ActionVerb.View, ActionVerb.Configure),
            S(Modules.Setup, Setup.Settings, "System settings", "إعدادات النظام", ActionVerb.View, ActionVerb.Configure),
            S(Modules.Setup, Setup.Lookups, "Lookup lists", "القوائم المرجعية", ActionVerb.View, ActionVerb.Create, ActionVerb.Deactivate),
            S(Modules.Setup, Setup.Nationalities, "Nationalities", "الجنسيات", Crud),
            S(Modules.Setup, Setup.Features, "Feature toggles", "مفاتيح الميزات", ActionVerb.View, ActionVerb.Configure),
            S(Modules.Setup, Setup.ContentPack, "Content pack", "حزمة المحتوى", ReadOnly),

            // ---- School
            S(Modules.Schools, Schools.Profile, "School profile", "ملف المدرسة", ActionVerb.View, ActionVerb.Edit),
            S(Modules.Schools, Schools.Signatories, "Signatories", "المفوّضون بالتوقيع", ActionVerb.View, ActionVerb.Edit),
            S(Modules.Schools, Schools.Status, "School status", "حالة المدرسة", ActionVerb.View, ActionVerb.Approve),

            // ---- Academic years
            S(Modules.AcademicYears, AcademicYears.Years, "Academic years", "الأعوام الدراسية", CrudApprove),

            // ---- Calendar
            S(Modules.Calendar, Calendar.Calendar_, "Academic calendar", "التقويم الدراسي", ActionVerb.View, ActionVerb.Edit, ActionVerb.Approve),

            // ---- Grades
            S(Modules.Grades, Grades.Grades_, "Grade levels", "الصفوف", Crud),
            S(Modules.Grades, Grades.Stages, "Stages", "المراحل", ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Grades, Grades.Profiles, "Grade-year profiles", "ملفات الصف للعام", ActionVerb.Create, ActionVerb.Deactivate),

            // ---- Sections
            S(Modules.Sections, Sections.Sections_, "Sections", "الشعب", CrudApprove),
            S(Modules.Sections, Sections.Roster, "Section roster", "كشف الشعبة", ActionVerb.Edit),

            // ---- Subjects
            S(Modules.Subjects, Subjects.Subjects_, "Subjects", "المواد الدراسية", Crud),
            S(Modules.Subjects, Subjects.Departments, "Departments", "الأقسام", ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Subjects, Subjects.CurriculumPlan, "Curriculum plan", "الخطة الدراسية", ActionVerb.View, ActionVerb.Create, ActionVerb.Deactivate),

            // ---- Classrooms
            S(Modules.Classrooms, Classrooms.Rooms, "Classrooms", "القاعات", Crud),
            S(Modules.Classrooms, Classrooms.Buildings, "Buildings and floors", "المباني والطوابق", ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),

            // ---- Admissions
            S(Modules.Admissions, Admissions.Applications, "Applications", "طلبات القبول", CrudApprove),
            S(Modules.Admissions, Admissions.Campaigns, "Campaigns", "حملات القبول", ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Admissions, Admissions.Board, "Admissions board", "لوحة القبول", ReadOnly),
            S(Modules.Admissions, Admissions.Waitlist, "Waitlist", "قائمة الانتظار", ActionVerb.View, ActionVerb.Edit, ActionVerb.Deactivate, ActionVerb.Approve),

            // ---- Students
            S(Modules.Students, Students.Directory, "Student directory", "دليل الطلاب", ActionVerb.View, ActionVerb.Create),
            S(Modules.Students, Students.File, "Student file", "ملف الطالب", ActionVerb.View, ActionVerb.Edit, ActionVerb.Deactivate, ActionVerb.Approve),
            S(Modules.Students, Students.SocialProfile, "Social profile", "البيانات الاجتماعية", ActionVerb.View, ActionVerb.Edit),
            S(Modules.Students, Students.Guardians, "Guardians", "أولياء الأمر", ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Students, Students.Enrollment, "Enrollment", "التسجيل", ActionVerb.Create),

            // ---- Parents
            S(Modules.Parents, Parents.Directory, "Parent directory", "دليل أولياء الأمور", ActionVerb.View, ActionVerb.Create),
            S(Modules.Parents, Parents.File, "Parent file", "ملف ولي الأمر", ActionVerb.View, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Parents, Parents.Dedup, "Duplicate workbench", "معالجة التكرار", ReadOnly),

            // ---- Employees
            S(Modules.Employees, Employees.Directory, "Employee directory", "دليل الموظفين", ActionVerb.View, ActionVerb.Create),
            S(Modules.Employees, Employees.File, "Employee file", "ملف الموظف", ActionVerb.View, ActionVerb.Edit, ActionVerb.Deactivate, ActionVerb.Approve),
            S(Modules.Employees, Employees.Contracts, "Contracts", "العقود", ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Approve),
            S(Modules.Employees, Employees.OrgChart, "Organization chart", "الهيكل التنظيمي", Crud),

            // ---- Teachers
            S(Modules.Teachers, Teachers.Teachers_, "Teachers", "المعلمون", ActionVerb.View, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Teachers, Teachers.Assignments, "Teaching assignments", "الإسنادات", ActionVerb.View, ActionVerb.Create, ActionVerb.Deactivate),
            S(Modules.Teachers, Teachers.Load, "Teaching load", "النصاب", ReadOnly),

            // ---- Attendance
            S(Modules.Attendance, Attendance.Capture, "Attendance capture", "رصد الحضور", ActionVerb.View, ActionVerb.Edit, ActionVerb.Approve),
            S(Modules.Attendance, Attendance.Gate, "Gate register", "سجل البوابة", ActionVerb.View, ActionVerb.Create, ActionVerb.Edit),
            S(Modules.Attendance, Attendance.Corrections, "Corrections", "التصويبات", ActionVerb.View, ActionVerb.Approve),
            S(Modules.Attendance, Attendance.Justifications, "Justifications", "الأعذار", ActionVerb.View, ActionVerb.Submit, ActionVerb.Approve),
            S(Modules.Attendance, Attendance.Analytics, "Attendance analytics", "تحليلات الحضور", ReadOnly),

            // ---- Timetable
            S(Modules.Timetable, Timetable.Builder, "Timetable builder", "بناء الجدول", ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Timetable, Timetable.Shape, "Week shape", "شكل الأسبوع", ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Timetable, Timetable.Versions, "Versions", "الإصدارات", ActionVerb.View, ActionVerb.Create, ActionVerb.Approve),
            S(Modules.Timetable, Timetable.Cover, "Cover and substitution", "التغطية والبديل", ActionVerb.View, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Timetable, Timetable.Validation, "Validation and conflicts", "التحقق والتعارضات", ActionVerb.View, ActionVerb.Approve),

            // ---- Grading
            S(Modules.Grading, Grading.Marksheets, "Marksheets", "كشوف الدرجات", ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate, ActionVerb.Submit, ActionVerb.Approve),
            S(Modules.Grading, Grading.Blueprints, "Blueprints and weights", "المخطّطات والأوزان", CrudApprove),
            S(Modules.Grading, Grading.Scales, "Grading scales", "سلالم التقدير", CrudApprove),
            S(Modules.Grading, Grading.Criteria, "Criteria", "المعايير", ActionVerb.View, ActionVerb.Edit),
            S(Modules.Grading, Grading.Results, "Results explorer", "مستكشف النتائج", ActionVerb.View, ActionVerb.Post),
            S(Modules.Grading, Grading.ReportCard, "Report card", "بطاقة التقرير", ReadOnly),

            // ---- Fees
            S(Modules.Fees, Fees.Charges, "Charges", "الفواتير", ActionVerb.View, ActionVerb.Create, ActionVerb.Deactivate, ActionVerb.Post),
            S(Modules.Fees, Fees.Categories, "Fee categories", "فئات الرسوم", Crud),
            S(Modules.Fees, Fees.Structure, "Fee structure", "هيكل الرسوم", CrudApprove),
            S(Modules.Fees, Fees.Position, "Financial position", "كشف الحساب", ReadOnly),
            S(Modules.Fees, Fees.GlExport, "GL export", "الترحيل المحاسبي", ActionVerb.View, ActionVerb.Post, ActionVerb.Deactivate, ActionVerb.Export),
            S(Modules.Fees, Fees.GlMapping, "GL account mapping", "ربط الحسابات", ActionVerb.View, ActionVerb.Configure),

            // ---- Installments
            S(Modules.Installments, Installments.Templates, "Plan templates", "قوالب الخطط", CrudApprove),
            S(Modules.Installments, Installments.Assignment, "Plan assignment", "إسناد الخطط", ActionVerb.View, ActionVerb.Create),
            S(Modules.Installments, Installments.Schedule, "Family schedule", "جدول الأسرة", ActionVerb.View, ActionVerb.Edit, ActionVerb.Submit, ActionVerb.Approve),
            S(Modules.Installments, Installments.Cases, "Reschedule cases", "حالات الجدولة", ActionVerb.View, ActionVerb.Approve),
            S(Modules.Installments, Installments.Dunning, "Dunning console", "لوحة التحصيل", ActionVerb.View, ActionVerb.Post),

            // ---- Payments
            S(Modules.Payments, Payments.Cashier, "Cashier", "الصندوق", ActionVerb.View, ActionVerb.Create),
            S(Modules.Payments, Payments.Till, "Till sessions", "جلسات الصندوق", ActionVerb.View, ActionVerb.Create, ActionVerb.Post),
            S(Modules.Payments, Payments.Pdc, "PDC registry", "الشيكات الآجلة", ActionVerb.View, ActionVerb.Create, ActionVerb.Edit),
            S(Modules.Payments, Payments.Refunds, "Refunds", "الاستردادات", ActionVerb.View, ActionVerb.Submit, ActionVerb.Approve),
            S(Modules.Payments, Payments.Allocations, "Allocations", "التخصيصات", ReadOnly),

            // ---- Discounts
            S(Modules.Discounts, Discounts.Grants, "Discount grants", "منح الخصم", ActionVerb.View, ActionVerb.Submit, ActionVerb.Approve, ActionVerb.Deactivate),
            S(Modules.Discounts, Discounts.Types, "Discount types", "أنواع الخصم", ActionVerb.View, ActionVerb.Create),
            S(Modules.Discounts, Discounts.Scholarships, "Scholarship programs", "برامج المنح", ActionVerb.View, ActionVerb.Create, ActionVerb.Submit),
            S(Modules.Discounts, Discounts.Renewals, "Renewal queue", "طابور التجديد", ActionVerb.View, ActionVerb.Create, ActionVerb.Approve),
            S(Modules.Discounts, Discounts.Waivers, "Waivers", "الإعفاءات", ActionVerb.View, ActionVerb.Submit, ActionVerb.Approve),

            // ---- Reports
            S(Modules.Reports, Reports.Catalog, "Report catalog", "دليل التقارير", ActionVerb.View, ActionVerb.Configure, ActionVerb.Export),
            S(Modules.Reports, Reports.Executions, "Execution log", "سجل التنفيذ", ActionVerb.View, ActionVerb.Edit),
            S(Modules.Reports, Reports.Subscriptions, "Subscriptions", "الاشتراكات", ActionVerb.View, ActionVerb.Create, ActionVerb.Deactivate),

            // ---- Dashboards
            S(Modules.Dashboards, Dashboards.Dashboard, "Dashboard", "لوحة المعلومات", ActionVerb.View, ActionVerb.Edit, ActionVerb.Post),
            S(Modules.Dashboards, Dashboards.Layouts, "Layout templates", "قوالب اللوحات", ActionVerb.View, ActionVerb.Create, ActionVerb.Edit),
            S(Modules.Dashboards, Dashboards.Widgets, "Widget catalog", "دليل الأدوات", ActionVerb.View, ActionVerb.Create, ActionVerb.Configure),

            // ---- Cafeteria
            S(Modules.Cafeteria, Cafeteria.Pos, "Cafeteria POS", "نقطة بيع المقصف", ActionVerb.View, ActionVerb.Create, ActionVerb.Deactivate),
            // ---- Portal
            S(Modules.Portal, Portal.Home, "Portal home", "الصفحة الرئيسية للبوابة", ReadOnly),
            S(Modules.Portal, Portal.Statement, "Family statement", "كشف حساب الأسرة", ReadOnly),
            S(Modules.Portal, Portal.Announcements, "Announcements", "الإعلانات", ReadOnly),
            S(Modules.Portal, Portal.Child, "Child view", "ملف الابن", ReadOnly),
        };

        /// <summary>Every screen in the product, in declaration order.</summary>
        public static IReadOnlyList<ScreenDefinition> Screens => All;

        /// <summary>Every (module, screen, verb) triple the catalogue defines — one <c>sec.Permission</c> row each.</summary>
        public static IEnumerable<(string ModuleCode, string ScreenCode, ActionVerb Action)> Permissions()
            => All.SelectMany(s => s.Verbs.Select(v => (s.ModuleCode, s.ScreenCode, v)));

        /// <summary>
        /// Whether the catalogue defines this triple. The architecture test asks
        /// it of every <c>[RequirePermission]</c> in the web project, which is how
        /// a typo in an attribute becomes a failing build instead of a screen
        /// nobody can open.
        /// </summary>
        public static bool Defines(string moduleCode, string screenCode, ActionVerb action)
            => All.Any(s =>
                string.Equals(s.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.ScreenCode, screenCode, StringComparison.OrdinalIgnoreCase)
                && s.Verbs.Contains(action));

        public static IReadOnlyList<ScreenDefinition> ForModule(string moduleCode)
            => All.Where(s => string.Equals(s.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase)).ToList();

        private static ScreenDefinition S(string module, string screen, string en, string ar, params ActionVerb[] verbs)
            => new(module, screen, en, ar, verbs);
    }
}
