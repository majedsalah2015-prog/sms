using System;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Guards;

namespace Sms.Web.Models
{
    /// <summary>
    /// Every refusal the people and structure modules can raise, in the reader's language.
    /// <para>
    /// Admissions, students, the grade ladder, subjects and rooms. Almost all of these are met by a
    /// registrar mid-enrolment, with a parent sitting across the desk — which is the moment a
    /// refusal has to say what to do next, not merely that something was rejected.
    /// </para>
    /// <para>
    /// The "cannot remove, something still points at it" refusals carry a <see cref="UsageReport"/>,
    /// which holds each blocking reference in both languages. That is why they can name what is in
    /// the way in Arabic; the engine composes no sentence for this boundary to guess at.
    /// </para>
    /// </summary>
    public static partial class UserMessage
    {
        private static string? People(Exception exception, bool arabic) => exception switch
        {
            // ---------------------------------------------------------------- M06 admissions

            DuplicateLiveApplicationException => arabic
                ? "لهذا المتقدم طلب قائم بالفعل لم يُغلق — افتح الطلب القائم وتابعه بدل فتح طلب ثانٍ باسمه (BR-ADM-002)."
                : "This applicant already has a live application that has not been closed — open the existing one and carry on with it rather than filing a second in the same name (BR-ADM-002).",

            AgeIneligibleException => arabic
                ? "عمر المتقدم خارج المدى الذي يقبله هذا الصف — راجع تاريخ الميلاد، أو قدّم على الصف الموافق لعمره (BR-GRD-005)."
                : "The applicant's age falls outside what this grade admits — check the date of birth, or apply to the grade their age fits (BR-GRD-005).",

            InvalidApplicationStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة الطلب الحالية — قد يكون الطلب قد بُتّ فيه أو سقط؛ افتحه لترى ما وصل إليه (BR-ADM-005)."
                : "That move is not available from the application's current state — it may already have been decided or lapsed; open it to see where it stands (BR-ADM-005).",

            ApplicationNotReadyForRegistrationException e => arabic
                ? (e.Blocker == RegistrationBlocker.NoParentLinked
                    ? "لا يمكن التسجيل قبل ربط الطلب بوليّ أمر — المقعد يحتاج من يُخاطَب وتُقيَّد عليه الرسوم (BR-ADM-007)."
                    : "لا يُسجَّل إلا طلب معتمد — اعتمد الطلب أولاً ثم سجّل المقعد (BR-ADM-007).")
                : (e.Blocker == RegistrationBlocker.NoParentLinked
                    ? "Registration cannot go ahead until the application is linked to a parent — the seat needs somebody to contact and to bill (BR-ADM-007)."
                    : "Only an approved application can be registered — approve it first, then register the seat (BR-ADM-007)."),

            // ---------------------------------------------------------------- M07 students

            LastFinanciallyResponsibleGuardianException => arabic
                ? "لا بد أن يبقى للطالب وليّ أمر واحد مسؤول مالياً على الأقل — أسنِد المسؤولية المالية لوليّ أمر آخر قبل رفعها عن هذا (BR-STU-003)."
                : "A student must keep at least one financially responsible guardian — give the responsibility to another guardian before taking it off this one (BR-STU-003).",

            InvalidStudentStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة الطالب الحالية — افتح ملف الطالب لترى حالته وما يتاح منها (BR-STU-002)."
                : "That move is not available from the student's current status — open their file to see where they stand and what follows from it (BR-STU-002).",

            DuplicateEnrollmentException => arabic
                ? "للطالب قيد فعّال في هذا العام الدراسي بالفعل — القيد واحد لكل عام؛ أنهِ القيد القائم أو انقله بدل إنشاء ثانٍ (BR-GLB-024)."
                : "The student already has an active enrolment for this academic year — there is one per year; end or transfer the existing one rather than creating a second (BR-GLB-024).",

            EnrollmentYearChangeException => arabic
                ? "تصحيح القيد يغيّر الصف لا العام — والعام الدراسي هو ما تتبعه درجات الطالب وحضوره ورسومه، فنقله إلى عام آخر يُعيد تصنيف ما حدث فعلاً. انتقال الطالب بين الأعوام يتم بالترحيل (BR-GLB-023)."
                : "Correcting an enrolment changes the grade, not the year — attendance, marks and fees all hang off the academic year, so moving the enrolment into another one would re-file what already happened. Moving a student between years is the rollover's job (BR-GLB-023).",

            EnrollmentSeatedException e => arabic
                ? $"الطالب مُسنَد إلى شعبة «{e.SectionNameAr}»، والشعبة تتبع صفاً سنوياً واحداً — أخرِجه من الشعبة أولاً، ثم صحّح الصف، ثم أعِد إسناده."
                : $"The student is seated in section \"{e.SectionNameEn}\", and a section belongs to one grade-year — take them out of the section first, then correct the grade, then seat them again.",

            // ---------------------------------------------------------------- M11 parents

            InvalidResidenceSelectionException e => arabic
                ? (e.Fault == ResidenceSelectionFault.QuarterWithoutLocality
                    ? "اختر المنطقة قبل الحي — الحي وحده لا يحدد مكاناً، والسكن يُحفَظ بالمنطقة ولو لم يكن للحي ذكر (doc/Modules/11 §7)."
                    : "الحي المختار لا يتبع هذه المنطقة — أعد اختيار الحي من قائمة هذه المنطقة، أو صحّح المنطقة أولاً (doc/Modules/11 §7).")
                : (e.Fault == ResidenceSelectionFault.QuarterWithoutLocality
                    ? "Choose the locality before the quarter — a quarter on its own does not name a place, and the residence is complete with the locality even when no quarter is recorded (doc/Modules/11 §7)."
                    : "The chosen quarter does not sit inside that locality — pick the quarter again from this locality's list, or correct the locality first (doc/Modules/11 §7)."),

            DuplicateResidenceCodeException e => arabic
                ? $"الرمز «{e.Code}» مستعمَل في هذا المستوى بالفعل ({ResidenceLevelName(e.Level, arabic: true)}) — الرموز لا تتكرر تحت الأصل نفسه؛ اترك الخانة فارغة ليُولَّد رمز، أو اكتب رمزاً آخر."
                : $"Code \"{e.Code}\" is already in use at that level ({ResidenceLevelName(e.Level, arabic: false)}) — codes do not repeat under the same parent; leave the box empty to have one generated, or type another.",

            ResidenceRowNotFoundException e => arabic
                ? $"لم يعد هذا السجل موجوداً ({ResidenceLevelName(e.Level, arabic: true)}) — قد يكون عُدِّل من شاشة أخرى؛ حدِّث الصفحة وأعد المحاولة."
                : $"That row is no longer there ({ResidenceLevelName(e.Level, arabic: false)}) — it may have been changed in another screen; reload the page and try again.",

            // ---------------------------------------------------------------- M05 grades and the promotion ladder

            DuplicateGradeCodeException => arabic
                ? "يوجد صف بهذا الرمز في المدرسة — رموز الصفوف لا تتكرر؛ اختر رمزاً آخر (BR-GRD-009)."
                : "A grade with this code already exists in the school — grade codes do not repeat; choose another (BR-GRD-009).",

            GradeStructureInUseException e => arabic
                ? $"لا يمكن تغيير هذا الجزء من هيكل الصفوف: ما زال مرتبطاً بـ {e.Usage.Describe(arabic: true)} (BR-GRD-007)."
                : $"This part of the grade structure cannot be changed: {e.Usage.Describe(arabic: false)} still depend on it (BR-GRD-007).",

            InvalidGenderPolicyNarrowingException => arabic
                ? "سياسة الجنس في الصف أو الشعبة تضيّق سياسة المرحلة ولا توسّعها — لا يُفتح للجنسين ما حُصر في أحدهما على مستوى المرحلة (BR-GRD-004)."
                : "A grade or section may narrow its stage's gender policy, never widen it — what the stage limits to one gender does not open to both below it (BR-GRD-004).",

            PromotionPathCycleException => arabic
                ? "مسار الترفيع بهذا الشكل يدور على نفسه — صف يُرفَّع إلى صف يعود إليه؛ راجع سلسلة الترفيع حتى تنتهي إلى صف التخرّج."
                : "This promotion path loops back on itself — a grade promoting into one that promotes back into it; follow the chain through until it ends at the leaving grade.",

            // ---------------------------------------------------------------- M08 subjects and the curriculum plan

            DuplicateSubjectCodeException => arabic
                ? "توجد مادة بهذا الرمز في المدرسة — رموز المواد لا تتكرر؛ اختر رمزاً آخر (BR-SUB-001)."
                : "A subject with this code already exists in the school — subject codes do not repeat; choose another (BR-SUB-001).",

            SubjectInUseException e => arabic
                ? $"لا يمكن إلغاء التفعيل: ما زال مرتبطاً بـ {e.Usage.Describe(arabic: true)}."
                : $"This cannot be deactivated: {e.Usage.Describe(arabic: false)} still reference it.",

            DuplicateOfferingException => arabic
                ? "هذه المادة مقرّرة على هذا الصف في هذا العام بالفعل — المقرر واحد لكل مادة في الصف؛ عدّل القائم بدل إضافة ثانٍ."
                : "This subject is already on this grade's plan for this year — there is one offering per subject per grade; edit the existing one instead of adding a second.",

            InvalidOfferingWeightException => arabic
                ? "المادة المحتسَبة في المعدل تحتاج وزناً أكبر من صفر — إمّا أن تُعطى وزناً، وإمّا أن تُعلَّم غير محتسَبة في المعدل."
                : "A subject that counts towards the GPA needs a weight greater than zero — either give it one, or mark it as not assessed.",

            EndedOfferingNotEditableException => arabic
                ? "هذه المادة أُنهيت بتاريخ، وما انتهى لا يُعدَّل — فدرجات الفصل الماضي وجداوله تشير إليها كما كانت. أضِف مادة جديدة إلى الخطة لتقول شيئاً مختلفاً من الآن فصاعداً (BR-SUB-004)."
                : "This offering has been end-dated, and what has ended is not rewritten — last term's marks and timetables point at it as it was. Add a new offering to say something different from now on (BR-SUB-004).",

            InvalidOfferingPeriodsException => arabic
                ? "المادة في الخطة تحتاج حصة واحدة على الأقل في الأسبوع — فمادة بصفر حصص لا يستطيع الجدول وضعها أبداً."
                : "An offering needs at least one period a week — a subject with zero periods is one the timetable can never place.",

            // ---------------------------------------------------------------- M09 buildings, floors and rooms

            DuplicateRoomCodeException => arabic
                ? "توجد قاعة بهذا الرمز في المدرسة — رموز القاعات لا تتكرر؛ اختر رمزاً آخر (BR-ROM-001)."
                : "A room with this code already exists in the school — room codes do not repeat; choose another (BR-ROM-001).",

            InvalidRoomCapacityException => arabic
                ? "سعة الاختبار لا تتجاوز السعة العادية — سعة الاختبار أقل لتباعد الطلاب، لا أكثر (BR-ROM-002)."
                : "Exam capacity cannot be higher than standard capacity — seating for an exam spreads students out, it does not fit more of them in (BR-ROM-002).",

            RoomInUseException e => arabic
                ? $"لا يمكن إلغاء التفعيل: ما زال يضم {e.Usage.Describe(arabic: true)}."
                : $"This cannot be deactivated: it still holds {e.Usage.Describe(arabic: false)}.",

            RoomUnavailableException => arabic
                ? "القاعة غير متاحة في هذا الوقت — إمّا أنها تحت الصيانة، وإمّا أنها محجوزة؛ راجع جدول إتاحة القاعة أو اختر غيرها (BR-ROM-004)."
                : "The room is not available for that window — it is either under maintenance or already reserved; check the room's availability calendar, or pick another (BR-ROM-004).",

            // ---------------------------------------------------------------- the shared destructive-action guard

            RecordInUseException e => arabic
                ? $"لا يمكن تنفيذ هذا الإجراء: ما زال السجل مرتبطاً بـ {e.Usage.Describe(arabic: true)} — عالِج ما سبق أولاً."
                : $"This cannot go ahead: the record is still referenced by {e.Usage.Describe(arabic: false)} — clear those first.",

            MissingRemovalReasonException => arabic
                ? "اكتب سبب الحذف — السجل الذي يُحذف يُقيَّد سببه في سجل التدقيق، وإلا لم يبقَ منه أثر يُسأل عنه (BR-GLB-032)."
                : "Say why it is being removed — a record that goes away is recorded in the audit trail with its reason, or nothing is left to answer for it (BR-GLB-032).",

            _ => null,
        };

        /// <summary>
        /// The name of one level of the residence hierarchy, as the maintenance screen labels it.
        /// The three tables are edited on one page, so a refusal that did not name its level would
        /// be read against whichever of the three lists the operator was looking at.
        /// </summary>
        private static string ResidenceLevelName(ResidenceLevel level, bool arabic) => level switch
        {
            ResidenceLevel.Governorate => arabic ? "المحافظة" : "governorate",
            ResidenceLevel.Locality => arabic ? "المنطقة" : "locality",
            _ => arabic ? "الحي" : "quarter",
        };
    }
}
