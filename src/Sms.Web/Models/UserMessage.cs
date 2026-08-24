using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Common.Exceptions;
using Sms.Application.Setup;

namespace Sms.Web.Models
{
    /// <summary>
    /// Turns an engine exception into the sentence a user should read, in their language.
    /// <para>
    /// The layering is deliberate. Domain and Application exceptions carry English messages because
    /// that is what a log entry should say, identically in every deployment. But those messages were
    /// reaching Arabic-speaking administrators unchanged, and an English-only refusal is a dead end:
    /// the reader cannot tell what was rejected, let alone what would have been accepted.
    /// </para>
    /// <para>
    /// So translation happens here, at the boundary, keyed on the exception's type and its typed
    /// properties rather than on its text. Anything not listed falls through to the original message
    /// — a wrong-language sentence is bad, an empty one is worse — and
    /// <c>Sms.Web.Tests/TranslatedRefusalTests</c> holds the controllers to calling this instead of
    /// printing <c>ex.Message</c> themselves.
    /// </para>
    /// <para>
    /// <b>Coverage is the setup circle, the workflow engine, and the cross-cutting guards.</b> The
    /// product defines 224 exception types and the rest still fall through to English; that is a
    /// tracked list, not a finished job, and the test above names which controllers are already
    /// migrated so the remainder stays countable instead of invisible.
    /// </para>
    /// <para>
    /// Where an exception embeds a clause the engine composed in English — "invalid because
    /// <em>the end date precedes the start date</em>" — the frame is translated and the clause is
    /// kept. Half a sentence in the reader's language beats none, and inventing a translation for
    /// text this class cannot see would be worse than either.
    /// </para>
    /// </summary>
    public static class UserMessage
    {
        public static string For(Exception exception, bool arabic) => exception switch
        {
            // ---------------------------------------------------------------- cross-cutting guards

            MissingAuditReasonException e => arabic
                ? $"تغيير «{FieldName(e.EntityType, e.FieldName, true)}» يتطلب كتابة سبب — الحقل من الفئة الأولى في التدقيق."
                : $"Changing \"{FieldName(e.EntityType, e.FieldName, false)}\" requires a reason — the field is audited at tier 1.",

            HardDeleteForbiddenException => arabic
                ? "هذا السجل من البيانات الأساسية ولا يُحذف — يُعطَّل بدلاً من ذلك (BR-GLB-005)."
                : "This is master data and cannot be deleted — deactivate it instead (BR-GLB-005).",

            CrossSchoolWriteException => arabic
                ? "هذا السجل يخص مدرسة أخرى، فلا يمكن تعديله من هنا (BR-GLB-010)."
                : "That record belongs to another school and cannot be changed from here (BR-GLB-010).",

            // ---------------------------------------------------------------- M01 system setup

            UnknownSettingKeyException => arabic
                ? "لا يوجد إعداد بهذا المفتاح — قائمة الإعدادات مغلقة ولا تُضاف مفاتيح من الشاشة."
                : "There is no setting with that key — the settings catalogue is closed and keys are not added from a screen.",

            InvalidSettingValueException e => arabic
                ? $"لم تُقبَل هذه القيمة لـ «{SettingLabels.Name(e.Key, true)}»."
                : $"\"{SettingLabels.Name(e.Key, false)}\" would not accept that value.",

            SettingEffectiveDateException => arabic
                ? "لا يمكن ربط هذا الإعداد بعام دراسي على هذا النحو: القيم المالية لا تُربَط بعام انتهى، وليست كل الإعدادات قابلة للربط بعام (BR-SET-005)."
                : "This setting cannot be pinned to an academic year that way: a financial value cannot be pinned to a year that has ended, and not every setting is year-versionable (BR-SET-005).",

            UnknownFeatureException => arabic
                ? "لا توجد ميزة بهذا الرمز (BR-SET-006)."
                : "There is no feature with that code (BR-SET-006).",

            FeatureDependencyException e => arabic
                ? (IsEnabling(e)
                    ? $"لا يمكن التفعيل قبل تفعيل: {FeatureNames(e.Blockers, true)} — التبعية مُلزِمة (BR-SET-006)."
                    : $"لا يمكن التعطيل ما دامت هذه الميزات تعتمد عليها: {FeatureNames(e.Blockers, true)} (BR-SET-006).")
                : (IsEnabling(e)
                    ? $"This cannot be enabled until these are: {FeatureNames(e.Blockers, false)} — the dependency is binding (BR-SET-006)."
                    : $"This cannot be disabled while these depend on it: {FeatureNames(e.Blockers, false)} (BR-SET-006)."),

            UnknownCountryPackException => arabic
                ? "لا توجد حزمة دولة فعّالة بهذا الرمز (BR-SET-004)."
                : "There is no active country pack with that code (BR-SET-004).",

            CountryPackChangeRequiresReasonException => arabic
                ? "تغيير حزمة الدولة بعد بدء التشغيل يتطلب كتابة سبب — اكتب السبب في خانة «السبب» ثم احفظ (BR-SET-004)."
                : "Changing the country pack after go-live requires a reason — write one in the Reason field and save again (BR-SET-004).",

            UnknownSetupStepException => arabic
                ? "لا توجد خطوة إعداد بهذا الرمز (BR-SET-003)."
                : "There is no setup step with that code (BR-SET-003).",

            SetupStepNotReadyException => arabic
                ? "لا يمكن إتمام هذه الخطوة قبل إدخال بياناتها — أكمل ما ينقصها ثم أعد المحاولة (BR-SET-003)."
                : "This step cannot be completed until its data is in place — fill in what is missing and try again (BR-SET-003).",

            SetupIncompleteException e => arabic
                ? (e.PendingSteps.Count == 0
                    ? "لم يُعلَن اكتمال معالج الإعداد بعد (BR-SET-003)."
                    : $"الإعداد غير مكتمل — الخطوات المتبقية: {StepNames(e.PendingSteps, true)} (BR-SET-003).")
                : (e.PendingSteps.Count == 0
                    ? "The setup wizard has not been declared complete yet (BR-SET-003)."
                    : $"Setup is not complete — pending steps: {StepNames(e.PendingSteps, false)} (BR-SET-003)."),

            // ---------------------------------------------------------------- M02/M03 school and years

            InvalidSchoolStatusTransitionException => arabic
                ? "حالة المدرسة لا تنتقل هذا الانتقال (BR-SCH-005)."
                : "The school's status cannot move that way (BR-SCH-005).",

            InvalidAcademicYearStatusTransitionException => arabic
                ? "حالة العام الدراسي لا تنتقل هذا الانتقال (BR-AYR-002)."
                : "The academic year's status cannot move that way (BR-AYR-002).",

            DuplicatePreparationYearException => arabic
                ? "يوجد بالفعل عام دراسي في حالة «تحت الإعداد» — أنهِ إعداده أو ألغِه قبل إنشاء غيره (BR-AYR-002)."
                : "A year is already in Preparation — finish or cancel it before creating another (BR-AYR-002).",

            InvalidAcademicYearDatesException e => arabic
                ? $"تواريخ العام الدراسي غير صالحة: {Clause(e.Message)} (BR-AYR-001)."
                : e.Message,

            InvalidPeriodDatesException e => arabic
                ? $"تواريخ الفترة غير صالحة: {Clause(e.Message)} (BR-AYR-007)."
                : e.Message,

            AcademicYearInUseException e => arabic
                ? $"العام الدراسي مستخدَم: {Clause(e.Message)}"
                : e.Message,

            // ---------------------------------------------------------------- M06 sections

            DuplicateSectionNameException e => arabic
                ? $"توجد شعبة باسم «{e.Name}» في هذا الصف والعام — أسماء الشعب لا تتكرر داخل الصف (BR-SCN-001)."
                : $"A section named \"{e.Name}\" already exists in this grade and year — section names do not repeat within a grade (BR-SCN-001).",

            SectionCapacityPlanExceededException e => arabic
                ? $"سعة {e.RequestedCapacity} تتجاوز حجم الشعبة المخطط للصف ({e.GradeTargetSectionSize}) — عدّل خطة الصف أولاً إن كان هذا هو المقصود (BR-SCN-002)."
                : $"A capacity of {e.RequestedCapacity} exceeds the grade's planned section size ({e.GradeTargetSectionSize}) — change the grade's plan first if that is what you mean (BR-SCN-002).",

            SectionFullException => arabic
                ? "الشعبة بلغت سعتها — تجاوز السعة يحتاج صلاحية استثناء غير متاحة بعد (BR-SCN-002)."
                : "The section is at capacity — going beyond it needs an override permission that does not exist yet (BR-SCN-002).",

            InvalidSectionGenderPolicyException e => arabic
                ? $"سياسة «{Labels.Gender(e.RequestedPolicy, true)}» لا تُضيّق سياسة الصف «{Labels.Gender(e.GradePolicy, true)}» — للشعبة أن تُضيّق سياسة صفها لا أن تُوسّعها (BR-SCN-003)."
                : $"\"{Labels.Gender(e.RequestedPolicy, false)}\" does not narrow the grade's \"{Labels.Gender(e.GradePolicy, false)}\" — a section may narrow its grade's policy, never widen it (BR-SCN-003).",

            SectionGenderMismatchException => arabic
                ? "سياسة الشعبة الجنسية لا تقبل هذا الطالب (BR-SCN-003)."
                : "The section's gender policy does not admit this student (BR-SCN-003).",

            SectionGradeMismatchException => arabic
                ? "هذه الشعبة تتبع صفاً آخر — الطالب يُوضع في شعبة صفه (BR-SCN-001)."
                : "That section belongs to a different grade — a student goes into a section of their own grade (BR-SCN-001).",

            SectionCloseWithMembersException e => arabic
                ? $"لا تُغلق الشعبة وفيها {e.MemberCount} طالباً — انقلهم أولاً (BR-SCN-007)."
                : $"The section still holds {e.MemberCount} student(s) — transfer them before closing it (BR-SCN-007).",

            // The reason is a clause the service composed in English. The frame is
            // translated and the clause kept — half a sentence in the reader's
            // language beats none, and inventing a translation for text this class
            // cannot see would be worse than either.
            SectionInUseException e => arabic
                ? $"الشعبة مستخدَمة: {e.Reason}."
                : e.Message,

            // ---------------------------------------------------------------- M36 roles and permissions

            LastPermissionAdministratorException => arabic
                ? "هذا التغيير لا يُبقي أحداً قادراً على إدارة الصلاحيات — امنح الصلاحية لشخص آخر أولاً (وثيقة 06 §4)."
                : "That change would leave nobody able to administer permissions — grant it to someone else first (doc 06 §4).",

            DuplicateRoleCodeException => arabic
                ? "يوجد دور بهذا الرمز بالفعل — اختر رمزاً آخر."
                : "A role with that code already exists — choose another.",

            UncataloguedPermissionException => arabic
                ? "هذه الصلاحية ليست في فهرس الشاشات، فلا شاشة تتحقق منها — لا فائدة من منحها."
                : "That permission is not in the screen catalogue, so no screen will ever check it.",

            // ---------------------------------------------------------------- doc 05 workflow engine

            WorkflowSelfApprovalException => arabic
                ? "أنت من قدّم هذا الطلب، فلا يمكنك اعتماده — ينتظر شخصاً آخر يحمل دور الاعتماد (BR-WF-003)."
                : "You submitted this request, so you cannot approve it — it waits for another holder of the approving role (BR-WF-003).",

            WorkflowReasonRequiredException => arabic
                ? "هذا الإجراء يحتاج سبباً قبل تسجيله (BR-WF-010)."
                : "This action needs a reason before it can be recorded (BR-WF-010).",

            WorkflowActorNotAuthorizedException => arabic
                ? "لست المعتمِد لهذه المرحلة، أو أن السجل خارج النطاق الذي تملكه (BR-WF-004)."
                : "You are not the approver for this step, or the record is outside the scope you hold (BR-WF-004).",

            WorkflowTransitionNotAllowedException => arabic
                ? "هذه الحركة غير مسموحة من حالة الطلب الحالية — ربما بُتَّ فيه بالفعل (BR-WF-001)."
                : "That move is not allowed from the request's current state — it may have been decided already (BR-WF-001).",

            FeeStructureLineInUseException => arabic
                ? "حُمِّل هذا السعر على طلاب بالفعل — اعكس تلك الرسوم بإشعار دائن قبل سحبه، وإلا بقيت فواتير لا يمكن تفسير مصدرها."
                : "This price has already been charged to students — reverse those charges with a credit note before withdrawing it, or invoices are left with nothing explaining them.",

            FeeStructureLineNotDraftException => arabic
                ? "السعر معتمد ولا يُعدَّل ولا يُحذف (BR-FEE-002) — اسحبه بدلاً من ذلك، فيبقى في السجل ويتوقف عن التحميل."
                : "The price is approved, so it cannot be edited or deleted (BR-FEE-002) — withdraw it instead: it stays on the record and stops being charged.",

            InvalidFeeStructureLineStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة السعر الحالية: المسودة تُعتمد، والمعتمد يُسحب، ولا طريق عائد إلى المسودة (BR-FEE-002)."
                : "That move is not available from the price's current state: a draft is approved, an approved price is withdrawn, and there is no way back to draft (BR-FEE-002).",

            // ---------------------------------------------------------------- M12 employees · M13 teachers
            //
            // Every refusal these two modules' screens can produce. They were reachable and
            // untranslated: the employee file, the contract manager and the assignment matrix each
            // answered an Arabic administrator in English the moment they did something the rules
            // forbid — which, on a screen whose whole job is to enforce those rules, is not a rare
            // path. None of the engine sentences name an id here either; "employee 7" told the
            // reader nothing they could act on.

            InvalidEmployeeStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة الموظف الحالية: النشط يُوقَف أو تُنهى خدمته، والموقوف يعود نشطاً أو تُنهى خدمته، ولا عودة بعد انتهاء الخدمة (BR-EMP-001)."
                : "That move is not available from the employee's current status: an active employee is suspended or offboarded, a suspended one returns to active or is offboarded, and there is no way back once service has ended (BR-EMP-001).",

            OverlappingContractException => arabic
                ? "للموظف عقد آخر يغطي جزءاً من هذه الفترة — أنهِ العقد القائم أو غيّر التواريخ، فعقود الموظف الواحد لا تتداخل (BR-EMP-003)."
                : "The employee already has a contract covering part of this period — end the existing one or change the dates; one employee's contracts do not overlap (BR-EMP-003).",

            InvalidContractStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة العقد الحالية: المسودة تُفعَّل، والساري يُنهى، ولا طريق عائد إلى المسودة (BR-EMP-003)."
                : "That move is not available from the contract's current state: a draft is activated, an active contract is terminated, and there is no way back to draft (BR-EMP-003).",

            ContractNotEditableException => arabic
                ? "العقد لم يعد مسودة، والعقد بعد تفعيله وثيقة لا استمارة — أنهِه واكتب عقداً جديداً بدل تعديل شروطه (BR-EMP-003)."
                : "The contract is no longer a draft, and an activated contract is a document rather than a form — terminate it and write a new one instead of editing its terms (BR-EMP-003).",

            OrgUnitInUseException => arabic
                ? "الوحدة التنظيمية ما زالت مرتبطة بغيرها — انقل الوحدات التابعة لها وأعد إسناد من شغلوا مناصب فيها أولاً، فسجل المناصب لا يُحذف معها (BR-EMP-002)."
                : "The org unit is still referenced — move its child units and reassign anyone who has held a position in it first; the position history is not deleted along with it (BR-EMP-002).",

            EmployeeNotEligibleForTeachingException => arabic
                ? "لا عقد سارياً لهذا الموظف اليوم، ومن لا عقد له لا يحمل صفة معلم ولا إسناداً تدريسياً — فعّل عقده أولاً (BR-TCH-001)."
                : "This employee has no contract in force today, and without one they can hold neither a teaching designation nor a teaching assignment — activate a contract first (BR-TCH-001).",

            DuplicatePrimaryTeacherException => arabic
                ? "لهذه المادة في هذه الشعبة معلم أساسي بالفعل — أنهِ إسناده، أو أضف الجديد معلماً مشاركاً (BR-TCH-005)."
                : "This subject already has a primary teacher in this section — end that assignment, or add the new teacher as a co-teacher instead (BR-TCH-005).",

            LoadExceededException => arabic
                ? "هذا الإسناد يتجاوز الحد الأسبوعي لنصاب المعلم — أنقص نصابه أو أسند الحصة لغيره؛ والتجاوز ممكن لكنه قرار صريح يُسجَّل (BR-TCH-004)."
                : "This assignment would take the teacher past their weekly maximum — reduce their load or give the class to someone else; exceeding it is possible, but it is an explicit choice and it is logged (BR-TCH-004).",

            _ => exception.Message,
        };

