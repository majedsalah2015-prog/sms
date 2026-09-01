using System;
using System.Linq;
using Sms.Application.Common.Exceptions;
using Sms.Domain.Discounts;
using Sms.Domain.Store;

namespace Sms.Web.Models
{
    /// <summary>
    /// Every refusal the money modules can raise, in the reader's language.
    /// <para>
    /// Fees, discounts, instalments, payments, the cafeteria and store counters, and the ledger
    /// export. These are the refusals most likely to be met by someone who is not an administrator —
    /// a cashier at a till, a collections officer on the phone to a parent — and the least
    /// forgiving of a sentence the reader cannot parse: the queue does not stop while they work out
    /// what "insufficient wallet balance" meant.
    /// </para>
    /// <para>
    /// Internal ids are dropped rather than translated. "Charge 4182 is not in Posted status" names
    /// a row in a table nobody at the counter can see; the operator already knows which charge they
    /// clicked, and what they need is the rule and the way out of it.
    /// </para>
    /// </summary>
    public static partial class UserMessage
    {
        private static string? Finance(Exception exception, bool arabic) => exception switch
        {
            // ---------------------------------------------------------------- M19 fees and charges

            // ---------------------------------------------------------------- M19 §8.7 student fee file

            EmptyFeeFileCommitException => arabic
                ? "لم تُحدَّد أي بنود ولا قالب تقسيط ولا خصم — اختر ما تريد اعتماده للطالب أولاً."
                : "No fee items, installment template or discount were selected — choose what to approve for the student first.",

            StudentNotEnrolledForFeeFileException => arabic
                ? "الطالب غير مسجّل في العام الحالي، وقائمة الأسعار تخصّ صفاً في عام — سجّله أولاً ثم اعتمد ماليته (BR-FEE-002)."
                : "The student is not enrolled in the working year, and a price list belongs to a grade within a year — enrol them first, then approve their finances (BR-FEE-002).",

            FeeItemAlreadyBilledException => arabic
                ? "هذا البند مفوتَر للطالب في هذا العام بالفعل — رُحّل بينما كانت الشاشة مفتوحة؛ حدّث الصفحة لترى وضعه الحالي."
                : "This item is already billed to the student for this year — it was posted while the screen was open; refresh the page to see where it now stands.",

            FeeItemAdjustmentNotLowerException => arabic
                ? "الفاتورة المرحّلة لا تُرفَع قيمتها، تُخفَّض فقط بإشعار دائن — أدخل مبلغاً أقل من الحالي، وإن أردت زيادة فأضف بنداً جديداً (BR-GLB-062)."
                : "A posted invoice is only ever brought down, by credit note — enter an amount lower than the current one; to bill more, add a new item (BR-GLB-062).",

            ChargeAlreadyFullyRelievedException => arabic
                ? "لم يبقَ من هذا البند شيء — أعفته الإشعارات الدائنة والخصومات بالكامل."
                : "Nothing is left on this item — credit notes and discounts have already relieved it in full.",

            FeeStructureLineNotApprovedException => arabic
                ? "لا يوجد سعر معتمد لهذا الصف وهذه الفئة، ولا تُحتسَب رسوم بغير سعر معتمد — اعتمد السطر في هيكل الرسوم أولاً (BR-FEE-002)."
                : "There is no approved price for this grade and fee category, and nothing can be charged without one — approve the line in the fee structure first (BR-FEE-002).",

            FeeStructureLineAlreadyExistsException => arabic
                ? "لهذا الصف وهذه الفئة سطر سعر بالفعل — السطر واحد لكل صف وفئة؛ عدّل القائم بدل إضافة ثانٍ."
                : "This grade and fee category already have a price line — there is one line per grade and category; edit the existing one instead of adding a second.",

            ChargeNotPostedException => arabic
                ? "هذه المطالبة ليست مرحّلة، والعمليات المالية لا تُجرى إلا على مطالبة مرحّلة — رحّلها أولاً (BR-GLB-062)."
                : "This charge is not posted, and financial operations only run against a posted charge — post it first (BR-GLB-062).",

            CreditNoteExceedsChargeException => arabic
                ? "قيمة الإشعار الدائن تتجاوز المتبقي من المطالبة — لا يُردّ أكثر مما طولب به؛ أنقص القيمة إلى المتبقي أو أقل."
                : "The credit note is larger than what is left on the charge — nothing may be credited beyond what was billed; reduce it to the remaining balance or less.",

            ChargeHasActivityException => arabic
                ? "على هذه المطالبة دفعات أو إشعارات دائنة أو خصومات، ولا تُلغى مطالبة تحرّكت — صحّحها بإشعار دائن يبقى في السجل (BR-GLB-062)."
                : "This charge has payments, credit notes or discounts against it, and a charge that has moved is never voided — correct it with a credit note, which stays on the record (BR-GLB-062).",

            FeeCategoryInUseException e => arabic
                ? $"لا يمكن تعطيل فئة الرسوم: يشير إليها {Count(e.StructureLines)} سطر سعر و{Count(e.Charges)} مطالبة — اسحب أسطر السعر أولاً، وأما المطالبات فهي سجل لا يُنقض."
                : $"The fee category cannot be deactivated: {Count(e.StructureLines)} price line(s) and {Count(e.Charges)} charge(s) point at it — withdraw the price lines first; the charges are history and stay.",

            // ---------------------------------------------------------------- M21 discounts and scholarships

            DiscountTypeNotFoundException => arabic
                ? "نوع الخصم المطلوب تعديله غير موجود — قد يكون حُذف من مدرسة أخرى أو أن الرابط قديم؛ أعد تحميل فهرس الأنواع."
                : "The discount type you tried to change is not there — the link may be stale, or the row belongs to another school; reload the type catalog.",

            DiscountStackingViolationException => arabic
                ? "خصومات الطالب الحالية لا تسمح بإضافة هذا الخصم فوقها — إمّا أن أحدها غير قابل للجمع، أو أن المجموع يتجاوز السقف؛ اسحب خصماً قائماً أو أنقص النسبة (BR-DIS-001)."
                : "The student's existing discounts do not allow this one on top — either one of them does not stack, or the combined percentage passes the cap; withdraw an existing grant or lower the rate (BR-DIS-001).",

            HardshipDocumentationRequiredException => arabic
                ? "هذا النوع من الخصم اجتماعي ويلزمه إرفاق مستندات الحالة قبل الاعتماد — أرفقها ثم أعد المحاولة (BR-DIS-003)."
                : "This discount type is a hardship one and needs the supporting documents attached before it can be approved — attach them and try again (BR-DIS-003).",

            ScholarshipEnvelopeExhaustedException => arabic
                ? "نفدت مخصصات برنامج المنح — بلغ عدد المستفيدين أو المبلغ حدّه؛ يلزم قرار من المالك لتوسيع المخصص أو الانتظار للعام القادم (BR-DIS-004)."
                : "The scholarship programme's envelope is used up — either the headcount or the amount has hit its cap; widening it is an Owner decision, otherwise it waits for next year (BR-DIS-004).",

            InvalidDiscountGrantStateException e => arabic
                ? (e.Expected == DiscountGrantStatus.Approved
                    ? "لا يُسحَب إلا خصم معتمد — هذا الخصم ليس معتمداً بعد (BR-DIS-008)."
                    : "لا يُبتّ إلا في خصم مقترح — هذا الخصم خرج من مرحلة الاقتراح، وقد يكون قد بُتّ فيه بالفعل (BR-DIS-003).")
                : (e.Expected == DiscountGrantStatus.Approved
                    ? "Only an approved discount can be revoked — this one is not approved (BR-DIS-008)."
                    : "Only a proposed discount can be decided — this one has left the proposal stage, and may already have been decided (BR-DIS-003)."),

            RevocationDateInPastException e => arabic
                ? $"تاريخ سحب الخصم {Day(e.EffectiveDate)} مضى، والسحب لا يرجع بأثر رجعي على مطالبات صدرت — اختر اليوم أو تاريخاً لاحقاً (BR-DIS-008)."
                : $"The revocation date {Day(e.EffectiveDate)} has passed, and a revocation does not reach back over charges already raised — choose today or a later date (BR-DIS-008).",

            WaiverExceedsChargeRemainderException => arabic
                ? "قيمة الإعفاء تتجاوز المتبقي من المطالبة — لا يُعفى أكثر مما هو مستحق؛ أنقص القيمة إلى المتبقي أو أقل (BR-DIS-006)."
                : "The waiver is larger than what is left on the charge — nothing may be waived beyond what is owed; reduce it to the remaining balance or less (BR-DIS-006).",

            WaiverNotPendingException => arabic
                ? "هذا الإعفاء لم يعد معلّقاً، وقد بُتّ فيه بالفعل — افتحه لترى القرار (BR-DIS-006)."
                : "This waiver is no longer pending — it has already been decided; open it to see the decision (BR-DIS-006).",

            RenewalItemNotPendingException => arabic
                ? "هذا البند في قائمة التجديد لم يعد معلّقاً، وقد بُتّ فيه بالفعل (BR-DIS-007)."
                : "This renewal item is no longer pending — it has already been decided (BR-DIS-007).",

            // ---------------------------------------------------------------- M20 instalment plans

            InvalidTemplateSplitException e => arabic
                ? (e.Fault == TemplateSplitFault.SplitsDoNotSumTo100
                    ? "نسب الأقساط لا تبلغ 100% — الخطة تقسّم الرسوم كاملة، فراجع النسب حتى يكون مجموعها مئة (BR-INS-001)."
                    : "كل قسط يحتاج موعد استحقاق: تاريخاً محدداً أو عدد أيام من بداية العام — أكمل الناقص (BR-INS-001).")
                : (e.Fault == TemplateSplitFault.SplitsDoNotSumTo100
                    ? "The instalment percentages do not reach 100% — a plan splits the whole fee, so adjust them until they add up to one hundred (BR-INS-001)."
                    : "Every instalment needs a due date: either a fixed date or a number of days from the start of the year — fill in the ones that have neither (BR-INS-001)."),

            PlanTemplateNotDraftException => arabic
                ? "هذا القالب معتمد ولم يعد يُعدَّل — فقد تكون خطط أُنشئت على شكله، وتعديله يترك الأسر القديمة على شكل والجديدة على آخر تحت اسم واحد؛ أنشئ نسخة جديدة (BR-INS-001)."
                : "This template is approved and no longer editable — schedules may already have been built on its shape, and changing it would leave old families on one shape and new ones on another under a single name; make a new version instead (BR-INS-001).",

            RescheduleRemainderMismatchException e => arabic
                ? $"مجموع الجدولة المقترحة {Amount(e.Proposed)} والمتبقي غير المسدَّد {Amount(e.Remainder)} — الجدولة تغطي المتبقي بالضبط، فلا يبقى مبلغ بلا قسط ولا يُجدوَل ما سُدِّد (BR-INS-005)."
                : $"The proposed schedule totals {Amount(e.Proposed)} while the unpaid remainder is {Amount(e.Remainder)} — a reschedule covers the remainder exactly, so no amount is left unscheduled and nothing already paid is scheduled again (BR-INS-005).",

            RescheduleCaseNotPendingException => arabic
                ? "طلب إعادة الجدولة هذا لم يعد معلّقاً، وقد بُتّ فيه بالفعل (BR-INS-005)."
                : "This reschedule request is no longer pending — it has already been decided (BR-INS-005).",

            PlanTemplateUnchangedException => arabic
                ? "الخطة على هذا القالب أصلاً — اختر قالباً آخر، فإعادة التوليد على القالب نفسه تستبدل أقساطاً قائمة بلا فائدة (BR-INS-003)."
                : "The plan is already on this template — choose a different one; regenerating on the same shape supersedes live instalments for nothing (BR-INS-003).",

            PlanTemplateScopeMismatchException => arabic
                ? "هذا القالب مخصّص لفئة رسوم أخرى — وخطة الطالب تجدوِل فئتها هي؛ اختر قالباً لنفس الفئة أو قالباً عاماً لكل الفئات (BR-INS-002)."
                : "That template belongs to a different fee category, while this plan schedules its own category's charges — choose a template for the same category, or one that covers all categories (BR-INS-002).",

            ScheduleFullyCollectedException => arabic
                ? "لا متبقٍّ غير مسدَّد في هذا الجدول — وتغيير القالب لا يعيد تأريخ إلا ما لم يُسدَّد بعد، والمسدَّد لا يُمَسّ (BR-INS-005)."
                : "Nothing on this schedule is still unpaid — changing the template only re-dates what is still owed, and what has been collected is never touched (BR-INS-005).",

            RescheduleNeedsPrincipalException e => arabic
                ? $"هذا القالب يدفع آخر قسط إلى {Day(e.ProposedLastDueDate)}، وهو امتداد يستدعي موافقة المدير — قدّمه من معالج إعادة الجدولة ليأخذ مساره في سلسلة الاعتماد (BR-INS-005)."
                : $"That template pushes the last instalment out to {Day(e.ProposedLastDueDate)}, an extension that needs the Principal's approval — raise it from the reschedule wizard so it goes through the chain (BR-INS-005).",

            PromiseDateOutOfRangeException e => arabic
                ? $"تاريخ الوعد بالسداد {Day(e.PromisedDate)} خارج المهلة المسموحة — الوعد يكون من اليوم إلى نهاية المهلة التي حددتها المدرسة (BR-INS-006)."
                : $"The promised payment date {Day(e.PromisedDate)} is outside the allowed window — a promise runs from today to the end of the horizon the school set (BR-INS-006).",

            InstallmentNotOverdueException => arabic
                ? "لا يُسجَّل وعد بالسداد إلا على قسط تأخّر — هذا القسط لم يحلّ أجله بعد أو سُدِّد (BR-INS-006)."
                : "A promise to pay is only recorded against an overdue instalment — this one is not yet due, or is already paid (BR-INS-006).",

            PdcNotCoverableException => arabic
                ? "هذا الشيك لا يغطي هذا القسط — إمّا أنه لدافع آخر، أو أنه لم يعد شيكاً سارياً (صُرِف أو ارتدّ أو أُلغي) (BR-INS-009)."
                : "This cheque cannot cover this instalment — it either belongs to a different payer, or is no longer live (cleared, bounced or cancelled) (BR-INS-009).",

            InstallmentNotOpenException => arabic
                ? "هذا القسط لم يعد مفتوحاً — فقد سُدِّد أو استُبدل بجدولة أخرى أو أُعدِم، والمسدَّد لا يُعدَّل (BR-INS-003)."
                : "This instalment is no longer open — it has been paid, superseded by a reschedule, or written off, and none of those are edited afterwards (BR-INS-003).",

            // The dates are read back to the officer rather than described, because the mistake is
            // almost always one typed digit and seeing the pair is what makes it visible.
            InvalidCollectionWindowException window => arabic
                ? $"فترة التحصيل تبدأ في {Day(window.From)} وتنتهي في {Day(window.To)} — أي أنها تنتهي قبل أن تبدأ؛ صحّح التاريخين، فالمدى المقلوب لا يطابق شيئاً ويبدو كأن لا أحد مدين."
                : $"The collection window starts {Day(window.From)} and ends {Day(window.To)} — it ends before it begins; correct the two dates, because a backwards range matches nothing and reads as though nobody owes anything.",

            // ---------------------------------------------------------------- M22 payments and refunds

            TillSessionNotOpenException => arabic
                ? "لا سند قبض بغير وردية صندوق مفتوحة — افتح وردية باسمك قبل التحصيل (BR-PAY-001)."
                : "No receipt is issued without an open cashier session — open one in your name before taking money (BR-PAY-001).",

            CashierAlreadyHasOpenTillException cashierTill => arabic
                ? $"لديك وردية مفتوحة على الصندوق {cashierTill.TillCode} — أغلقها بالعدّ قبل فتح غيرها، فالوردية أمين واحد وصندوق واحد ويوم واحد (BR-PAY-001)."
                : $"You already have a session open on till {cashierTill.TillCode} — close it with a count before opening another; a session is one cashier, one till, one day (BR-PAY-001).",

            TillAlreadyOpenException openTill => arabic
                ? $"الصندوق {openTill.TillCode} بيد أمين آخر الآن — لا يعمل اثنان على درج واحد وإلا تعذّر نسب العدّ عند الإغلاق (BR-PAY-001)."
                : $"Till {openTill.TillCode} is in another cashier's hands right now — two people on one drawer make the count at close unattributable (BR-PAY-001).",

            InvalidPdcStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة الشيك الحالية: المودَع يُصرَف أو يرتدّ، والمرتدّ يُستبدل أو يُلاحَق، ولا رجوع بعد الصرف (BR-PAY-004)."
                : "That move is not available from the cheque's current state: a lodged cheque clears or bounces, a bounced one is replaced or pursued, and nothing comes back after clearing (BR-PAY-004).",

            InvalidRefundVoucherStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة سند الاسترداد الحالي: المقترح يُعتمد أو يُرفض، والمعتمد يُصرَف، ولا طريق عائد بعد الصرف (BR-PAY-005)."
                : "That move is not available from the refund voucher's current state: a proposed voucher is approved or rejected, an approved one is paid out, and there is no way back after payout (BR-PAY-005).",

            RefundExceedsPositionException => arabic
                ? "قيمة الاسترداد تتجاوز الرصيد الدائن لدى الدافع — لا يُردّ إلا ما دُفع زائداً عن المستحق؛ راجع كشف حساب الأسرة (BR-PAY-005)."
                : "The refund is larger than the payer's credit balance — only money paid in excess of what is owed can be refunded; check the family's statement (BR-PAY-005).",

            // ---------------------------------------------------------------- where the money went (BR-PAY-002)

            CollectionAccountRequiredException required => arabic
                ? $"اختر {CollectionAccountLabels.KindDefinite(required.Kind, true)} الذي وصل إليه المبلغ — لا يُصدر سند لا يذكر أين ذهب المال (BR-PAY-002)."
                : $"Choose {CollectionAccountLabels.KindDefinite(required.Kind, false)} the money arrived in — a receipt that does not say where the money went is not issued (BR-PAY-002).",

            CollectionAccountMethodMismatchException mismatch => arabic
                ? $"هذه الطريقة تُحصَّل في {CollectionAccountLabels.Kind(mismatch.Required, true)}، والحساب المختار {CollectionAccountLabels.Kind(mismatch.Actual, true)} — لا يُقيَّد المال في وعاء لم يصل إليه (BR-PAY-002)."
                : $"This method is collected into a {CollectionAccountLabels.Kind(mismatch.Required, false).ToLowerInvariant()}, and the account chosen is a {CollectionAccountLabels.Kind(mismatch.Actual, false).ToLowerInvariant()} — money is not recorded into a pot it never reached (BR-PAY-002).",

            CollectionAccountInactiveException inactive => arabic
                ? $"الحساب {inactive.Code} مُلغى التفعيل — تبقى سنداته السابقة عليه، ولا يُحصَّل فيه جديد. اختر حساباً آخر."
                : $"Account {inactive.Code} has been retired — its earlier receipts stay on it, but nothing new is collected into it. Choose another account.",

            CollectionAccountNotFoundException => arabic
                ? "الحساب المطلوب ليس من حسابات هذه المدرسة — أعد تحميل الصفحة واختر من القائمة."
                : "That account is not one of this school's — reload the page and choose from the list.",

            DuplicateCollectionAccountCodeException duplicate => arabic
                ? $"الرمز {duplicate.Code} مستخدم لحساب آخر — الرمز هو ما يُشار به إلى الحساب، فلا يتكرر."
                : $"Code {duplicate.Code} already belongs to another account — the code is how an account is referred to, so it is not repeated.",

            BankCollectionAccountNeedsNumberException => arabic
                ? "الحساب البنكي يحتاج رقم حساب أو آيبان — بغيرهما لا يمكن إخبار ولي الأمر إلى أين يحوّل."
                : "A bank account needs an account number or an IBAN — without one there is no way to tell a parent where to send the money.",

            // ---------------------------------------------------------------- E-503 ledger export

            GlMappingMissingException e => arabic
                ? $"لا يوجد حساب محاسبي مرتبط بـ: {string.Join("، ", e.MissingKeys)} — أكمل جدول الربط المحاسبي قبل توليد الدفعة."
                : $"No ledger account is mapped for: {string.Join(", ", e.MissingKeys)} — complete the GL mapping table before generating the batch.",

            GlPeriodOverlapException e => arabic
                ? $"تتداخل هذه الفترة مع دفعة الترحيل {e.ExistingBatchNo} — المستند لا يصل إلى دفتر الأستاذ مرتين؛ ألغِ تلك الدفعة أولاً أو ضيّق الفترة."
                : $"This period overlaps export batch {e.ExistingBatchNo} — a document never reaches the ledger twice; void that batch first, or narrow the period.",

            GlBatchNotGeneratedException => arabic
                ? "لا يُلغى إلا ما كان في حالة «مولَّدة» — هذه الدفعة رُحِّلت أو أُلغيت من قبل."
                : "Only a generated batch can be voided — this one has already been posted or voided.",

            GlPostingRejectedException e => arabic
                ? $"رفض دفتر الأستاذ الدفعة {e.BatchNo} — الرمز [{e.ErrorCode}]. الغالب أن السبب إعداد لا خلل: فترة محاسبية مقفلة، أو حساب لا يقبل القيود، أو سنة مالية لم تُفتح بعد؛ راجعها في النظام المحاسبي ثم أعد المحاولة."
                : $"The ledger refused batch {e.BatchNo} — code [{e.ErrorCode}]. This is nearly always configuration rather than a fault: a closed accounting period, an account that is not postable, or a fiscal year that does not exist yet; fix it in the accounting system and try again.",

            // ---------------------------------------------------------------- M24 cafeteria

            BannedItemOnMenuException => arabic
                ? "هذا الصنف ممنوع من البيع للطلاب بحسب اللائحة الغذائية، فلا يُدرَج في قائمة الطعام (BR-CAF-008)."
                : "This item is banned from sale to students under the nutrition policy and cannot go on the menu (BR-CAF-008).",

            SaleBlockedException e => SaleBlocked(e, arabic),

            AllergyWarningUnconfirmedException e => arabic
                ? $"تنبيه حساسية ({e.Matches}) — لا تُتمّ العملية قبل تأكيدك أنك رأيت التنبيه (BR-CAF-002)."
                : $"Allergy warning ({e.Matches}) — the sale does not go through until you confirm you have seen it (BR-CAF-002).",

            SaleNotVoidableException => arabic
                ? "لا يمكن إلغاء هذه العملية — إمّا أنها أُلغيت من قبل، أو أن وردية الصندوق التي تمّت فيها قد أُقفلت؛ ما بعد الإقفال يُصحَّح باسترداد لا بإلغاء (BR-CAF-009)."
                : "This sale cannot be voided — it has either been voided already, or the cashier session it belongs to is closed; after close-out a sale is corrected with a refund, not a void (BR-CAF-009).",

            WalletAdjustmentReasonRequiredException => arabic
                ? "تعديل رصيد المحفظة يدوياً يتطلب كتابة سبب موثّق — اكتبه ثم احفظ (BR-CAF-009)."
                : "Adjusting a wallet balance by hand requires a documented reason — write one and save again (BR-CAF-009).",

            WalletBalanceNotRefundableException => arabic
                ? "لا رصيد موجباً في هذه المحفظة يُردّ (BR-CAF-001)."
                : "This wallet has no positive balance to refund (BR-CAF-001).",

            // ---------------------------------------------------------------- M25 school store

            StorePriceMissingException => arabic
                ? "لا سعر سارياً لهذا الصنف في تاريخ البيع — انشر قائمة الأسعار أولاً (BR-STO-001)."
                : "This item has no price in force on the sale date — publish the price list first (BR-STO-001).",

            StoreStockInsufficientException => arabic
                ? "الكمية المطلوبة من هذا المقاس أكثر من المتاح في المخزون (BR-STO-006)."
                : "There is less of this size in stock than the sale asks for (BR-STO-006).",

            AccountChargeNotAllowedException e => arabic
                ? (e.Refusal == AccountChargeRefusal.CategoryDisabled
                    ? $"لا تُقيَّد أصناف «{StoreCategoryName(e.Category, true)}» على حساب الأسرة — حصّل ثمنها الآن (BR-STO-003)."
                    : $"المبلغ يتجاوز سقف القيد على الحساب لأصناف «{StoreCategoryName(e.Category, true)}» — يلزم تجاوز من الإدارة المالية مع كتابة سببه (BR-STO-003).")
                : (e.Refusal == AccountChargeRefusal.CategoryDisabled
                    ? $"{StoreCategoryName(e.Category, false)} items cannot go on the family account — take payment now (BR-STO-003)."
                    : $"The total is over the account-charge cap for {StoreCategoryName(e.Category, false)} items — Finance has to override it, and say why (BR-STO-003)."),

            StoreTenderRejectedException e => TenderRefused(e, arabic),

            ReturnNotAllowedException => arabic
                ? "لا يُقبل الإرجاع أو الاستبدال هنا — إمّا انقضت مهلة الإرجاع، أو حالة الصنف خارج ما تسمح به السياسة، أو الكمية أكثر مما بيع (BR-STO-005)."
                : "This return or exchange is not allowed — the return window has closed, the item's condition is outside policy, or the quantity is more than was sold (BR-STO-005).",

            HandoutBeforeChargeException => arabic
                ? "لم تُقيَّد قيمة هذه الحزمة بعد، والتسليم لا يسبق القيد بحسب إعداد المدرسة — قيّد الرسوم أولاً (BR-STO-004)."
                : "This bundle has not been charged yet, and the school's setting says handout follows the charge — raise the charge first (BR-STO-004).",

            StoreSaleNotVoidableException => arabic
                ? "لا تُلغى عملية المتجر إلا داخل الوردية نفسها التي تمّت فيها — بعد الإقفال يكون التصحيح إرجاعاً (BR-STO-008)."
                : "A store sale is only voided inside the same cashier session it was made in — after close-out the correction is a return (BR-STO-008).",

            _ => null,
        };

