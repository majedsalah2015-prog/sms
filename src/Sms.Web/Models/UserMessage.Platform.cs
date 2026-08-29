using System;
using System.Linq;
using Sms.Application.Attachments;
using Sms.Application.Common.Exceptions;
using Sms.Application.Security;
using Sms.Domain.Messaging;
using Sms.Domain.Notifications;

namespace Sms.Web.Models
{
    /// <summary>
    /// Every refusal the platform modules can raise, in the reader's language.
    /// <para>
    /// Sign-in, roles and permissions, the audit trail, backups and restores, scheduled jobs,
    /// numbering, reports, dashboards, notifications, messaging, attachments, the parent portal,
    /// and the workflow engine's own catalogue.
    /// </para>
    /// <para>
    /// Two of these deliberately say less than the engine knows. A failed sign-in never reveals
    /// whether the username or the password was wrong, because a message that distinguishes them is
    /// an account-enumeration tool; and a portal request for a child the account cannot see is
    /// answered as "not found", never "denied", because "denied" confirms the child exists.
    /// Translating them must not make them more forthcoming than they were in English.
    /// </para>
    /// </summary>
    public static partial class UserMessage
    {
        private static string? Platform(Exception exception, bool arabic) => exception switch
        {
            // ---------------------------------------------------------------- M10 sign-in

            InvalidCredentialsException => arabic
                ? "اسم المستخدم أو كلمة المرور غير صحيحة."
                : "The username or password is incorrect.",

            AccountLockedOutException e => arabic
                ? $"أُوقف الحساب مؤقتاً بعد محاولات دخول فاشلة متتابعة — أعد المحاولة بعد {LockoutWait(e, true)}، أو اطلب من مسؤول النظام فتحه (BR-SEC-002)."
                : $"The account is locked after repeated failed sign-ins — try again in {LockoutWait(e, false)}, or ask a system administrator to unlock it (BR-SEC-002).",

            InvalidTwoFactorCodeException => arabic
                ? "رمز التحقق غير صحيح — الرمز يتغير كل ثلاثين ثانية، فتأكد من أحدث رمز في تطبيق المصادقة ومن ضبط ساعة جهازك (BR-SEC-003)."
                : "That verification code is not right — the code changes every thirty seconds, so use the newest one in your authenticator and check your device's clock (BR-SEC-003).",

            PasswordPolicyViolationException e => arabic
                ? $"كلمة المرور لا تستوفي السياسة: {string.Join("، ", e.Violations.Select(v => PasswordRule(v, true)))} (BR-SEC-001)."
                : $"The password does not meet the policy: {string.Join(", ", e.Violations.Select(v => PasswordRule(v, false)))} (BR-SEC-001).",

            // M11's own refusals — the last permission administrator, a duplicate role code, an
            // uncatalogued grant — were translated before the tables were split and answer from
            // UserMessage.cs, so they are deliberately absent here.

            // ---------------------------------------------------------------- M33 system administration

            ImportNotDryRunException => arabic
                ? "هذه الدفعة ليست في وضع التجربة — لا تُعتمد ولا يُتراجع عنها إلا دفعة جربت أولاً؛ شغّل تجربة ثم اعتمد (BR-SYS-003)."
                : "This import batch is not in dry-run — only a batch that has been trialled is committed or rolled back; run the trial first (BR-SYS-003).",

            ImportRollbackWindowClosedException => arabic
                ? "لم يعد التراجع عن هذه الدفعة ممكناً — اعتُمدت دفعة أحدث منها على القالب نفسه، والتراجع الآن يُسقط بيانات جاءت بعدها؛ صحّح الفارق يدوياً (BR-SYS-003)."
                : "This batch can no longer be rolled back — a later batch has committed against the same template, and undoing this one now would take that later data with it; correct the difference by hand (BR-SYS-003).",

            PurgeNotEligibleException => arabic
                ? "لا يُشغَّل هذا الإتلاف الآن — إمّا أن مدة الاحتفاظ لم تنقضِ بعد، وإمّا أن على البيانات حجزاً قانونياً، وإمّا أن بيانات التدقيق مجمّدة للتحقيق (BR-SYS-005)."
                : "This purge cannot run yet — either the retention horizon has not passed, a legal hold sits on the data, or audit data is frozen pending an investigation (BR-SYS-005).",

            SelfApprovalNotAllowedException => arabic
                ? "أنت من طلب هذه العملية، فلا تكون أنت المعتمد الثاني لها — التأكيد المزدوج يحتاج شخصين (BR-SYS-005)."
                : "You requested this operation, so you cannot also be its second approver — dual confirmation needs two people (BR-SYS-005).",

            InsufficientMaintenanceLeadTimeException => arabic
                ? "موعد الصيانة أقرب من مهلة الإشعار المطلوبة — أخّر الموعد، أو صنّفها صيانة طارئة إن كانت كذلك (BR-SYS-007)."
                : "The maintenance window starts sooner than the required notice period — move it later, or mark it as emergency maintenance if that is what it is (BR-SYS-007).",

            // ---------------------------------------------------------------- M34 audit

            AnomalyHitAlreadyDispositionedException => arabic
                ? "بُتّ في هذا التنبيه من قبل — افتحه لترى القرار ومن اتخذه (BR-AUM-002)."
                : "This alert has already been dispositioned — open it to see the decision and who made it (BR-AUM-002).",

            AuditPurgeFrozenException => arabic
                ? "إتلاف بيانات التدقيق مجمّد حتى يُبتّ في فحص سلامة فشل — لا تُمسّ سجلات التدقيق وهناك شك في سلامتها؛ يراجعها المدقّق أولاً (BR-AUM-001)."
                : "Purging audit data is frozen until a failed integrity check is investigated — audit records are not touched while their soundness is in doubt; an Auditor resolves it first (BR-AUM-001).",

            AuditImmutableException => arabic
                ? "سجل التدقيق يُضاف إليه ولا يُعدَّل ولا يُحذف — لا تملك أي جهة في النظام صلاحية تغييره (BR-AUD-001)."
                : "The audit trail is append-only — no role in this product can change or delete what is in it (BR-AUD-001).",

            // ---------------------------------------------------------------- M35 backup and restore

            SnapshotFailedException => arabic
                ? "فشل أخذ النسخة الاحتياطية قبل العملية، فأُوقفت العملية — لا تُشغَّل عملية لا رجعة فيها بغير نسخة تسبقها؛ راجع سجل النسخ ثم أعد المحاولة (BR-BAK-004)."
                : "The pre-operation backup failed, so the operation was stopped — nothing irreversible runs without a snapshot in front of it; check the backup log and try again (BR-BAK-004).",

            InvalidRestoreCaseTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة طلب الاستعادة الحالية — افتحه لترى خطوته التالية (BR-BAK-005)."
                : "That move is not available from the restore case's current state — open it to see its next step (BR-BAK-005).",

            // ---------------------------------------------------------------- scheduled jobs and numbering

            UnknownJobException => arabic
                ? "لا توجد مهمة مجدولة بهذا الرمز — قائمة المهام مغلقة وتُسجَّل في النظام لا من الشاشة."
                : "There is no scheduled job with that code — the job list is closed and is registered in the system, not from a screen.",

            NoActiveNumberingSeriesException => arabic
                ? "لا يوجد تسلسل ترقيم فعّال لهذا المستند، ولا يصدر مستند بلا رقم — فعّل تسلسلاً في شاشة الترقيم أولاً (BR-NUM-001)."
                : "No numbering series is active for this document, and nothing is issued without a number — activate one on the numbering screen first (BR-NUM-001).",

            // ---------------------------------------------------------------- M30 reports · M31 dashboards

            // The parameter keys are shown as the report designer names them. They are identifiers
            // rather than prose, so they are not translated — and a reader who has to fill one in
            // is looking at a form whose boxes carry the same keys.
            MissingRequiredParametersException e => arabic
                ? $"ينقص التقرير معايير مطلوبة: {string.Join("، ", e.MissingKeys)} — أدخلها ثم شغّله."
                : $"The report is missing required parameters: {string.Join(", ", e.MissingKeys)} — fill them in and run it again.",

            ReportPermissionDeniedException => arabic
                ? "لا تملك صلاحية عرض هذا التقرير (BR-RPT-002)."
                : "You do not hold the permission to view this report (BR-RPT-002).",

            ReportExportNotAllowedException => arabic
                ? "هذا التقرير يحوي بيانات شخصية، وتصديره إلى ملف يحتاج صلاحية تصدير منفصلة عن العرض — يمكنك عرضه على الشاشة (BR-RPT-003)."
                : "This report holds personal data, and saving it to a file needs an export permission separate from viewing — you can still read it on screen (BR-RPT-003).",

            RestrictedReportEmailDeliveryException => arabic
                ? "هذا التقرير مقيَّد ولا يُرسَل بالبريد — جدوِله للتسليم داخل البوابة، حيث يفتحه صاحب الصلاحية وحده (BR-RPT-003)."
                : "This report is restricted and is never emailed — schedule it for portal delivery instead, where only a permitted reader opens it (BR-RPT-003).",

            SubscriptionRecipientNotAuthorizedException => arabic
                ? "المستلم المطلوب لا يملك صلاحية عرض هذا التقرير، والاشتراك لا يمنح صلاحية — امنحه الصلاحية أولاً إن كان أهلاً لها (BR-RPT-006)."
                : "The intended recipient does not hold the permission to view this report, and a subscription does not confer one — grant it first if they should have it (BR-RPT-006).",

            ReportExecutionNotQueuedException => arabic
                ? "هذا التشغيل لم يعد في الانتظار — قد يكون اكتمل أو أخفق؛ افتح سجل التشغيل لترى نتيجته (BR-RPT-005)."
                : "This run is no longer queued — it may have finished or failed; open the run log to see how it ended (BR-RPT-005).",

            WidgetNotPermittedException => arabic
                ? "لا تملك صلاحية البيانات التي تعرضها هذه اللوحة، فلا تُضاف إلى لوحتك — والشاشة لا تعرض ما لا يُسمح لك برؤيته."
                : "You do not hold the permission for the data behind this widget, so it is not added to your dashboard — the screen does not show what you may not see.",

            WidgetDefinitionNotFoundException => arabic
                ? "العنصر المطلوب تعديله غير موجود في السجل — قد يكون الرابط قديماً؛ أعد تحميل سجل العناصر."
                : "The widget you tried to change is not in the registry — the link may be stale; reload the widget registry.",

            // ---------------------------------------------------------------- M32 notifications and messaging

            InvalidTemplatePublishTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة نسخة القالب الحالية — والغالب أنك تنشر نسخة لم تُرسَل تجريبياً بعد؛ أرسل رسالة اختبار أولاً (BR-NTF-001)."
                : "That move is not available from the template version's current state — most often this is publishing a version that was never test-sent; send a test first (BR-NTF-001).",

            StatutorySubscriptionChangeDeniedException => arabic
                ? "هذا الإشعار نظامي ملزم، ولا يُعطَّل إلا باعتماد المدير — فالأسر لها حق في تبلّغه (BR-NTF-002)."
                : "This notification is statutory, and only the Principal can switch it off — families are entitled to receive it (BR-NTF-002).",

            BudgetHardStopException => arabic
                ? "بلغت ميزانية الإرسال لهذه الفترة حدّها، فأُوقف الإرسال غير الطارئ — رسائل السلامة وحدها تمرّ؛ ارفع الميزانية أو انتظر الفترة التالية (BR-NTF-004)."
                : "This period's sending budget has hit its ceiling, so non-urgent messages are stopped — only safety messages get through; raise the budget, or wait for the next period (BR-NTF-004).",

            AnnouncementNotApprovedException => arabic
                ? "الإعلانات الموجَّهة إلى صف أو مرحلة أو المدرسة كلها تحتاج اعتماداً قبل الإرسال — أرسله للاعتماد أولاً (BR-MSG-001)."
                : "An announcement to a grade, a stage or the whole school needs approval before it goes out — send it for approval first (BR-MSG-001).",

            UnroutableTopicException => arabic
                ? "لا توجد قاعدة توجيه لهذا الموضوع في مصفوفة التواصل، فلا يُعرف من يستقبله — عرّف الموضوع في المصفوفة أولاً (BR-MSG-002)."
                : "The communication matrix has no routing for this topic, so there is nobody to send it to — define the topic in the matrix first (BR-MSG-002).",

            UnknownTemplateException => arabic
                ? "لا يوجد قالب إشعار بهذا الرقم في هذه المدرسة."
                : "There is no notification template with that id in this school.",

            UnknownProviderCodeException e => arabic
                ? (e.Code == null
                    ? "لا توجد بوابة مسجّلة بهذا الرقم."
                    : $"«{e.Code}» ليست بوابة يستطيع هذا النظام الإرسال عبرها — اختر من القائمة المعروضة (BR-NTF-003).")
                : (e.Code == null
                    ? "There is no gateway registered with that id."
                    : $"'{e.Code}' is not a gateway this system can send through — choose one from the list offered (BR-NTF-003)."),

            ProviderChannelMismatchException e => arabic
                ? $"هذه البوابة لا تخدم قناة {ChannelName(e.Channel, true)} — سجّلها على القناة التي تخدمها (BR-NTF-003)."
                : $"This gateway does not serve the {ChannelName(e.Channel, false)} channel — register it on a channel it does serve (BR-NTF-003).",

            ProviderInUseException e => arabic
                ? $"هذه البوابة الفعّالة الوحيدة لقناة {ChannelName(e.Channel, true)}، وما زال عليها {e.ActiveRuleCount} من قواعد الاشتراك. تعطيلها يقطع التواصل مع الأسر على هذه القناة دون أن ينبّه أحد — عطّل القواعد أولاً أو سجّل بوابة بديلة (BR-NTF-003)."
                : $"This is the only active gateway for {ChannelName(e.Channel, false)}, and {e.ActiveRuleCount} subscription rule(s) still send on it. Switching it off would cut families off on that channel with nothing to say so — disable the rules first, or register a replacement gateway (BR-NTF-003).",

            ProviderNotConfiguredException e => arabic
                ? $"لا توجد بوابة مُهيّأة وفعّالة لقناة {ChannelName(e.Channel, true)} — سجّل واحدة في كونسول المزوّدين (BR-NTF-003)."
                : $"No configured, active gateway is registered for the {ChannelName(e.Channel, false)} channel — register one in the provider console (BR-NTF-003).",

            RecipientUnreachableException e => arabic
                ? $"لا يوجد عنوان {ChannelName(e.Channel, true)} مسجَّل لهذا المستخدم، فلا سبيل لبلوغه على هذه القناة — أضف رقم الجوال أو البريد في ملفه أولاً (BR-NTF-005)."
                : $"This user has no {ChannelName(e.Channel, false)} address on file, so there is no way to reach them on that channel — add the mobile or the mailbox to their record first (BR-NTF-005).",

            EmptyAudienceException => arabic
                ? "لا يصل هذا الإعلان إلى أحد: الجمهور المختار لا يضم ولي أمر واحد بحساب مفعَّل. راجع الشعبة أو الصف المختار (doc/Modules/32 §9)."
                : "This announcement reaches nobody: the chosen audience contains no guardian with an active account. Check the section or grade you picked (doc/Modules/32 §9).",

            InvalidAudienceTargetException e => arabic
                ? (e.Scope == AudienceScope.SchoolWide
                    ? "الإعلان الموجَّه إلى المدرسة كلها لا يُحدَّد له صف ولا شعبة."
                    : "اختر الشعبة أو الصف أو المرحلة التي يُوجَّه إليها الإعلان.")
                : (e.Scope == AudienceScope.SchoolWide
                    ? "A school-wide announcement takes no section or grade."
                    : "Choose the section, grade or stage this announcement is addressed to."),

            // ---------------------------------------------------------------- M36 attachments

            AttachmentPolicyViolationException e => arabic
                ? $"لم يُقبل الملف: {string.Join("، ", e.Violations.Select(v => UploadRule(v, true)))} (BR-ATT-002/003/008)."
                : $"The file was not accepted: {string.Join(", ", e.Violations.Select(v => UploadRule(v, false)))} (BR-ATT-002/003/008).",

            AttachmentQuarantinedException => arabic
                ? "هذا المرفق محجوز أو لم يُفحص بعد، ولا يُفتح قبل اكتمال الفحص — انتظر انتهاء الفحص أو ارفع نسخة أخرى (BR-ATT-009)."
                : "This attachment is quarantined or not yet scanned, and is not opened until the scan finishes — wait for it, or upload another copy (BR-ATT-009).",

            DocumentTypeNotFoundException => arabic
                ? "لا يوجد نوع مستند فعّال بهذا الرمز — كل مرفق ينتمي إلى نوع معرَّف؛ عرّف النوع في شاشة أنواع المستندات أولاً (BR-ATT-001)."
                : "There is no active document type with that code — every attachment belongs to a defined type; define it on the document-types screen first (BR-ATT-001).",

            // ---------------------------------------------------------------- the parent and student portal

            PortalAccessDeniedException => arabic
                ? "الصفحة المطلوبة غير موجودة (BR-SEC-011)."
                : "The page you asked for does not exist (BR-SEC-011).",

            // ---------------------------------------------------------------- the workflow catalogue

            WorkflowDefinitionMissingException => arabic
                ? "لم يُعرَّف مسار اعتماد لهذا الإجراء في هذه المدرسة — لم يُنشأ بعد أو عُطِّل؛ عرّفه في شاشة مسارات الاعتماد."
                : "No approval workflow is defined for this action in this school — it was never created, or has been deactivated; define it on the workflows screen.",

            _ => null,
        };