        /// <summary>
        /// <see cref="FeatureDependencyException"/> composes two different sentences and does not
        /// expose which; the enable form is the one that says "requires".
        /// </summary>
        private static bool IsEnabling(FeatureDependencyException e)
            => e.Message.Contains("requires", StringComparison.OrdinalIgnoreCase);

        /// <summary>Feature codes as the features screen names them, so a refusal and the toggle agree.</summary>
        private static string FeatureNames(IReadOnlyList<string> codes, bool arabic)
            => string.Join("، ", codes.Select(code =>
                FeatureCatalog.TryGet(code, out var feature) ? (arabic ? feature.TitleAr : feature.TitleEn) : code));

        /// <summary>Setup step codes as the wizard names them.</summary>
        private static string StepNames(IReadOnlyList<string> codes, bool arabic)
            => string.Join("، ", codes.Select(code =>
                SetupWizardSteps.TryGet(code, out var step) ? (arabic ? step.TitleAr : step.TitleEn) : code));

        /// <summary>
        /// The engine's own explanatory clause, kept as it was written. These exceptions are
        /// composed as "frame: clause (BR-x)", so the clause is what sits between the first colon
        /// and the rule reference — translated frame, untranslated detail, which is the honest
        /// halfway house until the engines carry structured reasons instead of sentences.
        /// </summary>
        private static string Clause(string message)
        {
            var start = message.IndexOf(": ", StringComparison.Ordinal);
            if (start < 0)
            {
                return message;
            }

            var clause = message[(start + 2)..];
            var rule = clause.LastIndexOf(" (BR-", StringComparison.Ordinal);
            return (rule < 0 ? clause : clause[..rule]).TrimEnd('.');
        }