        /// <summary>
        /// The counter refusals, kept apart because there are ten of them and they read as one list:
        /// a cashier meets these more often than every other refusal in the product combined.
        /// </summary>
        private static string SaleBlocked(SaleBlockedException e, bool arabic) => e.Reason switch
        {
            SaleBlockReason.ItemNotSellableToStudents => arabic
                ? "هذا الصنف لا يُباع للطلاب (BR-CAF-008)."
                : "This item is not sold to students (BR-CAF-008).",

            SaleBlockReason.DailyLimitExceeded => arabic
                ? "بلغ الطالب حدّ الإنفاق اليومي المسموح له — لا مزيد اليوم (BR-CAF-003)."
                : "The student has reached their daily spending limit — nothing more today (BR-CAF-003).",

            SaleBlockReason.BlockedCategory => arabic
                ? "السلة تحوي صنفاً من فئة محظورة على هذا الطالب بقرار من وليّ أمره أو من المدرسة (BR-CAF-003)."
                : "The basket contains an item from a category blocked for this student by their guardian or the school (BR-CAF-003).",

            SaleBlockReason.AllergyHardBlock => arabic
                ? "السلة تحوي صنفاً يحتوي على مادة مسجَّلة في حساسية هذا الطالب، والمنع هنا قاطع لا يُتجاوز (BR-CAF-002)."
                : "The basket contains something the student is recorded as allergic to, and this block is absolute — it cannot be overridden (BR-CAF-002).",

            SaleBlockReason.InsufficientStock => arabic
                ? "الكمية المطلوبة أكثر من المتاح من هذا الصنف اليوم."
                : "There is less of this item left today than the basket asks for.",

            SaleBlockReason.NoActiveMealPlan => arabic
                ? "لا اشتراك وجبات سارياً لهذا الطالب — حصّل الثمن بوسيلة أخرى أو جدّد الاشتراك."
                : "This student has no active meal plan — take payment another way, or renew the plan.",

            SaleBlockReason.MealPlanEntitlementUsed => arabic
                ? "استُهلك استحقاق اشتراك الوجبات لليوم، أو أن السلة أكبر مما يغطيه ليوم واحد."
                : "Today's meal-plan entitlement is used up, or the basket is bigger than one day covers.",

            SaleBlockReason.NoWallet => arabic
                ? "لا محفظة لهذا الحساب — افتح محفظة واشحنها قبل الدفع بها."
                : "This holder has no wallet — open one and top it up before paying with it.",

            SaleBlockReason.InsufficientWalletBalance => arabic
                ? "رصيد المحفظة لا يكفي قيمة السلة — اشحنها أو حصّل الفرق نقداً."
                : "The wallet does not hold the price of the basket — top it up, or take the difference in cash.",

            SaleBlockReason.TillSessionNotOpen => arabic
                ? "لا بيع نقدي بغير وردية صندوق مفتوحة — افتح وردية باسمك أولاً (BR-CAF-007)."
                : "No cash sale without an open cashier session — open one in your name first (BR-CAF-007).",

            _ => arabic ? "لم تُقبل العملية (BR-CAF-002/003)." : "The sale was refused (BR-CAF-002/003).",
        };

