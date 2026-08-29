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
    /// <b>Coverage is now every refusal the product can raise.</b> It began as the setup circle and
    /// the cross-cutting guards while the other two hundred exception types still fell through to
    /// English; the tables below finish that list, and <c>RefusalCoverageTests</c> keeps it closed —
    /// a new exception type with no sentence here fails the build rather than reaching a school in a
    /// language its administrator may not read.
    /// </para>
    /// <para>
    /// Some exceptions used to embed a clause the engine had composed in English — "invalid
    /// because <em>the end date precedes the start date</em>" — and this class translated the frame
    /// around it and kept the clause, which left a half-Arabic sentence on an Arabic screen. Those
    /// engines now carry the reason as a value (an enum, a count, a bilingual
    /// <c>UsageReport</c>) instead of as a sentence, so the whole thing can be said in either
    /// language. That is the pattern for any new refusal that has a "because": put the case in the
    /// exception, put the words here.
    /// </para>
    /// </summary>
    public static partial class UserMessage
    {
        /// <summary>
        /// The sentence to show, in the reader's language.
        /// <para>
        /// Two tables answer in turn. This file holds the refusals that were translated first — the
        /// setup circle, the workflow engine, the cross-cutting guards — and the module tables in
        /// <c>UserMessage.*.cs</c> hold the rest, one file per product area so that adding a module's
        /// refusals does not mean editing a thousand-line switch shared with every other module.
        /// </para>
        /// <para>
        /// The fallback is still the engine's own English sentence, because a wrong-language sentence
        /// beats an empty one. It is no longer expected to be reached: <c>RefusalCoverageTests</c>
        /// fails the build when the product grows an exception type no table names.
        /// </para>
        /// </summary>
        public static string For(Exception exception, bool arabic)
            => Established(exception, arabic)
            ?? ByModule(exception, arabic)
            ?? exception.Message;

        /// <summary>The refusals translated before the tables were split by module.</summary>
        private static string? Established(Exception exception, bool arabic) => exception switch
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
                ? (e.Fault == AcademicYearDateFault.OverlapsAnotherYear
                    ? "هذه التواريخ تتداخل مع عام دراسي قائم في المدرسة — غيّرها، أو عدّل تواريخ العام الآخر أولاً (BR-AYR-001)."
                    : "مدة العام الدراسي يجب أن تكون بين ستة أشهر وأربعة عشر شهراً، وأن ينتهي بعد أن يبدأ — راجع تاريخي البداية والنهاية (BR-AYR-001).")
                : (e.Fault == AcademicYearDateFault.OverlapsAnotherYear
                    ? "These dates overlap an academic year the school already has — change them, or move the other year's dates first (BR-AYR-001)."
                    : "An academic year runs between six and fourteen months and ends after it begins — check the start and end dates (BR-AYR-001)."),

            InvalidPeriodDatesException e => arabic
                ? e.Fault switch
                {
                    PeriodDateFault.EndsBeforeItStarts => $"{PeriodName(e.Kind, true)} يجب أن ينتهي بعد أن يبدأ — راجع تاريخي البداية والنهاية (BR-AYR-007).",
                    PeriodDateFault.NotInsideItsParent => $"{PeriodName(e.Kind, true)} يجب أن يقع داخل المدة التي تحويه — {(e.Kind == SchoolPeriodKind.Term ? "الفترة داخل فصلها" : "الفصل داخل عامه")} (BR-AYR-007).",
                    _ => $"{PeriodName(e.Kind, true)} يتداخل مع غيره من {(e.Kind == SchoolPeriodKind.Term ? "الفترات" : "الفصول")} — لا تتداخل مدتان في التقويم الدراسي (BR-AYR-007).",
                }
                : e.Fault switch
                {
                    PeriodDateFault.EndsBeforeItStarts => $"The {PeriodName(e.Kind, false)} has to end after it begins — check its start and end dates (BR-AYR-007).",
                    PeriodDateFault.NotInsideItsParent => $"The {PeriodName(e.Kind, false)} has to sit inside the span that holds it — {(e.Kind == SchoolPeriodKind.Term ? "a term inside its semester" : "a semester inside its year")} (BR-AYR-007).",
                    _ => $"This {PeriodName(e.Kind, false)} overlaps another one — two spans of the school calendar do not sit on top of each other (BR-AYR-007).",
                },

            AcademicYearInUseException e => arabic
                ? $"لا يمكن تعديل هذا العام الدراسي أو حذفه: ما زال مرتبطاً بـ {e.Usage.Describe(arabic: true)} — عالِج ما سبق أولاً."
                : $"This academic year cannot be changed or removed: {e.Usage.Describe(arabic: false)} still depend on it — clear those first.",

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
                ? e.Reason switch
                {
                    SectionInUseReason.CapacityBelowAssigned => $"السعة المطلوبة {Count(e.Requested)} أقل من عدد الطلاب المسنَدين إلى الشعبة وهو {Count(e.Existing)} — انقل بعضهم أولاً، أو ارفع السعة (BR-SCN-002).",
                    SectionInUseReason.HasHistory => $"للشعبة سجل قائم ({Count(e.Existing)} قيد أو إسناد ريادة) — الشعبة تُغلق ولا تُحذف، حتى يبقى سجل من درس فيها (BR-SCN-007).",
                    _ => "ما زالت سجلات أخرى تشير إلى هذه الشعبة — أغلقها بدل حذفها (BR-SCN-007).",
                }
                : e.Reason switch
                {
                    SectionInUseReason.CapacityBelowAssigned => $"A capacity of {Count(e.Requested)} is below the {Count(e.Existing)} student(s) already in the section — move some of them out first, or raise the capacity (BR-SCN-002).",
                    SectionInUseReason.HasHistory => $"The section has history behind it ({Count(e.Existing)} membership or homeroom record(s)) — a section is closed rather than deleted, so the record of who sat in it survives (BR-SCN-007).",
                    _ => "Other records still point at this section — close it instead of deleting it (BR-SCN-007).",
                },

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

            // ---------------------------------------------------------------- M36 user accounts

            InvalidUserNameException => arabic
                ? $"اسم المستخدم غير صالح: من {Sms.Application.Security.UserNameRules.MinLength} إلى {Sms.Application.Security.UserNameRules.MaxLength} حرفاً، أحرف إنجليزية صغيرة وأرقام و . _ - @ فقط، ويبدأ بحرف أو رقم."
                : $"That user name will not work: {Sms.Application.Security.UserNameRules.MinLength}–{Sms.Application.Security.UserNameRules.MaxLength} characters, lower-case letters, digits and . _ - @ only, starting with a letter or a digit.",

            DuplicateUserNameException => arabic
                ? "اسم المستخدم محجوز — والحساب المعطَّل يحجز اسمه أيضاً، لأن الاسم يبقى لصاحبه. اختر غيره."
                : "That user name is taken — a deactivated account keeps its name too, because the name still belongs to whoever held it. Choose another.",

            PersonAlreadyHasAccountException => arabic
                ? "لهذا الشخص حساب بالفعل — ولكل شخص حساب واحد (BR-GLB-002). ابحث عن حسابه في قائمة الحسابات بدل إنشاء ثانٍ."
                : "This person already has an account — one person holds one account (BR-GLB-002). Find theirs on the accounts list instead of creating a second.",

            SelfAccountDeactivationException => arabic
                ? "لا يمكنك تعطيل الحساب الذي تعمل به الآن — اطلب من زميل يحمل الصلاحية أن يفعل ذلك."
                : "You cannot deactivate the account you are signed in with — ask a colleague who holds the permission to do it.",

            InactiveAccountException => arabic
                ? "الحساب معطَّل، فلا يوجد دخول تُستخدم فيه كلمة مرور جديدة — أعِد تفعيله أولاً."
                : "The account is deactivated, so there is no sign-in for a new password to be used at — reactivate it first.",

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

            // ---------------------------------------------------------------- M20 installment plans
            //
            // Every refusal the assignment console can produce. They were reaching Arabic-speaking
            // collection officers in English, and "Student 1 already has a plan assignment for this
            // year and category" is not a sentence that tells an Arabic reader what to do next.
            // The exceptions carry no typed properties, so these translate by type: the reader knows
            // which student they picked, and the id in the English text was never for them.

            PlanTemplateNotApprovedException => arabic
                ? "هذا القالب غير معتمد، ولا يُسنَد إلا قالب معتمد — اعتمده في مصمّم القوالب أولاً (BR-INS-001)."
                : "This template is not approved, and only an approved template can be assigned — approve it in the template designer first (BR-INS-001).",

            NoChargesToScheduleException => arabic
                ? "لا رسوم مرحّلة لهذا الطالب في هذا العام، ولا شيء يُقسَّم قبل وجود رسوم — رحّل رسومه أولاً (BR-INS-002)."
                : "This student has no posted charges for the year, and there is nothing to split until charges exist — post their fees first (BR-INS-002).",

            PlanAssignmentExistsException => arabic
                ? "لهذا الطالب خطة تقسيط لهذا العام ولهذه الفئة بالفعل — الخطة واحدة لكل طالب في العام الواحد؛ افتح القائمة وأعد جدولتها بدل إنشاء خطة ثانية (BR-INS-002)."
                : "This student already has an installment plan for this year and category — one plan per student per year; open the existing one and reschedule it instead of creating a second (BR-INS-002).",

            ExceptionAssignmentReasonRequiredException => arabic
                ? "الإسناد الاستثنائي يتطلب سبباً مكتوباً، ويُحفظ السبب على الإسناد (BR-INS-002)."
                : "An exception assignment requires a written reason, and the reason is kept on the assignment (BR-INS-002).",

            TemplateCategoryNotMandatoryException => arabic
                ? "إسناد صف كامل يجدول الرسوم الإلزامية فقط، وهذا القالب مقصور على فئة رسوم غير إلزامية فلن يجدول شيئاً — اختر قالباً عاماً أو قالباً على فئة إلزامية (BR-INS-002)."
                : "A grade-wide assignment schedules mandatory fees only, and this template is scoped to a non-mandatory fee category, so it would schedule nothing — choose a general template or one scoped to a mandatory category (BR-INS-002).",

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

            _ => null,
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
        /// The field's name as the screen calls it. Only the fields a user actually meets are
        /// listed; anything else falls back to the entity and field as the model names them, which
        /// is still more use than nothing when a new T1 field appears before this table is updated.
        /// </summary>
        /// <summary>
        /// What the reader calls the field an engine refused to change without a reason.
        /// <para>
        /// The list is closed and complete: exactly the properties carrying
        /// <c>[RequiresAuditReason]</c>, which is the only way to reach this sentence, and
        /// <c>AuditReasonFieldNameTests</c> fails the build when the domain grows one this file
        /// does not name. It has to be complete rather than best-effort, because the fallback used
        /// to print <c>Parent.PrimaryIdNo</c> at an administrator — a class name and a property name,
        /// meaningless in Arabic and barely better in English, on a screen whose whole job at that
        /// moment is to say what needs a justification.
        /// </para>
        /// </summary>
        public static string FieldName(string entityType, string field, bool arabic) => (entityType, field) switch
        {
            // ---------------------------------------------------------------- people: identity

            ("Student", "FirstNameAr") or ("Student", "FirstNameEn") => arabic ? "اسم الطالب" : "the student's name",
            ("Student", "FatherNameAr") or ("Student", "FatherNameEn") => arabic ? "اسم أب الطالب" : "the student's father's name",
            ("Student", "GrandfatherNameAr") or ("Student", "GrandfatherNameEn") => arabic ? "اسم جد الطالب" : "the student's grandfather's name",
            ("Student", "FamilyNameAr") or ("Student", "FamilyNameEn") => arabic ? "اسم عائلة الطالب" : "the student's family name",
            ("Student", "PrimaryIdNo") => arabic ? "رقم هوية الطالب" : "the student's ID number",
            ("Student", "DateOfBirth") => arabic ? "تاريخ ميلاد الطالب" : "the student's date of birth",
            ("Student", "Gender") => arabic ? "جنس الطالب" : "the student's gender",
            ("Student", "Status") => arabic ? "حالة الطالب" : "the student's status",

            ("Parent", "NameAr") or ("Parent", "NameEn") => arabic ? "اسم ولي الأمر" : "the parent's name",
            ("Parent", "PrimaryIdNo") => arabic ? "رقم هوية ولي الأمر" : "the parent's ID number",

            ("Employee", "FirstNameAr") or ("Employee", "FirstNameEn") => arabic ? "اسم الموظف" : "the employee's name",
            ("Employee", "FatherNameAr") or ("Employee", "FatherNameEn") => arabic ? "اسم أب الموظف" : "the employee's father's name",
            ("Employee", "GrandfatherNameAr") or ("Employee", "GrandfatherNameEn") => arabic ? "اسم جد الموظف" : "the employee's grandfather's name",
            ("Employee", "FamilyNameAr") or ("Employee", "FamilyNameEn") => arabic ? "اسم عائلة الموظف" : "the employee's family name",
            ("Employee", "MaritalStatus") => arabic ? "الحالة الاجتماعية للموظف" : "the employee's marital status",
            ("Employee", "BankName") => arabic ? "بنك الموظف" : "the employee's bank",
            ("Employee", "BankAccountNo") => arabic ? "رقم حساب الموظف البنكي" : "the employee's bank account number",
            ("Employee", "PalPayWalletNo") => arabic ? "رقم محفظة بالي بي للموظف" : "the employee's PalPay wallet number",
            ("Employee", "JawwalPayWalletNo") => arabic ? "رقم محفظة جوال بي للموظف" : "the employee's JawwalPay wallet number",

            // ---------------------------------------------------------------- the family's circumstances

            // The father's and mother's life status left the student on 2026-08-24 (owner request):
            // it is Parent.LifeStatus now, one row per person. Parent is T1 but that property is not
            // [RequiresAuditReason], so no entry replaces these two here.
            ("Student", "Religion") => arabic ? "ديانة الطالب" : "the student's religion",
            ("Student", "ResidencyStatus") => arabic ? "حالة إقامة الطالب" : "the student's residency status",
            ("Student", "FinancialStatus") => arabic ? "الحالة المالية للأسرة" : "the family's financial standing",
            ("Student", "RationCardNo") => arabic ? "رقم بطاقة التموين" : "the ration card number",

            // ---------------------------------------------------------------- custody and access

            ("StudentGuardianLink", "IsPrimaryContact") => arabic ? "جهة الاتصال الأساسية للطالب" : "which guardian is the student's primary contact",
            ("StudentGuardianLink", "IsFinanciallyResponsible") => arabic ? "المسؤولية المالية عن الطالب" : "who is financially responsible for the student",
            ("StudentGuardianLink", "IsPickupAuthorized") => arabic ? "تفويض استلام الطالب" : "who may collect the student",
            ("StudentGuardianLink", "IsPortalVisible") => arabic ? "ظهور الطالب في بوابة ولي الأمر" : "whether the student appears in the parent portal",
            ("GateEvent", "IsAuthorizedPickupOverride") => arabic ? "تجاوز تفويض الاستلام عند البوابة" : "the pickup authorisation override at the gate",

            // ---------------------------------------------------------------- money

            ("Contract", "SalaryBasic") => arabic ? "الراتب الأساسي" : "the basic salary",
            ("Contract", "SalaryAllowances") => arabic ? "البدلات" : "the salary allowances",

            // Staff advances (owner request, 2026-08-28). Both are T1 with a mandatory reason for
            // the same reason the salary above is: they decide what leaves somebody's pay.
            ("SalaryAdvance", "Amount") => arabic ? "مبلغ السلفة" : "the advance amount",
            ("SalaryAdvance", "InstallmentCount") => arabic ? "عدد أقساط السلفة" : "the advance's instalment count",
            ("FeeStructureLine", "Amount") => arabic ? "قيمة بند الرسوم" : "the fee line's amount",
            ("DiscountGrant", "RevokedEffectiveDate") => arabic ? "تاريخ سحب الخصم" : "the discount's revocation date",
            ("Installment", "IsWrittenOff") => arabic ? "إعدام القسط" : "writing the instalment off",
            ("GlExportBatch", "VoidReason") => arabic ? "سبب إلغاء دفعة الترحيل المحاسبي" : "the export batch's void reason",
            ("Sale", "VoidReason") => arabic ? "سبب إلغاء عملية المقصف" : "the cafeteria sale's void reason",
            ("StoreSale", "VoidReason") => arabic ? "سبب إلغاء عملية المتجر" : "the store sale's void reason",
            ("TransportSubscription", "SuspendedEffectiveDate") => arabic ? "تاريخ إيقاف اشتراك النقل" : "the transport subscription's suspension date",

            // ---------------------------------------------------------------- what the school decides about itself

            ("School", "NameAr") or ("School", "NameEn") => arabic ? "اسم المدرسة" : "the school's name",
            ("School", "LicenseNumber") => arabic ? "رقم ترخيص المدرسة" : "the school's licence number",
            ("School", "MinistryCode") => arabic ? "الرمز الوزاري للمدرسة" : "the school's ministry code",
            ("School", "Status") => arabic ? "حالة المدرسة" : "the school's status",
            ("SchoolSetting", "Value") => arabic ? "قيمة الإعداد" : "the setting's value",
            ("FeatureToggle", "IsEnabled") => arabic ? "تفعيل الميزة" : "whether the feature is enabled",
            ("NumberingSeries", "FormatTemplate") => arabic ? "قالب صيغة الترقيم" : "the numbering series' format",
            ("CountryPack", "Code") => arabic ? "رمز حزمة الدولة" : "the country pack's code",

            // ---------------------------------------------------------------- academic and pastoral judgements

            ("GradingScale", "NameAr") or ("GradingScale", "NameEn") => arabic ? "اسم سلّم التقدير" : "the grading scale's name",
            ("PromotionCriteria", "OverallPassMark") => arabic ? "درجة النجاح العامة" : "the overall pass mark",
            ("PromotionCriteria", "MaxFailedSubjectsForPromotion") => arabic ? "أقصى عدد مواد راسبة يسمح بالترفيع" : "the number of failed subjects promotion still allows",
            ("AttendanceDay", "Status") => arabic ? "حالة الحضور" : "the attendance status",
            ("CertificateRequest", "ClearanceOverridden") => arabic ? "تجاوز إخلاء الطرف" : "the clearance override",
            ("DisciplineCase", "DeviationReason") => arabic ? "سبب الخروج عن لائحة الانضباط" : "the reason for departing from the discipline policy",
            ("SafetyEvent", "ResolvedAtUtc") => arabic ? "تاريخ معالجة حادثة السلامة" : "when the safety event was resolved",

            // A field nobody has named yet. Never reached for anything the domain actually audits —
            // the test above sees to that — but if it ever is, "this field" is still a sentence a
            // person can read, which "Parent.PrimaryIdNo" was not.
            _ => arabic ? "هذا الحقل" : Humanise(field),
        };

        /// <summary>"PrimaryIdNo" → "primary id no". Not a translation, just not a class name.</summary>
        private static string Humanise(string field) =>
            string.Concat(field.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
