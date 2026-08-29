using System;
using System.Linq;
using Sms.Application.Common.Exceptions;

namespace Sms.Web.Models
{
    /// <summary>
    /// Every refusal the service modules can raise, in the reader's language.
    /// <para>
    /// Transport, the library, activities and trips, the clinic, and discipline. Several of these
    /// are safety refusals — an unroadworthy bus, a child handed to the wrong adult, a suspension
    /// longer than the law allows — and a safety refusal that arrives in a language the supervisor
    /// cannot read is not a safeguard, it is an obstacle they will find a way around.
    /// </para>
    /// <para>
    /// The discipline sentences were written once already inside <c>DisciplineController</c> and
    /// have been moved here unchanged, so the module's refusals live where every other module's do
    /// and the coverage test can see them.
    /// </para>
    /// </summary>
    public static partial class UserMessage
    {
        private static string? Services(Exception exception, bool arabic) => exception switch
        {
            // ---------------------------------------------------------------- M23 transport

            StopTimesNotSequentialException => arabic
                ? "مواعيد محطات المسار يجب أن تتصاعد محطةً بعد محطة — راجع الأوقات، فمحطة لا تسبق التي قبلها (BR-TRN-003)."
                : "The route's stop times have to run forward from one stop to the next — check them; no stop comes before the one ahead of it (BR-TRN-003).",

            SubscriptionDatesOutsideYearException => arabic
                ? "تواريخ اشتراك النقل يجب أن تقع داخل العام الدراسي — اختر تواريخ داخل العام (BR-TRN-004)."
                : "Transport subscription dates have to fall inside the academic year — choose dates within it (BR-TRN-004).",

            TransportSubscriptionExistsException => arabic
                ? "لهذا الطالب اشتراك نقل سارٍ في هذا العام — الاشتراك واحد؛ عدّل القائم أو أوقفه قبل إنشاء آخر (BR-TRN-004)."
                : "This student already has a live transport subscription this year — there is one per student; change or suspend the existing one before adding another (BR-TRN-004).",

            BusUnroadworthyException e => arabic
                ? $"لا تُسند رحلة إلى هذه الحافلة: وثائقها ناقصة أو منتهية — {BusDocuments(e, true)}. جدّد الوثيقة قبل التسيير؛ هذا منع سلامة لا يُتجاوز (BR-TRN-001)."
                : $"No trip is assigned to this bus: its papers are missing or expired — {BusDocuments(e, false)}. Renew them before it runs; this is a safety block and is not overridden (BR-TRN-001).",

            DriverNotEligibleException => arabic
                ? "هذا السائق غير مؤهّل لقيادة هذه الحافلة — رخصته مفقودة أو منتهية أو من فئة أدنى مما تتطلبه؛ هذا منع سلامة لا يُتجاوز (BR-TRN-002)."
                : "This driver may not drive this bus — their licence is missing, expired, or of a lower class than the bus requires; this is a safety block and is not overridden (BR-TRN-002).",

            TripAlreadyOpenException => arabic
                ? "لهذا المسار رحلة في هذا اليوم بالفعل — الرحلة واحدة لكل مسار في اليوم؛ افتح الرحلة القائمة (BR-TRN-005)."
                : "This route already has a trip on that day — there is one per route per day; open the existing trip (BR-TRN-005).",

            TripNotInProgressException => arabic
                ? "الرحلة ليست جارية — لا يُسجَّل صعود ولا نزول ولا إقفال إلا على رحلة انطلقت ولم تُقفل بعد (BR-TRN-005)."
                : "The trip is not running — boardings, alightings and close-out are only recorded on a trip that has departed and not yet closed (BR-TRN-005).",

            StudentNotOnTripRosterException => arabic
                ? "هذا الطالب ليس على كشف هذه الرحلة — راجع اشتراكه ومحطته، أو أضِفه إلى الكشف قبل تسجيل صعوده (BR-TRN-005)."
                : "This student is not on this trip's list — check their subscription and stop, or add them to the list before recording them aboard (BR-TRN-005).",

            TripNotClosableException e => arabic
                ? (e.UnresolvedStudentIds.Count > 0
                    ? $"لا تُقفل الرحلة وفيها {Count(e.UnresolvedStudentIds.Count)} طالباً لم يُسجَّل نزولهم — حدّد مصير كل واحد منهم أولاً، فالإقفال إقرار بأن الحافلة خالية (BR-TRN-005)."
                    : "لا تُقفل الرحلة قبل تأكيد المشرف أنه مرّ على الحافلة ووجدها خالية — هذا التأكيد هو ما يمنع نسيان طفل بداخلها (BR-TRN-005).")
                : (e.UnresolvedStudentIds.Count > 0
                    ? $"The trip does not close while {Count(e.UnresolvedStudentIds.Count)} student(s) are unaccounted for — resolve each of them first; closing the trip is a statement that the bus is empty (BR-TRN-005)."
                    : "The trip does not close until the supervisor confirms they swept the bus and found it empty — that confirmation is what stops a child being left aboard (BR-TRN-005)."),

            HandoverNotAuthorizedException => arabic
                ? "هذا الشخص غير مفوَّض باستلام هذا الطالب — راجع قائمة المفوَّضين في ملف الطالب؛ لا يُسلَّم طفل لغير مفوَّض (BR-TRN-006)."
                : "This person is not authorised to collect this student — check the authorised list on the student's file; a child is not handed to anyone else (BR-TRN-006).",

            SuspensionMidTripException => arabic
                ? "الطالب على متن رحلة جارية الآن — لا يُوقف اشتراك النقل في أثنائها، وإلا بقي بلا عودة؛ أوقفه بعد إقفال الرحلة (BR-TRN-008)."
                : "The student is aboard a trip right now — a subscription is never suspended mid-trip, or they are left with no way home; suspend it after the trip closes (BR-TRN-008).",

            RouteCapacityExceededException => arabic
                ? "الحافلة المختارة مقاعدها أقل من عدد ركّاب هذا المسار — المشتركون مسجَّلون ومحصَّلة رسومهم، ولا يُحوَّل بعضهم إلى قائمة انتظار؛ اختر حافلة أوسع أو قسّم المسار (BR-TRN-003)."
                : "The chosen bus seats fewer than this route already carries — those riders are subscribed and charged, and turning some of them back into applicants is not a reassignment; pick a larger bus, or split the route (BR-TRN-003).",

            // ---------------------------------------------------------------- M26 library

            DuplicateBarcodeException => arabic
                ? "هذا الباركود مستخدم لنسخة أخرى — الباركود لا يتكرر؛ امسح باركود النسخة الصحيحة أو أصدر واحداً جديداً (BR-LIB-001)."
                : "This barcode already belongs to another copy — barcodes do not repeat; scan the right copy, or issue a new barcode (BR-LIB-001).",

            CheckoutBlockedException e => Checkout(e, arabic),

            RenewalNotAllowedException => arabic
                ? "لا يمكن تجديد هذه الإعارة — إمّا أنها بلغت حدّ التجديد، وإمّا أن عضواً آخر حجز العنوان؛ أعِد النسخة في موعدها (BR-LIB-003)."
                : "This loan cannot be renewed — it has either reached its renewal limit, or another member has reserved the title; return it on time (BR-LIB-003).",

            ReservationLimitReachedException => arabic
                ? "بلغ هذا العضو حدّ الحجوزات المسموح له — ألغِ حجزاً قائماً قبل إضافة آخر (BR-LIB-004)."
                : "This member has as many reservations as they are allowed — cancel one before adding another (BR-LIB-004).",

            ReplacementPriceUnknownException => arabic
                ? "لا تُعرف قيمة هذه النسخة ولا سعر بديل في سياسة المكتبة، فلا يمكن احتساب رسم الإبدال — أدخل قيمة النسخة أو حدّد سعراً في السياسة (BR-LIB-006)."
                : "This copy has no recorded cost and the library policy names no replacement price, so nothing can be charged — enter the copy's cost, or set a policy price (BR-LIB-006).",

            StocktakeUnresolvedException e => arabic
                ? $"لا يُقفل الجرد وفيه {Count(e.Unresolved)} فرقاً لم يُعالَج — عالِج كل فرق (مفقود، في غير موضعه، مشطوب) ثم أقفل (BR-LIB-008)."
                : $"The stocktake does not close with {Count(e.Unresolved)} discrepancy(ies) outstanding — resolve each one (missing, misshelved, written off) and then close it (BR-LIB-008).",

            LoanNotOpenException => arabic
                ? "هذه الإعارة ليست مفتوحة — النسخة أُعيدت أو أُغلقت الإعارة من قبل."
                : "This loan is not open — the copy has been returned, or the loan was closed already.",

            // ---------------------------------------------------------------- M29 activities and trips

            InvalidProgramStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة البرنامج الحالية — افتحه لترى ما وصل إليه."
                : "That move is not available from the programme's current state — open it to see where it stands.",

            InvalidProgramEnrollmentStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة التسجيل الحالية — قد يكون قد أُلغي أو اكتمل (BR-ACT-002/005)."
                : "That move is not available from the enrolment's current state — it may have been cancelled or completed (BR-ACT-002/005).",

            ConsentRequiredException => arabic
                ? "لا يُفعَّل التسجيل في هذا البرنامج قبل موافقة ولي الأمر — وهذا شرط قاطع لا يُتجاوز بصلاحية (BR-ACT-005)."
                : "This programme's enrolment is not activated before the guardian's consent is on file — and no permission overrides that (BR-ACT-005).",

            TripNotReadyForDepartureException => arabic
                ? "الرحلة غير جاهزة للانطلاق — راجع الثلاثة: نسبة المشرفين إلى الطلاب، وموافقات أولياء الأمور لكل مسجَّل، وتأكيد خطة النقل (BR-ACT-004)."
                : "The trip is not ready to leave — check all three: the supervisor-to-student ratio, a current consent for every enrolled child, and a confirmed transport plan (BR-ACT-004).",

            TripHeadcountMismatchException e => arabic
                ? $"العدد لا يتطابق: غادر {Count(e.DepartedCount)} وعاد المؤكَّد {Count(e.ReturnedCount)} — لا تُقفل الرحلة حتى يتطابق العددان؛ ابحث عن الفارق الآن (BR-ACT-004)."
                : $"The headcount does not match: {Count(e.DepartedCount)} left and {Count(e.ReturnedCount)} are confirmed back — the trip does not close until they agree; account for the difference now (BR-ACT-004).",

            // ---------------------------------------------------------------- M28 health

            SentHomeWithoutVerifiedPickupException => arabic
                ? "لا يُرسَل الطالب إلى بيته إلا مع شخص مفوَّض بالاستلام جرى التحقق منه، أو باستثناء موثَّق مكتوب — لا يُترك طفل مريض بلا مرافق معروف (BR-HLT-005)."
                : "A student goes home only with a verified pickup-authorised person, or under a written documented exception — a sick child is not sent off with nobody identified (BR-HLT-005).",

            MedicationDeviationReasonRequiredException => arabic
                ? "هذه الجرعة تخالف الإذن الدوائي في المقدار أو الوقت أو المدة — اكتب سبب المخالفة، فالمخالفة تُسجَّل ولا تُمنع (BR-HLT-006)."
                : "This dose departs from the medication authorisation in amount, time or date window — write down why; the departure is recorded rather than blocked (BR-HLT-006).",

            VaccinationConsentMissingException => arabic
                ? "لا موافقة لهذا الطالب على حملة التطعيم — لا يُطعَّم طالب بلا موافقة وليّ أمره (BR-HLT-004)."
                : "This student has no consent on file for the vaccination campaign — no student is vaccinated without their guardian's consent (BR-HLT-004).",

            ExposureNoticeAlreadySentException => arabic
                ? "أُرسل إشعار المخالطة هذا بالفعل، والمرسَل نهائي — أصدر إشعاراً جديداً إن استجدّ ما يُبلَّغ به (BR-HLT-009)."
                : "This exposure notice has already gone out, and a sent notice is final — raise a new one if there is more to tell (BR-HLT-009).",

            // ---------------------------------------------------------------- M27 discipline

            InvalidCaseStatusTransitionException => arabic
                ? "هذه الخطوة ليست التالية في مسار القضية (BR-DCP-003)."
                : "That step is not the case's next one (BR-DCP-003).",

            MeritPointsOutOfBoundsException e => arabic
                ? $"{Count(e.Points)} نقطة خارج ما يمنحه هذا التميّز — راجع حدود النوع في لائحة السلوك (BR-DCP-002)."
                : $"{Count(e.Points)} points is outside what this merit may award — check the type's bounds in the behaviour code (BR-DCP-002).",

            DecisionArticleRequiredException => arabic
                ? "يجب أن يستند القرار إلى مادة من لائحة السلوك (BR-DCP-003)."
                : "The decision must cite an article of the behaviour code (BR-DCP-003).",

            StatementsRequiredException => arabic
                ? "لا يُبتّ في قضية جسيمة قبل أخذ إفادة الطالب أو ولي الأمر (BR-DCP-003)."
                : "A serious case cannot be decided before the student or a parent has given a statement (BR-DCP-003).",

            DecisionDeviationReasonRequiredException => arabic
                ? "هذا القرار أخفّ ممّا تقترحه اللائحة — اذكر السبب (BR-DCP-005)."
                : "This decision is lighter than the code proposes — say why (BR-DCP-005).",

            PrincipalApprovalRequiredException => arabic
                ? "هذا القرار يحتاج اعتماد المدير: فهو أشدّ من المقترح، أو فصل، أو قضية بالغة (BR-DCP-004)."
                : "This decision needs the Principal: it is harsher than the proposal, a suspension, or a gravest-level case (BR-DCP-004).",

            SuspensionExceedsPackLimitException e => arabic
                ? $"مدّة الفصل {Count(e.Days)} يوماً تتجاوز ما تسمح به اللائحة النظامية وهو {Count(e.Max)} — أنقص المدة إلى الحد أو دونه (BR-DCP-004)."
                : $"A {Count(e.Days)}-day suspension is longer than the {Count(e.Max)} days the regulation allows — bring it down to the limit or below (BR-DCP-004).",

            AppealNotAllowedException => arabic
                ? "لا تظلّم هنا — القضايا البسيطة لا يُتظلَّم عليها، أو انقضت المهلة، أو قُدِّم تظلّم من قبل (BR-DCP-006)."
                : "No appeal is possible here — minor cases cannot be appealed, the window has closed, or one was already filed (BR-DCP-006).",

            AppealReviewerNotIndependentException => arabic
                ? "لا يراجع التظلّم من أصدر القرار نفسه (BR-DCP-006)."
                : "An appeal cannot be reviewed by the person who took the decision (BR-DCP-006).",

            CaseNotClosableException => arabic
                ? "لا يمكن إغلاق القضية بعد — مهلة التظلّم مفتوحة أو هناك تظلّم لم يُبتّ فيه (BR-DCP-006)."
                : "The case cannot close yet — the appeal window is open or an appeal is undecided (BR-DCP-006).",

            _ => null,
        };

