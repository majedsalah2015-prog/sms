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

            /// <summary>Module 23. School transport: the fleet, the routes, who rides, and today's trips.</summary>
            public const string Transport = "TRN";

            /// <summary>Module 25. Behaviour: the code, what was recorded against it, and the cases that follow.</summary>
            public const string Discipline = "DIS";

            /// <summary>Module 36. The screens that decide what every other screen may be reached by.</summary>
            public const string SystemAdministration = "SYS";

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

            /// <summary>doc/Modules/01 §8.3 — the settings hub embeds doc 08's series registry.</summary>
            public const string Numbering = "Numbering";

            /// <summary>doc/Modules/01 §8.3 + doc 10 §5 — the document-type catalogue every module attaches against.</summary>
            public const string Documents = "Documents";

            /// <summary>doc/Modules/01 §8.3 ("Notifications defaults" tab) + doc 09 §3/§4 — the event × channel subscription matrix, BR-SET-008 and BR-NOT-003.</summary>
            public const string Notifications = "Notifications";
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

            /// <summary>doc/Modules/06 §8.3 — drag-drop assignment board and rule-based auto-distribute across a grade's sections.</summary>
            public const string Board = "Board";
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

            /// <summary>doc/Modules/08 §8.5 — rooms × periods occupancy off the published timetable.</summary>
            public const string Utilization = "Utilization";
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

        public static class Discipline
        {
            /// <summary>The year's behaviour code: violations, merits, consequences and the ladder (BR-DCP-001).</summary>
            public const string Code = "Code";

            /// <summary>Recording what happened — an incident against the code, or a merit (BR-DCP-002).</summary>
            public const string Incidents = "Incidents";

            /// <summary>The WF-11 case: investigation, statements, decision, action, appeal, closure (BR-DCP-003).</summary>
            public const string Cases = "Cases";

            /// <summary>Consequences in force — detentions, suspensions, contracts coming due (BR-DCP-004).</summary>
            public const string Actions = "Actions";

            /// <summary>Where behaviour concentrates, and who it keeps happening to (BR-DCP-007).</summary>
            public const string Analytics = "Analytics";
        }

        public static class Transport
        {
            /// <summary>The buses and their expiry-tracked documents (BR-TRN-001).</summary>
            public const string Fleet = "Fleet";

            /// <summary>Drivers and attendants, with the licences trip-opening validates (BR-TRN-002).</summary>
            public const string Staff = "Staff";

            /// <summary>Routes and their ordered stops (BR-TRN-003).</summary>
            public const string Routes = "Routes";

            /// <summary>Who rides, on which stops, and what it charges them (BR-TRN-004/007/008).</summary>
            public const string Subscriptions = "Subscriptions";

            /// <summary>Today's trips: open, board, alight, close (BR-TRN-005/006).</summary>
            public const string Trips = "Trips";

            /// <summary>The safety register — a child not boarded, not collected, or handed to the wrong person (BR-TRN-005/006/009).</summary>
            public const string Safety = "Safety";
        }

        public static class SystemAdministration
        {
            /// <summary>The role designer: which permissions each role carries.</summary>
            public const string Roles = "Roles";

            /// <summary>Who holds which role.</summary>
            public const string UserRoles = "UserRoles";

            /// <summary>
            /// The accounts themselves (doc 06 §8, Module 36 §8.1). Separate from
            /// <see cref="UserRoles"/> because they are separate authorities: handing an existing
            /// person a role is an everyday act of administration, while creating the login that
            /// carries it decides who exists in this system at all.
            /// </summary>
            public const string Users = "Users";
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
            S(Modules.Setup, Setup.Lookups, "Lookup lists", "القوائم المرجعية", ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Setup, Setup.Nationalities, "Nationalities", "الجنسيات", Crud),
            S(Modules.Setup, Setup.Features, "Feature toggles", "مفاتيح الميزات", ActionVerb.View, ActionVerb.Configure),
            // Configure, not Edit: changing what a country imposes is the same shape of act as the
            // wizard and the settings hub beside it, and the pack was read-only here only because
            // nothing could yet write one.
            S(Modules.Setup, Setup.ContentPack, "Content pack", "حزمة المحتوى", ActionVerb.View, ActionVerb.Configure),
            S(Modules.Setup, Setup.Numbering, "Numbering series", "سلاسل الترقيم", ActionVerb.View, ActionVerb.Configure),
            S(Modules.Setup, Setup.Documents, "Document types", "أنواع المستندات", ActionVerb.View, ActionVerb.Configure),
            S(Modules.Setup, Setup.Notifications, "Notification defaults", "الإشعارات الافتراضية", ActionVerb.View, ActionVerb.Configure),

            // ---- School
            S(Modules.Schools, Schools.Profile, "School profile", "ملف المدرسة", ActionVerb.View, ActionVerb.Edit),
            S(Modules.Schools, Schools.Signatories, "Signatories", "المفوّضون بالتوقيع", ActionVerb.View, ActionVerb.Edit),
            S(Modules.Schools, Schools.Status, "School status", "حالة المدرسة", ActionVerb.View, ActionVerb.Approve),

            // ---- Academic years
            S(Modules.AcademicYears, AcademicYears.Years, "Academic years", "الأعوام الدراسية", CrudApprove),

            // ---- Calendar
            // Deactivate is cancelling an event (BR-GLB-005 — no delete verb), and it is separate
            // from Edit on purpose: correcting the date of a school trip and calling the trip off
            // are not the same authority over a calendar parents have already been shown.
            S(Modules.Calendar, Calendar.Calendar_, "Academic calendar", "التقويم الدراسي", ActionVerb.View, ActionVerb.Edit, ActionVerb.Deactivate, ActionVerb.Approve),

            // ---- Grades
            S(Modules.Grades, Grades.Grades_, "Grade levels", "الصفوف", Crud),
            S(Modules.Grades, Grades.Stages, "Stages", "المراحل", ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Grades, Grades.Profiles, "Grade-year profiles", "ملفات الصف للعام", ActionVerb.Create, ActionVerb.Deactivate),

            // ---- Sections
            S(Modules.Sections, Sections.Sections_, "Sections", "الشعب", CrudApprove),
            S(Modules.Sections, Sections.Roster, "Section roster", "كشف الشعبة", ActionVerb.Edit),
            S(Modules.Sections, Sections.Board, "Assignment board", "لوحة توزيع الطلاب", ActionVerb.View, ActionVerb.Edit),

            // ---- Subjects
            S(Modules.Subjects, Subjects.Subjects_, "Subjects", "المواد الدراسية", Crud),
            S(Modules.Subjects, Subjects.Departments, "Departments", "الأقسام", ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Subjects, Subjects.CurriculumPlan, "Curriculum plan", "الخطة الدراسية", ActionVerb.View, ActionVerb.Create, ActionVerb.Deactivate),

            // ---- Classrooms
            S(Modules.Classrooms, Classrooms.Rooms, "Classrooms", "القاعات", Crud),
            S(Modules.Classrooms, Classrooms.Buildings, "Buildings and floors", "المباني والطوابق", ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Classrooms, Classrooms.Utilization, "Room utilisation", "استغلال القاعات", ReadOnly),

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

            // ---- Transport
            //
            // Post, not Create, opens a trip: opening one is running an engine — it builds the day's
            // roster from the active subscriptions and refuses an unroadworthy bus or an ineligible
            // driver — and the person allowed to run the morning is not always the person allowed to
            // design a route. Approve is the Principal's unroadworthy override and the arrears
            // suspension, both of which are decisions someone else has to make.
            S(Modules.Transport, Transport.Fleet, "Fleet and documents", "الحافلات والوثائق",
                ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Transport, Transport.Staff, "Drivers and attendants", "السائقون والمرافقون",
                ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Transport, Transport.Routes, "Routes and stops", "المسارات والمحطات",
                ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Transport, Transport.Subscriptions, "Transport subscriptions", "اشتراكات النقل",
                ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate, ActionVerb.Approve),
            S(Modules.Transport, Transport.Trips, "Trip console", "لوحة الرحلات",
                ActionVerb.View, ActionVerb.Post, ActionVerb.Edit, ActionVerb.Approve),
            S(Modules.Transport, Transport.Safety, "Safety register", "سجل السلامة",
                ActionVerb.View, ActionVerb.Approve),

            // ---- Discipline
            //
            // The split here is the module's own separation of powers (BR-DCP-002/003/006): recording
            // is open to any teacher, deciding is not, and the person who decided a case may not be
            // the person who reviews its appeal. Create on Incidents is therefore a wide grant and
            // Approve on Cases is a narrow one, which only works if they are separate screens.
            //
            // Configure writes a new version of the code, Approve publishes it to families: writing
            // the school's behaviour rules and putting them in front of parents are different acts,
            // and the second is the Principal's.
            S(Modules.Discipline, Discipline.Code, "Behaviour code", "لائحة السلوك",
                ActionVerb.View, ActionVerb.Configure, ActionVerb.Approve),
            S(Modules.Discipline, Discipline.Incidents, "Record behaviour", "تسجيل السلوك",
                ActionVerb.View, ActionVerb.Create),
            S(Modules.Discipline, Discipline.Cases, "Discipline cases", "قضايا السلوك",
                ActionVerb.View, ActionVerb.Edit, ActionVerb.Approve),
            S(Modules.Discipline, Discipline.Actions, "Action tracker", "متابعة الإجراءات",
                ActionVerb.View, ActionVerb.Edit),
            S(Modules.Discipline, Discipline.Analytics, "Behaviour analytics", "تحليلات السلوك",
                ActionVerb.View),

            // ---- System administration
            //
            // Configure, not Edit, is the verb that changes what a role may do: Edit renames a role
            // and adjusts its 2FA and session policy, which is administration; Configure changes the
            // system's own shape, which is what a permission grant is. They are separate so that
            // "may rename roles" and "may widen them" can be given to different people — and because
            // Configure on this one screen is the permission that can reach every other permission,
            // which makes it the one worth being able to withhold on its own.
            S(Modules.SystemAdministration, SystemAdministration.Roles, "Roles and permissions", "الأدوار والصلاحيات",
                ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate, ActionVerb.Configure),
            S(Modules.SystemAdministration, SystemAdministration.UserRoles, "User role assignments", "إسناد الأدوار للمستخدمين",
                ActionVerb.View, ActionVerb.Create, ActionVerb.Deactivate),
            // Create provisions an account; Edit issues it a new one-time password. No View yet:
            // the directory with the rest of the lifecycle (deactivate, unlock, end sessions) has
            // no screen, so View would be a grant that opens nothing. The two verbs are separate
            // because a receptionist who resets forgotten passwords all morning is not thereby
            // someone who may decide who exists in the system.
            S(Modules.SystemAdministration, SystemAdministration.Users, "User accounts", "حسابات المستخدمين",
                ActionVerb.Create, ActionVerb.Edit),
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