        /// <summary>The store counter's tender refusals, the same list one shop along.</summary>
        private static string TenderRefused(StoreTenderRejectedException e, bool arabic) => e.Refusal switch
        {
            StoreTenderRefusal.TillSessionNotOpen => arabic
                ? "البيع نقداً أو بالبطاقة يحتاج وردية صندوق مفتوحة — افتح وردية باسمك أولاً (BR-PAY-001)."
                : "Cash and card sales need an open cashier session — open one in your name first (BR-PAY-001).",

            StoreTenderRefusal.WalletTenderUnavailable => arabic
                ? "الدفع بالمحفظة غير مفعَّل في المتجر، أو أن العملية لم تُنسَب إلى طالب تحمل محفظته الرصيد."
                : "Wallet payment is switched off in the store, or the sale names no student whose wallet would hold the money.",

            StoreTenderRefusal.NoWallet => arabic
                ? "لا محفظة لهذا الطالب — افتح محفظة واشحنها قبل الدفع بها."
                : "This student has no wallet — open one and top it up before paying with it.",

            StoreTenderRefusal.InsufficientWalletBalance => arabic
                ? "رصيد المحفظة لا يكفي قيمة السلة — اشحنها أو حصّل الفرق بوسيلة أخرى."
                : "The wallet does not hold the price of the basket — top it up, or take the difference another way.",

            StoreTenderRefusal.StudentRequired => arabic
                ? "الدفع بالقيد على الحساب يحتاج طالباً يُقيَّد عليه — اختر الطالب أولاً."
                : "A tender that becomes a charge needs a student to charge it to — pick the student first.",

            _ => arabic ? "لم تُقبل وسيلة الدفع (BR-STO-003)." : "The tender was refused (BR-STO-003).",
        };

        /// <summary>
        /// Store categories as a counter operator names them. Four values, and no store screen has
        /// been built yet to own a labels class — when one is, this moves there.
        /// </summary>
        private static string StoreCategoryName(StoreItemCategory category, bool arabic) => category switch
        {
            StoreItemCategory.Uniform => arabic ? "الزي المدرسي" : "uniform",
            StoreItemCategory.Book => arabic ? "الكتب" : "book",
            StoreItemCategory.Stationery => arabic ? "القرطاسية" : "stationery",
            _ => arabic ? "أخرى" : "other",
        };
    }
}