        /// <summary>
        /// How long is left on a lockout, rounded up to the minute. An exact timestamp answers a
        /// question nobody asked; "in 4 minutes" is what the person locked out actually needs.
        /// </summary>
        private static string LockoutWait(AccountLockedOutException e, bool arabic)
        {
            var minutes = (int)Math.Ceiling((e.UnlocksAtUtc - DateTime.UtcNow).TotalMinutes);
            if (minutes <= 1)
            {
                return arabic ? "أقل من دقيقة" : "less than a minute";
            }

            return arabic ? $"{Count(minutes)} دقيقة" : $"{Count(minutes)} minutes";
        }

        /// <summary>
        /// The password rules, said as requirements rather than as failures. Kept identical to what
        /// the change-password screen already shows beside the box, so the refusal and the guidance
        /// do not word the same rule two ways.
        /// </summary>
        private static string PasswordRule(PasswordPolicyViolation violation, bool arabic) => violation switch
        {
            PasswordPolicyViolation.TooShort => arabic ? "10 أحرف على الأقل" : "at least 10 characters",
            PasswordPolicyViolation.MissingUppercase => arabic ? "حرف كبير واحد على الأقل" : "an uppercase letter",
            PasswordPolicyViolation.MissingLowercase => arabic ? "حرف صغير واحد على الأقل" : "a lowercase letter",
            PasswordPolicyViolation.MissingDigit => arabic ? "رقم واحد على الأقل" : "a digit",
            PasswordPolicyViolation.MissingSymbol => arabic ? "رمز واحد على الأقل" : "a symbol",
            PasswordPolicyViolation.ReusesRecentPassword => arabic ? "لا تكون إحدى كلمات المرور الخمس الأخيرة" : "not one of your last 5 passwords",
            _ => violation.ToString(),
        };

