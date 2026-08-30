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

            /// <summary>Module 32. Human-composed communication: announcements, threads, official letters.</summary>
            public const string Messaging = "MSG";

            /// <summary>Module 33. The administration surface over doc 09's notification engine — templates, gateways, the delivery log, the budget.</summary>
            public const string Notifications = "NTF";

            /// <summary>Module 36. The screens that decide what every other screen may be reached by.</summary>
            public const string SystemAdministration = "SYS";

            /// <summary>
            /// Module 37. E-learning: teaching material, homework and online
            /// assessment. Scope opened 2026-08-30 and NOT part of approved
            /// Analysis v1.0 (README Q8 kept LMS out of v1; GAP register G2).
            /// Portal-facing e-learning screens take <see cref="Portal"/>
            /// permissions, never these, so a portal grant can never widen into
            /// a staff one (doc/Modules/37 §6).
            /// </summary>
            public const string Learning = "LRN";

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

            /// <summary>
            /// Putting a student into a grade-year. <c>Create</c> is the write — the single
            /// placement form and the bulk placement screen both demand it. <c>View</c> opens the
            /// bulk screen without granting the write, so a registrar can read the year's unplaced
            /// roll before anyone hands them the right to change it (BR-SEC-010).
            /// </summary>
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

            /// <summary>
            /// الثوابت — the qualification, university, specialization and bank catalogues the staff
            /// file picks from. Its own screen code rather than a corner of <see cref="File"/>:
            /// authoring a school's reference lists is a different act from filling in one
            /// employee's record, and a registrar who may do the second should not automatically be
            /// able to rename a university on every record that names it (BR-SEC-010).
            /// </summary>
            public const string Reference = "Reference";

            /// <summary>
            /// مسير الرواتب — the monthly payroll run, its register, the payslips and the bank
            /// transfer list (owner request, 2026-08-28).
            /// <para>
            /// Its own screen code, apart from <see cref="Contracts"/>, because it is the same 🔒
            /// restricted category (BR-EMP-003, BR-EMP-010: salary data is HR + Principal only) but
            /// a different act. A payroll officer who runs the month should not thereby be able to
            /// rewrite the contracts the run reads from, and whoever drafts contracts has no
            /// business signing off the payment.
            /// </para>
            /// </summary>
            public const string Payroll = "Payroll";

            /// <summary>
            /// سلف الموظفين — advance requests, their approval and disbursement, the repayment
            /// schedule, and the advances statements (owner request, 2026-08-28).
            /// <para>
            /// Separate from <see cref="Payroll"/>: an advance is decided one employee at a time,
            /// often by a different person from the one who runs the month, and the school-wide
            /// outstanding-advances report is a finance question rather than a payroll one.
            /// </para>
            /// </summary>
            public const string Advances = "Advances";
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

            /// <summary>
            /// doc/Modules/19 §8.7 read from the student's side. <see cref="Position"/> answers
            /// "what does this payer owe"; this one answers "what is this student's fee made of,
            /// and what is left on it" — the question a finance clerk is asked at the counter,
            /// where the name that arrives is the child's and not the guardian's. Separately
            /// grantable because it opens the whole roll: reading one family's position is not
            /// the right to browse every family's (BR-SEC-010).
            /// </summary>
            public const string StudentFinance = "StudentFinance";

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

            /// <summary>doc/Modules/31 — the school's own figures read straight rather than through a widget layout.</summary>
            public const string Statistics = "Statistics";
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

        public static class Messaging
        {
            /// <summary>doc/Modules/32 §8.1 — compose a broadcast, build its audience, pick its channels, submit it for approval and send it.</summary>
            public const string Announcements = "Announcements";
        }

        public static class Notifications
        {
            /// <summary>
            /// doc/Modules/33 §8.2 — the bilingual template studio and its
            /// draft → test-send → publish lifecycle (BR-NTF-001).
            /// </summary>
            public const string Templates = "Templates";

            /// <summary>
            /// doc/Modules/33 §8.3 — the gateways a school reaches parents through, and their
            /// credentials (BR-NTF-003). <c>Configure</c> is the only verb that writes here:
            /// entering a WhatsApp token is not an act of the same kind as filling in a form,
            /// and the person trusted with the rest of the module is not automatically the
            /// person trusted with the school's messaging account.
            /// </summary>
            public const string Providers = "Providers";

            /// <summary>doc/Modules/33 §8.4 — the delivery log, the failure queue, and the retry (BR-NTF-005).</summary>
            public const string Deliveries = "Deliveries";

            /// <summary>doc/Modules/33 §8.5 — what the metered channels have spent this month against their ceiling (BR-NTF-004).</summary>
            public const string Budgets = "Budgets";
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

        /// <summary>
        /// Module 37 §8. Slice 1 builds screens 1 and 2 only; homework (3-5),
        /// the question bank and paper builder (6-7), the sitting and integrity
        /// consoles (8-9), the portal surfaces (10-11) and analytics (12) are
        /// later slices and are deliberately absent rather than declared and
        /// unbuilt — a catalogued screen no action answers is a grant that
        /// opens nothing.
        /// </summary>
        public static class Learning
        {
            /// <summary>§8.1 — the offering x week planner.</summary>
            public const string Planner = "Planner";

            /// <summary>§8.2 — the per-lesson resource library.</summary>
            public const string Resources = "Resources";
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
            S(Modules.Setup, Setup.Documents, "Document types", "أنواع المستندات", ActionVerb.View, ActionVerb.Configure, ActionVerb.Deactivate),
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
            S(Modules.Students, Students.Enrollment, "Enrollment", "التسجيل", ActionVerb.View, ActionVerb.Create),

            // ---- Parents
            S(Modules.Parents, Parents.Directory, "Parent directory", "دليل أولياء الأمور", ActionVerb.View, ActionVerb.Create),
            S(Modules.Parents, Parents.File, "Parent file", "ملف ولي الأمر", ActionVerb.View, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Parents, Parents.Dedup, "Duplicate workbench", "معالجة التكرار", ReadOnly),

            // ---- Employees
            S(Modules.Employees, Employees.Directory, "Employee directory", "دليل الموظفين", ActionVerb.View, ActionVerb.Create),
            S(Modules.Employees, Employees.File, "Employee file", "ملف الموظف", ActionVerb.View, ActionVerb.Edit, ActionVerb.Deactivate, ActionVerb.Approve),
            S(Modules.Employees, Employees.Contracts, "Contracts", "العقود", ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Approve),
            S(Modules.Employees, Employees.OrgChart, "Organization chart", "الهيكل التنظيمي", Crud),
            S(Modules.Employees, Employees.Reference, "Staff reference lists", "ثوابت الموظفين", Crud),

            // Owner request, 2026-08-28. Post is the verb that moves the money — marking a run paid
            // and handing an advance over — and is deliberately distinct from Approve, so signing a
            // register off and paying it can be two people (doc 06 §4.1's own reading of Post).
            S(Modules.Employees, Employees.Payroll, "Payroll", "مسير الرواتب",
                ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate,
                ActionVerb.Approve, ActionVerb.Post, ActionVerb.Print, ActionVerb.Export),
            S(Modules.Employees, Employees.Advances, "Salary advances", "سلف الموظفين",
                ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate,
                ActionVerb.Approve, ActionVerb.Post, ActionVerb.Print),

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

            // Print is a verb of its own here, not decoration: the list and the breakdown are a
            // clerk reading the file, while the statement is a formal document handed to a family
            // over the counter. A school that lets a receptionist look up a balance without letting
            // them issue statements can express exactly that, and cannot if the two share a verb.
            S(Modules.Fees, Fees.StudentFinance, "Student finance", "مالية الطلاب", ActionVerb.View, ActionVerb.Print),

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
            S(Modules.Discounts, Discounts.Types, "Discount types", "أنواع الخصم", ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate),
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
            S(Modules.Dashboards, Dashboards.Widgets, "Widget catalog", "دليل الأدوات", ActionVerb.View, ActionVerb.Create, ActionVerb.Configure, ActionVerb.Edit, ActionVerb.Deactivate),
            S(Modules.Dashboards, Dashboards.Statistics, "School statistics", "إحصائيات المدرسة", ReadOnly),

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

            // ---- Messaging
            //
            // Four verbs on one screen because a broadcast passes through different hands
            // (BR-MSG-001): a homeroom teacher writes one for their own section and sends it
            // themselves; anything wider is decided by a VP or the Principal first. Post
            // rather than Create is the send, because sending runs an engine — it resolves
            // the audience and queues a message per guardian per channel, and on the metered
            // channels it spends the school's money. Whoever may write an announcement is not
            // therefore whoever may spend that.
            //
            // No Submit: submission is not an act here. Saving an announcement wider than a
            // section puts it in PendingApproval by itself, so a separate verb would be a
            // grant that gates nothing.
            S(Modules.Messaging, Messaging.Announcements, "Announcements", "الإعلانات",
                ActionVerb.View, ActionVerb.Create, ActionVerb.Approve, ActionVerb.Post),

            // ---- Notifications
            //
            // Approve is publish (BR-NTF-001): a published version is what every future
            // delivery renders from and is immutable once sent against, so putting one live
            // is a decision, not an edit. Submit is the test send that must precede it —
            // separate because it costs a real message on a real channel.
            // No Deactivate: retiring a template has no screen yet. The port supports it and
            // the entity is ISoftActiveFiltered, but a catalogued verb no action requires is a
            // permission a school can grant and never use.
            S(Modules.Notifications, Notifications.Templates, "Template studio", "استوديو القوالب",
                ActionVerb.View, ActionVerb.Create, ActionVerb.Submit, ActionVerb.Approve),
            S(Modules.Notifications, Notifications.Providers, "Provider console", "كونسول المزوّدين",
                ActionVerb.View, ActionVerb.Configure, ActionVerb.Deactivate),
            S(Modules.Notifications, Notifications.Deliveries, "Delivery operations", "عمليات التسليم",
                ActionVerb.View, ActionVerb.Post),
            S(Modules.Notifications, Notifications.Budgets, "Messaging budget", "ميزانية المراسلة",
                ActionVerb.View, ActionVerb.Configure),

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
            // ---- Learning (module 37, doc/Modules/37 §6)
            // Publish takes Approve, not Edit. The verb taxonomy above fixes
            // Approve as "approve, reject, activate, close, publish, lock", and
            // BR-LRN-003 makes publication the event families see and the event
            // that raises notifications — a different authority from editing a
            // draft nobody can read yet. DEVIATION: §6's table lists only
            // View/Create/Edit/Deactivate for the planner and names no verb for
            // publishing; gating it behind Edit would leave the publication gate
            // with no permission of its own.
            S(Modules.Learning, Learning.Planner, "Lesson planner", "مخطط الدروس", CrudApprove),
            // No Edit verb: material is added and withdrawn, never edited in
            // place — the file itself is re-uploaded as a new version through
            // doc 10, which is Create's business, not Edit's. A catalogued verb
            // no action answers is a grant that opens nothing.
            S(Modules.Learning, Learning.Resources, "Resource library", "مكتبة المصادر",
                ActionVerb.View, ActionVerb.Create, ActionVerb.Deactivate),

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
