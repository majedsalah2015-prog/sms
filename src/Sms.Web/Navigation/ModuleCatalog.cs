using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Web.Navigation
{
    /// <summary>
    /// The product modules (docs/Modules/01..36, plus 37 whose scope was opened
    /// on 2026-08-30 outside approved Analysis v1.0) as sidebar-facing
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

            // Module 37 sits with the academic modules although it is numbered
            // past 36: scope opened 2026-08-30, outside approved Analysis v1.0
            // (README Q8, GAP register G2 -> R3). Slice 1 lands the planner;
            // homework, question banks, papers, online sittings and the portal
            // surfaces are later slices.
            M("LRN", "37", "E-Learning", "التعليم الإلكتروني", "bi-mortarboard", "academics", "E-901", "37-E-Learning.md", "Learning", "Index"),

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
            M("MSG", "32", "Messaging", "المراسلات", "bi-chat-dots", "platform", "E-703", "32-Messaging.md", "Messaging", "Index"),
            M("NTF", "33", "Notifications", "الإشعارات", "bi-bell", "platform", "E-703", "33-Notifications.md", "Notifications", "Index"),
            M("AUD", "34", "Audit", "التدقيق", "bi-clipboard-data", "platform", "E-704", "34-Audit.md"),
            M("BAK", "35", "Backup", "النسخ الاحتياطي", "bi-hdd-stack", "platform", "E-704", "35-Backup.md"),

            // Users and permissions. "security" is deliberately not one of the stage groups below:
            // this module is rendered by BuildSecuritySection as one entry per screen, so the loop
            // that turns a module into a single leaf must not also produce one for it. See that
            // method for why it is a section of its own rather than a line under Platform.
            M("SYS", "36", "System Administration", "إدارة النظام", "bi-gear", "security", "E-704", "36-System-Administration.md", "Security", "Index"),
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
        /// <para>
        /// <paramref name="canSeeSectionBoard"/>, <paramref name="canSeeRoles"/> and
        /// <paramref name="canSeeUserRoles"/> are screen-level rights rather than module ones, so
        /// they arrive already answered — the same shape <paramref name="canExportToLedger"/> uses,
        /// because this catalogue has no way to ask a permission question itself.
        /// </para>
        /// </summary>
        public static IReadOnlyList<NavItem> BuildSidebar(
            Func<ModuleInfo, bool>? isVisible = null,
            Func<ModuleInfo, bool>? isPermitted = null,
            bool canExportToLedger = true,
            IReadOnlyList<NavItem>? erpGroups = null,
            bool canSeeSectionBoard = true,
            bool canSeeRoles = true,
            bool canSeeUserRoles = true,
            bool canSeeStudentFinance = true,
            bool canSeeBulkPlacement = true,
            bool canSeeCollection = true)
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

                AddBulkPlacement(key, group, isVisible, canSeeBulkPlacement);
                AddSectionBoard(key, group, isVisible, canSeeSectionBoard);
                AddStudentFinance(key, group, isVisible, canSeeStudentFinance);
                AddCollection(key, group, isVisible, canSeeCollection);

                if (group.Items.Count > 0)
                {
                    items.Add(group);
                }
            }

            var security = BuildSecuritySection(canSeeRoles, canSeeUserRoles);
            if (security.HasChildren)
            {
                items.Add(security);
            }

            var accounting = BuildAccountingGroup(canExportToLedger, erpGroups);
            if (accounting.HasChildren)
            {
                items.Add(accounting);
            }

            return items;
        }

        /// <summary>
        /// Bulk placement (doc/Modules/10 §8, BR-STU-010), given its own entry beside the students
        /// rather than buried inside one child's file.
        /// <para>
        /// It is the step between registering an intake and everything the year does with it. A
        /// student who is registered and not enrolled is invisible to the fee screens, the section
        /// board, attendance and the charge pickers, all of which read through the year's
        /// enrollments — and after a register import that is the whole school. A step that
        /// unblocks four modules cannot be reachable only from the placement screen of one child
        /// the reader would first have to find.
        /// </para>
        /// <para>
        /// Both of the sidebar's filters still apply, through their own route: the Students feature
        /// toggle via <paramref name="isVisible"/> (BR-SET-006), and this screen's own right via
        /// <paramref name="canSeeBulkPlacement"/> (BR-SEC-010). Reading the student directory is
        /// not the right to enrol a year group.
        /// </para>
        /// </summary>
        private static void AddBulkPlacement(
            string groupKey, NavItem group, Func<ModuleInfo, bool>? isVisible, bool canSeeBulkPlacement)
        {
            var students = All.First(m => m.Code == "STU");
            if (groupKey != "people" || !canSeeBulkPlacement || (isVisible != null && !isVisible(students)))
            {
                return;
            }

            var entry = new NavItem(
                "STU-PLACE", "Bulk placement", "الإسناد الجماعي", "bi-people-fill",
                "Students", "BulkPlacement",
                // The dry run renders the screen back instead of redirecting, so the entry owns that
                // action too or the highlight leaves the group mid-decision.
                siblingActions: new[] { "BulkPlacementPreview" });

            // Directly under the students it places. Appended if the Students module is hidden by a
            // filter this entry does not share, so it never silently disappears with a neighbour.
            var index = group.Items.FindIndex(i => i.Key == "STU");
            group.Items.Insert(index < 0 ? group.Items.Count : index + 1, entry);
        }

        /// <summary>
        /// The student assignment board (doc/Modules/06 §8.3, BR-SCN-008), given its own entry beside
        /// the students rather than left reachable only from inside the Sections module.
        /// <para>
        /// It is a screen of Sections, but the question it answers — which student sits in which
        /// section — is a People question, not a structural one: the structure group defines what
        /// sections exist, and this fills them. That is the same reading the launcher already takes,
        /// where the board is a tile of the Students department (<see cref="WorkspaceCatalog"/>), and
        /// a menu that disagreed with the launcher about where a screen lives would make both harder
        /// to trust.
        /// </para>
        /// <para>
        /// Both of the sidebar's filters still apply, through their own route: the module's feature
        /// toggle via <paramref name="isVisible"/> (BR-SET-006 — switching Sections off takes its
        /// board with it), and the user's right to this particular screen via
        /// <paramref name="canSeeSectionBoard"/> (BR-SEC-010). Being able to open the Sections list is
        /// not the same right as being able to move students between them.
        /// </para>
        /// </summary>
        private static void AddSectionBoard(
            string groupKey, NavItem group, Func<ModuleInfo, bool>? isVisible, bool canSeeSectionBoard)
        {
            var sections = All.First(m => m.Code == "SEC");
            if (groupKey != "people" || !canSeeSectionBoard || (isVisible != null && !isVisible(sections)))
            {
                return;
            }

            var board = new NavItem(
                "SEC-BOARD", "Assignment board", "لوحة توزيع الطلاب", "bi-columns-gap",
                "Sections", "Board",
                // Proposing a distribution renders the board back instead of redirecting, so the
                // entry has to own that action too or the highlight jumps groups mid-decision.
                siblingActions: new[] { "Propose" });

            // Directly under the students, whose distribution it is. Appended if the students module
            // is switched off or hidden, so the board never silently disappears with a neighbour.
            var students = group.Items.FindIndex(i => i.Key == "STU");
            group.Items.Insert(students < 0 ? group.Items.Count : students + 1, board);
        }

        /// <summary>
        /// The student-side reading of the fee position (doc/Modules/19 §8.7), given its own entry
        /// under Finance rather than left as a tab inside the Fees module.
        /// <para>
        /// It is a screen of Fees, and it is also the one a finance clerk opens most: the name that
        /// arrives at the counter is a child's, and every other finance entry starts from a document,
        /// a guardian or a catalogue. Burying the only student-first way in is what made staff search
        /// the student directory — which shows no money — and then guess at a parent.
        /// </para>
        /// <para>
        /// Both of the sidebar's filters still apply, through their own route: the Fees feature
        /// toggle via <paramref name="isVisible"/> (BR-SET-006 — switching Fees off takes this with
        /// it), and this screen's own right via <paramref name="canSeeStudentFinance"/> (BR-SEC-010).
        /// Being able to open the charge explorer is not the right to browse every family's balance.
        /// </para>
        /// </summary>
        private static void AddStudentFinance(
            string groupKey, NavItem group, Func<ModuleInfo, bool>? isVisible, bool canSeeStudentFinance)
        {
            var fees = All.First(m => m.Code == "FEE");
            if (groupKey != "finance" || !canSeeStudentFinance || (isVisible != null && !isVisible(fees)))
            {
                return;
            }

            var entry = new NavItem(
                "FEE-STUDENTS", "Student finance", "مالية الطلاب", "bi-person-vcard",
                "Fees", "StudentFinance",
                // The breakdown and the statement are drill-downs of this entry, not screens of their
                // own: without this the highlight would jump to the Fees module the moment a clerk
                // opened one, and the Finance group would close around them.
                siblingActions: new[] { "StudentFinanceDetail", "StudentStatement" });

            // Directly under the fees whose position it reads. Appended if the Fees module is hidden
            // by a filter this entry does not share, so it never silently disappears with a neighbour.
            var feesIndex = group.Items.FindIndex(i => i.Key == "FEE");
            group.Items.Insert(feesIndex < 0 ? group.Items.Count : feesIndex + 1, entry);
        }

        /// <summary>
        /// doc/Modules/20 §8.5 / §10 — the outstanding-fees inquiry, given its own
        /// entry under Finance for the same reason student finance has one.
        /// <para>
        /// It is a screen of Instalment Plans, and nobody looking for it thinks of
        /// it that way: the question is "who owes us money that fell due between
        /// these dates", and the sidebar's instalments entry opens the template
        /// designer. Two clicks deeper into a module named after plan shapes is
        /// where the one screen a finance office opens every month should not be.
        /// </para>
        /// <para>
        /// Gated by its own right (BR-SEC-010): being able to read the instalment
        /// templates is not the right to open the whole school's arrears, still
        /// less to write to every family on it (BR-GLB-102). The instalments
        /// feature toggle still governs it through <paramref name="isVisible"/>
        /// (BR-SET-006) — a school running no plans at all should not be offered
        /// the module's screens by a side door.
        /// </para>
        /// </summary>
        private static void AddCollection(
            string groupKey, NavItem group, Func<ModuleInfo, bool>? isVisible, bool canSeeCollection)
        {
            var installments = All.First(m => m.Code == "INS");
            if (groupKey != "finance" || !canSeeCollection || (isVisible != null && !isVisible(installments)))
            {
                return;
            }

            var entry = new NavItem(
                "INS-COLLECTION", "Outstanding fees", "الرسوم المستحقة", "bi-cash-stack",
                "Installments", "Collection",
                // The printed notice batch is a drill-down of this entry, not a screen of its own —
                // without this the highlight would jump away the moment an officer pressed Print.
                siblingActions: new[] { "CollectionNotices" });

            // Under the instalments module it belongs to, and after student finance when both are
            // present: the two roll screens read as a pair, one per child and one per due date.
            var insIndex = group.Items.FindIndex(i => i.Key == "INS");
            group.Items.Insert(insIndex < 0 ? group.Items.Count : insIndex + 1, entry);
        }

        /// <summary>
        /// Who may use this system, and what each of them may reach — module 36's two built screens
        /// (doc/Modules/36 §8.1-8.2, doc 06 §8), as a section of their own.
        /// <para>
        /// They were one leaf labelled "System administration" at the foot of the Platform group,
        /// which named none of the three things anybody opens it for. The user-role screen had no
        /// entry at all: /security/users was reachable only by typing it, so the one screen that
        /// decides whether a new employee can reach anything was the hardest one in the product to
        /// find. Naming the section for its contents rather than for the module is the point of it.
        /// </para>
        /// <para>
        /// Two rights, not one (BR-SEC-010, pre-answered by the caller as
        /// <c>canExportToLedger</c> is): designing a role reaches every permission in the product,
        /// while handing an existing role out does not, and they are separate permissions precisely
        /// so the first can be withheld from someone who holds the second. A user with neither gets
        /// no section rather than an empty one.
        /// </para>
        /// </summary>
        private static NavItem BuildSecuritySection(bool canSeeRoles, bool canSeeUserRoles)
        {
            var section = new NavItem("security", "Users & permissions", "المستخدمون والصلاحيات", "bi-shield-lock");

            // The everyday one first: roles are designed once and handed out every time somebody
            // joins, moves or leaves.
            if (canSeeUserRoles)
            {
                section.Items.Add(new NavItem(
                    "SYS-USERS", "Users and roles", "المستخدمون وأدوارهم", "bi-person-gear", "Security", "Users"));
            }

            if (canSeeRoles)
            {
                // The list opens into the designer for one role, which is the same screen as far as
                // the menu is concerned — without this the highlight would leave the section the
                // moment a role was opened.
                section.Items.Add(new NavItem(
                    "SYS-ROLES", "Roles and permissions", "الأدوار والصلاحيات", "bi-shield-lock", "Security", "Index",
                    siblingActions: new[] { "Role" }));
            }

            return section;
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
