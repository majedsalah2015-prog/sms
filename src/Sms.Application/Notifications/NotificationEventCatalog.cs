using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Notifications;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// doc 09 §3's standard event catalog, as product data rather than prose —
    /// every event this system may notify on, the module that owns it, the
    /// channels and timing it ships configured with, and whether a school is
    /// allowed to switch it off at all.
    /// <para>
    /// <b>Why it exists.</b> BR-NOT-003 says "product defaults ship enabled for
    /// the catalog above", and BR-SET-008 says the setup wizard "offers
    /// per-school adjustment, not per-school invention". Neither sentence could
    /// be true before this file: the defaults lived only in doc 09, no seeder
    /// wrote them, and <c>msg.SubscriptionRule</c> was an empty table with no
    /// screen. A school could therefore neither see what the product intended to
    /// notify on nor adjust it — the only way to get a rule was to have a
    /// developer insert one. This is the "product defaults" both rules refer to.
    /// </para>
    /// <para>
    /// <b>Codes are the ones the modules already publish.</b> Thirteen of these
    /// events are raised today by <c>LibraryAdmin</c>, <c>InstallmentAdmin</c>,
    /// <c>HealthAdmin</c>, <c>TransportAdmin</c> and <c>DisciplineAdmin</c>, and
    /// this catalogue repeats their constants verbatim — a subscription rule is
    /// matched to a publish by string equality on <c>EventCode</c>, so a
    /// prettier code here would silently govern nothing. The rest carry the same
    /// PascalCase shape and are inert until their module raises them;
    /// <see cref="NotificationEvent.HasPublisher"/> says which is which, so a
    /// screen can tell a school the difference instead of implying every row is
    /// live.
    /// </para>
    /// <para>
    /// <b>Where this departs from doc 09 §3, and why.</b>
    /// <list type="bullet">
    /// <item>§3 lists Health's first event as "ClinicVisit". The module does not
    /// raise a notification for every visit — it raises <c>ClinicStudentSentHome</c>,
    /// <c>SchoolEmergencyProtocol</c> and <c>HealthExposureNotice</c>, which are
    /// the visits a parent must hear about. Those three are catalogued;
    /// inventing a fourth "ClinicVisit" code nothing publishes would be a row
    /// that can never fire.</item>
    /// <item>§3's "ExamScheduplePublished" is a typo in the doc and is
    /// catalogued as <c>ExamSchedulePublished</c>.</item>
    /// <item>BR-NOT-003 names OTP among the SMS defaults. OTP is an
    /// authentication message, not one of §3's business events, and no event
    /// code exists for it — it is not catalogued here.</item>
    /// <item>§3 groups "Library/Store/Cafeteria". Only the library overdue and
    /// cafeteria low-balance events are named concretely, so only those are
    /// catalogued; the school store gets no invented event.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>The statutory floor (BR-NOT-007).</b> Ten events carry a floor: a
    /// school may not switch them off, and each states in both languages why.
    /// The rule's other clause — "fees legal notices <em>per school policy</em>"
    /// — is conditional on policy the product does not hold, so the overdue
    /// events are catalogued as adjustable rather than fixed.
    /// </para>
    /// </summary>
    public static class NotificationEventCatalog
    {
        /// <summary>One row of doc 09 §3's table, in the order the doc lists them.</summary>
        public sealed record EventGroup(string ModuleCode, string TitleEn, string TitleAr);

        /// <summary>
        /// One notifiable event. <paramref name="FloorEn"/>/<paramref name="FloorAr"/>
        /// are non-null exactly when the event is statutory — a floor with no
        /// stated reason is not a floor, it is an opinion.
        /// </summary>
        public sealed record NotificationEvent(
            string Code,
            string ModuleCode,
            string TitleEn,
            string TitleAr,
            string RecipientsEn,
            string RecipientsAr,
            IReadOnlyList<NotificationChannel> DefaultChannels,
            NotificationTiming DefaultTiming,
            string? FloorEn,
            string? FloorAr,
            bool HasPublisher)
        {
            /// <summary>BR-NOT-007: recipients cannot opt out, and neither can the school.</summary>
            public bool IsStatutory => FloorEn != null;
        }

        // ------------------------------------------------------------------ module codes
        //
        // ModuleCatalog's codes (it lives in Sms.Web, which this layer may not
        // see), so a grouped row reads back to the same module the sidebar shows.
        // Workflow is the exception: doc 09 §3 lists it as an event source but it
        // is a cross-module engine, not a ModuleCatalog module, so it carries its
        // own code and is grouped on its own.

        private static class M
        {
            public const string Admissions = "ADM";
            public const string Students = "STU";
            public const string Attendance = "ATT";
            public const string Grading = "GRA";
            public const string Certificates = "CRT";
            public const string Fees = "FEE";
            public const string Installments = "INS";
            public const string Payments = "PAY";
            public const string Discipline = "DIS";
            public const string Health = "HLT";
            public const string Transport = "TRN";
            public const string Library = "LIB";
            public const string Cafeteria = "CAF";
            public const string Employees = "EMP";
            public const string Workflow = "WFL";
            public const string System = "SYS";
        }

        private static readonly EventGroup[] GroupList =
        {
            new(M.Admissions, "Admissions", "القبول والتسجيل"),
            new(M.Students, "Students", "الطلاب"),
            new(M.Attendance, "Attendance", "الحضور والغياب"),
            new(M.Grading, "Grading and examinations", "الدرجات والاختبارات"),
            new(M.Certificates, "Certificates", "الشهادات"),
            new(M.Fees, "Fees", "الرسوم الدراسية"),
            new(M.Installments, "Installment plans", "خطط التقسيط"),
            new(M.Payments, "Payments", "المدفوعات"),
            new(M.Discipline, "Discipline", "السلوك والانضباط"),
            new(M.Health, "Health", "الصحة المدرسية"),
            new(M.Transport, "Transportation", "النقل المدرسي"),
            new(M.Library, "Library", "المكتبة"),
            new(M.Cafeteria, "Cafeteria", "المقصف"),
            new(M.Employees, "Employees", "الموظفون"),
            new(M.Workflow, "Workflow and approvals", "سير العمل والاعتمادات"),
            new(M.System, "System and security", "النظام والأمان"),
        };

        // ------------------------------------------------------------------ default channels (BR-NOT-003)
        //
        // "in-app + email; SMS for absence, overdue". Email and SMS are product
        // *intent* — no provider is chosen yet (doc 09 §9 Q1), so what actually
        // gets seeded is decided by NotificationDefaultsSeedContributor, not here.

        private static readonly NotificationChannel[] InAppEmail =
            { NotificationChannel.InApp, NotificationChannel.Email };

        private static readonly NotificationChannel[] InAppEmailSms =
            { NotificationChannel.InApp, NotificationChannel.Email, NotificationChannel.Sms };

        private static readonly NotificationEvent[] All =
        {
            // ---- Admissions (doc 09 §3)
            E("AdmissionApplicationReceived", M.Admissions, "Application received", "استلام طلب التحاق",
                "Applicant's parent", "ولي أمر المتقدم", InAppEmail),
            E("AdmissionDecisionMade", M.Admissions, "Admission decision made", "صدور قرار القبول",
                "Parent", "ولي الأمر", InAppEmail),
            E("AdmissionDocumentsMissing", M.Admissions, "Documents missing", "نقص في المستندات",
                "Parent", "ولي الأمر", InAppEmail),

            // ---- Students (doc 09 §3)
            E("StudentRegistered", M.Students, "Student registered", "تسجيل الطالب",
                "Parent", "ولي الأمر", InAppEmail),
            E("StudentWithdrawalCompleted", M.Students, "Withdrawal completed", "اكتمال الانسحاب",
                "Parent, finance", "ولي الأمر والشؤون المالية", InAppEmail),

            // ---- Attendance (doc 09 §3)
            E("AttendanceStudentAbsent", M.Attendance, "Student absent (same day)", "غياب الطالب (اليوم نفسه)",
                "Parents", "أولياء الأمور", InAppEmailSms,
                floorEn: "A parent has to learn on the day that their child is not where the school expected them to be. Every other absence rule is built on that one message arriving.",
                floorAr: "على ولي الأمر أن يعلم في يومه أن ابنه ليس حيث توقّعته المدرسة. وكل قاعدة غياب أخرى مبنية على وصول هذه الرسالة."),
            E("AttendanceRepeatedAbsence", M.Attendance, "Repeated absence (threshold reached)", "تكرار الغياب (بلوغ الحد)",
                "Parents, homeroom teacher, stage supervisor", "أولياء الأمور ومعلم الفصل ومشرف المرحلة", InAppEmailSms),
            // Digest by default: a late arrival is the one event that recurs daily
            // per child, and BR-NOT-005 exists precisely so it does not become spam.
            E("AttendanceLateArrival", M.Attendance, "Late arrival", "التأخر عن الدوام",
                "Parents", "أولياء الأمور", InAppEmail, NotificationTiming.Digest),

            // ---- Grading and examinations (doc 09 §3)
            E("ExamSchedulePublished", M.Grading, "Exam schedule published", "نشر جدول الاختبارات",
                "Parents, students", "أولياء الأمور والطلاب", InAppEmail),
            E("GradingResultsPublished", M.Grading, "Results published", "نشر النتائج",
                "Parents, students", "أولياء الأمور والطلاب", InAppEmail),
            E("GradingMarkChangedAfterPublication", M.Grading, "Mark changed after publication", "تعديل درجة بعد النشر",
                "Parents", "أولياء الأمور", InAppEmail,
                floorEn: "doc 09 §3 marks this one mandatory and routes it through WF-08: a mark that moves after it was published is corrected in the open, or the published result meant nothing.",
                floorAr: "يجعله دليل الإشعارات §3 إلزامياً ويمرّره عبر WF-08: الدرجة التي تتغير بعد نشرها تُصحَّح في العلن، وإلا فلا معنى للنتيجة المنشورة."),

            // ---- Certificates (doc 09 §3)
            E("CertificateIssued", M.Certificates, "Certificate issued", "إصدار شهادة",
                "Parent", "ولي الأمر", InAppEmail),

            // ---- Fees and payments (doc 09 §3)
            E("FeeInvoicePosted", M.Fees, "Invoice posted", "ترحيل فاتورة",
                "Parent", "ولي الأمر", InAppEmail),
            // Digest: BR-NOT-005's own worked example — one message per parent per
            // day instead of one per child per installment.
            E("InstallmentDueSoon", M.Installments, "Installment due soon (D-7, D-1)", "قرب استحقاق قسط (قبل ٧ أيام وقبل يوم)",
                "Parent", "ولي الأمر", InAppEmail, NotificationTiming.Digest, hasPublisher: true),
            E("InstallmentOverdue", M.Installments, "Installment overdue", "تأخر سداد قسط",
                "Parent, finance", "ولي الأمر والشؤون المالية", InAppEmailSms, NotificationTiming.Digest, hasPublisher: true),
            E("PaymentReceived", M.Payments, "Payment received (receipt)", "استلام دفعة (إيصال)",
                "Parent", "ولي الأمر", InAppEmail),
            E("PaymentRefundProcessed", M.Payments, "Refund processed", "تنفيذ استرداد",
                "Parent", "ولي الأمر", InAppEmail),

            // ---- Discipline (doc 09 §3)
            E("DisciplineIncidentRecorded", M.Discipline, "Incident recorded (severity-gated)", "تسجيل مخالفة (بحسب الدرجة)",
                "Parents", "أولياء الأمور", InAppEmail, hasPublisher: true),
            E("DisciplineDecision", M.Discipline, "Action applied", "تطبيق إجراء",
                "Parents, homeroom teacher", "أولياء الأمور ومعلم الفصل", InAppEmail, hasPublisher: true),

            // ---- Health (doc 09 §3)
            E("ClinicStudentSentHome", M.Health, "Clinic visit — student sent home", "زيارة العيادة — إرسال الطالب إلى المنزل",
                "Parents", "أولياء الأمور", InAppEmailSms, hasPublisher: true,
                floorEn: "The school is releasing a child who arrived well and is leaving unwell. There is no version of that a parent may be left to discover at the end of the day.",
                floorAr: "المدرسة تصرف طفلاً حضر معافى ويغادر متوعكاً. ولا صورة لذلك يُترك فيها ولي الأمر ليكتشفه في آخر اليوم."),
            E("SchoolEmergencyProtocol", M.Health, "Clinic emergency protocol", "بروتوكول الطوارئ الصحية",
                "Parents, emergency contacts", "أولياء الأمور وجهات الاتصال للطوارئ", InAppEmailSms, hasPublisher: true,
                floorEn: "BR-NOT-004 lets this one through quiet hours; switching it off would be switching off the reason quiet hours have an exception.",
                floorAr: "تسمح له القاعدة BR-NOT-004 بتجاوز ساعات الهدوء؛ وإيقافه إيقاف للسبب الذي من أجله وُضع الاستثناء أصلاً."),
            E("MedicationAdministered", M.Health, "Medication administered", "إعطاء دواء",
                "Parents", "أولياء الأمور", InAppEmail, hasPublisher: true),
            E("HealthExposureNotice", M.Health, "Communicable-disease exposure notice", "إشعار مخالطة مرض معدٍ",
                "Parents of the exposed group", "أولياء أمور المخالطين", InAppEmailSms, hasPublisher: true,
                floorEn: "A family cannot act on an exposure it was never told about, and the window in which acting helps is measured in hours.",
                floorAr: "لا تستطيع الأسرة التصرف حيال مخالطة لم تُبلَّغ بها، ونافذة التصرف المفيد تُقاس بالساعات."),
            E("HealthVaccinationDue", M.Health, "Vaccination due", "استحقاق تطعيم",
                "School nurse, parents", "ممرضة المدرسة وأولياء الأمور", InAppEmail, NotificationTiming.Digest),

            // ---- Transportation (doc 09 §3)
            E("TransportRouteChanged", M.Transport, "Route assigned or changed", "إسناد مسار أو تغييره",
                "Parent", "ولي الأمر", InAppEmail, hasPublisher: true),
            E("TransportStudentNotBoarded", M.Transport, "Student did not board", "الطالب لم يصعد الحافلة",
                "Parents", "أولياء الأمور", InAppEmailSms, hasPublisher: true,
                floorEn: "doc 09 §3 marks it immediate and BR-NOT-004 names it as the safety event that bypasses quiet hours — a child is unaccounted for, and the school is the only party that knows.",
                floorAr: "يجعله دليل الإشعارات §3 فورياً، وتسمّيه القاعدة BR-NOT-004 حدث السلامة الذي يتجاوز ساعات الهدوء — فالطفل غير معلوم المكان، والمدرسة وحدها من تعلم ذلك."),
            E("TransportSuspended", M.Transport, "Transport subscription suspended", "إيقاف اشتراك النقل",
                "Parent", "ولي الأمر", InAppEmail, hasPublisher: true),
            E("TransportBusDelayed", M.Transport, "Bus delayed (manual trigger)", "تأخر الحافلة (تشغيل يدوي)",
                "Parents on the route", "أولياء أمور طلاب المسار", InAppEmailSms),

            // ---- Library and cafeteria (doc 09 §3)
            E("LibraryOverdue", M.Library, "Loan overdue", "تأخر إعادة إعارة",
                "Parent or student", "ولي الأمر أو الطالب", InAppEmailSms, NotificationTiming.Digest, hasPublisher: true),
            E("LibraryReservationReady", M.Library, "Reservation ready for collection", "الحجز جاهز للاستلام",
                "Member", "المستعير", InAppEmail, hasPublisher: true),
            E("CafeteriaLowBalance", M.Cafeteria, "Cafeteria wallet running low", "انخفاض رصيد محفظة المقصف",
                "Parent", "ولي الأمر", InAppEmail),

            // ---- Employees (doc 09 §3)
            E("EmployeeLeaveDecision", M.Employees, "Leave decision", "قرار طلب إجازة",
                "Employee", "الموظف", InAppEmail),
            E("EmployeeContractExpiring", M.Employees, "Contract expiring", "قرب انتهاء عقد",
                "HR", "الموارد البشرية", InAppEmail, NotificationTiming.Digest),
            E("EmployeeDocumentExpiring", M.Employees, "Identity document expiring", "قرب انتهاء مستند هوية",
                "HR, employee", "الموارد البشرية والموظف", InAppEmail, NotificationTiming.Digest),

            // ---- Workflow (doc 09 §3, doc 05)
            E("WorkflowStepAssigned", M.Workflow, "Approval step assigned", "إسناد خطوة اعتماد",
                "The assigned approver", "المعتمِد المسنَد إليه", InAppEmail),
            E("WorkflowStepOverdue", M.Workflow, "Approval step overdue", "تأخر خطوة اعتماد",
                "The assigned approver", "المعتمِد المسنَد إليه", InAppEmail),
            E("WorkflowStepEscalated", M.Workflow, "Approval escalated", "تصعيد الاعتماد",
                "The next approver", "المعتمِد التالي", InAppEmail),
            E("WorkflowStepApproved", M.Workflow, "Approved", "الاعتماد",
                "The submitter", "مُقدِّم الطلب", InAppEmail),
            E("WorkflowStepRejected", M.Workflow, "Rejected", "الرفض",
                "The submitter", "مُقدِّم الطلب", InAppEmail),
            E("WorkflowStepReturned", M.Workflow, "Returned for correction", "الإعادة للتصحيح",
                "The submitter", "مُقدِّم الطلب", InAppEmail),

            // ---- System and security (doc 09 §3)
            E("SystemBackupFailed", M.System, "Backup failed", "فشل النسخ الاحتياطي",
                "IT administrator", "مسؤول تقنية المعلومات", InAppEmail,
                floorEn: "A backup nobody was told had failed is not a backup. The whole retention promise in doc 35 rests on somebody hearing about this one.",
                floorAr: "النسخة الاحتياطية التي لم يُبلَّغ أحد بفشلها ليست نسخة احتياطية. ووعد الاحتفاظ كله في الوثيقة ٣٥ قائم على أن يسمع أحدهم بهذا الإشعار."),
            E("SystemJobFailed", M.System, "Scheduled job failed", "فشل مهمة مجدولة",
                "IT administrator", "مسؤول تقنية المعلومات", InAppEmail,
                floorEn: "The nightly jobs raise the fee charges and the dunning steps. A job that stopped running silently looks exactly like a quiet month.",
                floorAr: "المهام الليلية هي التي تُنشئ الرسوم وخطوات المطالبة. والمهمة التي توقفت بصمت تبدو تماماً كشهر هادئ."),
            E("SecurityPasswordChanged", M.System, "Password changed", "تغيير كلمة المرور",
                "The account owner", "صاحب الحساب", InAppEmail,
                floorEn: "This is how an account owner finds out that somebody else changed their password. A school cannot switch off its users' only warning.",
                floorAr: "بهذا يعرف صاحب الحساب أن شخصاً آخر غيّر كلمة مروره. ولا يجوز لمدرسة أن توقف التحذير الوحيد لمستخدميها."),
            E("SecurityTwoFactorChanged", M.System, "Two-factor settings changed", "تغيير إعدادات التحقق بخطوتين",
                "The account owner", "صاحب الحساب", InAppEmail,
                floorEn: "Turning off a second factor is the first move in taking an account over, and the owner is the only person able to say it was not them.",
                floorAr: "إيقاف العامل الثاني أول خطوة في الاستيلاء على حساب، وصاحبه وحده من يستطيع أن يقول إنه لم يفعلها."),
        };

        private static readonly Dictionary<string, NotificationEvent> ByCode =
            All.ToDictionary(e => e.Code, StringComparer.OrdinalIgnoreCase);

        /// <summary>Every catalogued event, in doc 09 §3's own order.</summary>
        public static IReadOnlyList<NotificationEvent> Events => All;

        /// <summary>The owning modules, in the order the screen groups them.</summary>
        public static IReadOnlyList<EventGroup> Groups => GroupList;

        /// <summary>Column order for the subscription matrix — doc 09 §2's channels.</summary>
        public static IReadOnlyList<NotificationChannel> Channels { get; } =
            new[] { NotificationChannel.InApp, NotificationChannel.Email, NotificationChannel.Sms, NotificationChannel.WhatsApp };

        public static bool TryGet(string? code, out NotificationEvent notificationEvent)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                notificationEvent = null!;
                return false;
            }

            return ByCode.TryGetValue(code, out notificationEvent!);
        }

        public static IReadOnlyList<NotificationEvent> ForModule(string moduleCode) =>
            All.Where(e => string.Equals(e.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase)).ToList();

        /// <summary>
        /// Whether a channel has a transport behind it in this deployment, mirroring
        /// <c>Startup</c>'s <c>IChannelSender</c> registrations: in-app is real,
        /// email/SMS/WhatsApp are <c>StubChannelSender</c> because no provider has
        /// been chosen (doc 09 §9 Q1, BR-NOT-009). A rule on a stubbed channel is
        /// configuration recorded ahead of a decision, not a message anybody gets —
        /// which is exactly why the seeder does not enable one.
        /// </summary>
        public static bool ChannelDelivers(NotificationChannel channel) => channel == NotificationChannel.InApp;

        private static NotificationEvent E(
            string code,
            string moduleCode,
            string titleEn,
            string titleAr,
            string recipientsEn,
            string recipientsAr,
            NotificationChannel[] defaultChannels,
            NotificationTiming timing = NotificationTiming.Immediate,
            bool hasPublisher = false,
            string? floorEn = null,
            string? floorAr = null)
            => new(code, moduleCode, titleEn, titleAr, recipientsEn, recipientsAr, defaultChannels, timing, floorEn, floorAr, hasPublisher);
    }
}