        /// <summary>
        /// The field's name as the screen calls it. Only the fields a user actually meets are
        /// listed; anything else falls back to the entity and field as the model names them, which
        /// is still more use than nothing when a new T1 field appears before this table is updated.
        /// </summary>
        private static string FieldName(string entityType, string field, bool arabic) => (entityType, field) switch
        {
            ("SchoolSetting", "Value") => arabic ? "قيمة الإعداد" : "the setting's value",
            ("Student", "FirstNameAr") or ("Student", "FirstNameEn") => arabic ? "اسم الطالب" : "the student's name",
            ("Student", "FamilyNameAr") or ("Student", "FamilyNameEn") => arabic ? "اسم عائلة الطالب" : "the student's family name",
            ("Student", "PrimaryIdNo") => arabic ? "رقم هوية الطالب" : "the student's ID number",
            ("Student", "DateOfBirth") => arabic ? "تاريخ ميلاد الطالب" : "the student's date of birth",
            ("Parent", "NameAr") or ("Parent", "NameEn") => arabic ? "اسم ولي الأمر" : "the parent's name",
            ("Employee", "FirstNameAr") or ("Employee", "FirstNameEn") => arabic ? "اسم الموظف" : "the employee's name",
            ("AttendanceDay", "Status") => arabic ? "حالة الحضور" : "the attendance status",
            ("School", "NameAr") or ("School", "NameEn") => arabic ? "اسم المدرسة" : "the school's name",
            ("DiscountGrant", "RevokedEffectiveDate") => arabic ? "تاريخ سحب الخصم" : "the discount's revocation date",
            ("CountryPack", "Code") => arabic ? "رمز حزمة الدولة" : "the country pack's code",
            _ => arabic ? $"{entityType}.{field}" : $"{entityType}.{field}",
        };
    }
}