        /// <summary>
        /// A channel, named the way a school names it rather than the way the enum spells it.
        /// "WhatsApp" stays Latin in Arabic — it is a product name, and transliterating it
        /// would leave a reader searching their phone for something that is not there.
        /// </summary>
        private static string ChannelName(NotificationChannel channel, bool arabic) => channel switch
        {
            NotificationChannel.InApp => arabic ? "الإشعارات داخل النظام" : "in-app",
            NotificationChannel.Email => arabic ? "البريد الإلكتروني" : "email",
            NotificationChannel.Sms => arabic ? "الرسائل النصية" : "SMS",
            NotificationChannel.WhatsApp => "WhatsApp",
            _ => channel.ToString(),
        };

        /// <summary>Why an upload was turned away, in the words the upload box itself uses.</summary>
        private static string UploadRule(UploadLimitViolation violation, bool arabic) => violation switch
        {
            UploadLimitViolation.FormatNotAllowed => arabic ? "صيغة الملف غير مسموح بها لهذا النوع" : "the file format is not allowed for this document type",
            UploadLimitViolation.ExceedsTypeSizeLimit => arabic ? "حجم الملف يتجاوز حدّ هذا النوع" : "the file is larger than this document type allows",
            UploadLimitViolation.ExceedsProductSizeCeiling => arabic ? "حجم الملف يتجاوز الحد الأقصى في النظام" : "the file is larger than the system's overall ceiling",
            UploadLimitViolation.ExpiryDateRequired => arabic ? "هذا النوع يتطلب تاريخ انتهاء" : "this document type requires an expiry date",
            _ => violation.ToString(),
        };
    }
}