        /// <summary>The issue desk's three refusals, told apart because the librarian's next move differs in each.</summary>
        private static string Checkout(CheckoutBlockedException e, bool arabic) => e.Reason switch
        {
            CheckoutBlockReason.LoanLimitReached => arabic
                ? "بلغ هذا العضو حدّ الإعارات المسموح له — يعيد نسخة قبل أن يستعير أخرى (BR-LIB-003)."
                : "This member is holding as many items as they are allowed — one comes back before another goes out (BR-LIB-003).",

            CheckoutBlockReason.MemberOnHold => arabic
                ? "على هذا العضو غرامات غير مسددة أو إيقاف — سوِّ ما عليه قبل الإعارة (BR-LIB-003)."
                : "This member has unpaid fines or a hold on their record — clear it before lending to them (BR-LIB-003).",

            _ => arabic
                ? $"النسخة «{e.Barcode}» ليست متاحة للإعارة: حالتها «{CopyState(e.CopyStatus, true)}» — وهذا منع مادي لا يُتجاوز بصلاحية (BR-LIB-003)."
                : $"Copy \"{e.Barcode}\" is not available to lend: it is {CopyState(e.CopyStatus, false)} — and no permission lends a copy that is not on the shelf (BR-LIB-003).",
        };

        /// <summary>Where a copy is, as the library screens say it.</summary>
        private static string CopyState(Sms.Domain.Library.CopyStatus status, bool arabic) => status switch
        {
            Sms.Domain.Library.CopyStatus.Available => arabic ? "متاحة" : "available",
            Sms.Domain.Library.CopyStatus.Loaned => arabic ? "معارة" : "on loan",
            Sms.Domain.Library.CopyStatus.Reserved => arabic ? "محجوزة" : "reserved",
            Sms.Domain.Library.CopyStatus.Repair => arabic ? "تحت الإصلاح" : "in repair",
            Sms.Domain.Library.CopyStatus.Lost => arabic ? "مفقودة" : "lost",
            _ => arabic ? "مسحوبة من التداول" : "withdrawn",
        };

        /// <summary>
        /// The bus papers that are missing or out of date, named rather than counted — a driver
        /// cannot renew "two documents", they renew the licence and the insurance.
        /// </summary>
        private static string BusDocuments(BusUnroadworthyException e, bool arabic) => string.Join(
            arabic ? "، " : ", ",
            e.Blockers.Select(kind => BusDocument(kind, arabic)));

        private static string BusDocument(Sms.Domain.Transport.BusDocumentKind kind, bool arabic) => kind switch
        {
            Sms.Domain.Transport.BusDocumentKind.Registration => arabic ? "استمارة التسجيل" : "the registration",
            Sms.Domain.Transport.BusDocumentKind.Insurance => arabic ? "التأمين" : "the insurance",
            _ => arabic ? "فحص السلامة" : "the safety inspection",
        };
    }
}
