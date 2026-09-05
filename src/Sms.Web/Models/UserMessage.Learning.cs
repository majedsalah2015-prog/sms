using System;
using System.Linq;
using Sms.Application.Common.Exceptions;
using Sms.Application.Learning;
using Sms.Domain.Learning;

namespace Sms.Web.Models
{
    /// <summary>
    /// Every refusal the academic modules can raise, in the reader's language.
    /// <para>
    /// Grading, examinations, attendance, the timetable, certificates, the school calendar, the
    /// year rollover, and module 37 e-learning. Most of these are met by a teacher rather than an administrator, and a teacher
    /// meeting an untranslated refusal has no help desk sitting beside them — the sentence has to
    /// carry the rule and the way out of it on its own.
    /// </para>
    /// <para>
    /// Where a count decides what to do next — how many marks are missing, how many periods are
    /// unplaced — it is carried in the message. Where an internal id is all the engine had, it is
    /// dropped: the teacher knows which marksheet they opened.
    /// </para>
    /// </summary>
    public static partial class UserMessage
    {
        private static string? Learning(Exception exception, bool arabic) => exception switch
        {
            // ---------------------------------------------------------------- M37 e-learning

            TeachingReachException => arabic
                ? "هذه المادة ليست من نصابك في الجدول المنشور — المعلّم ينشر لما يُدرّسه، ورئيس القسم لمواد قسمه، ولا يوجد طريق «كل الشعب» دون وكيل المدرسة. راجع إسنادك في الجدول المعتمد (BR-LRN-002)."
                : "This subject is not yours in the published timetable — a teacher publishes to what they teach and a head of department to their department's subjects, and there is no all-sections path below Vice-Principal. Check your allocation in the published timetable (BR-LRN-002).",

            LessonSessionMismatchException => arabic
                ? "الحصة المختارة لا تُدرّس هذا المقرر — اربط الدرس بحصة من حصص المقرر نفسه ليكون سجلّ ما جرى فيها، أو اتركه بلا ربط ليكون خطة أسبوعية (BR-LRN-001)."
                : "The chosen period does not teach this offering — bind the lesson to a period of the same offering so it records what happened in it, or leave it unbound as a weekly plan (BR-LRN-001).",

            HomeworkIssueRefusedException hi => hi.Reason switch
            {
                HomeworkIssueRefusal.GradedWithoutBlueprintComponent => arabic
                    ? "هذا الواجب عليه درجة ولم يُحدَّد المكوّن الذي تُضاف إليه في نموذج الدرجات — حدِّده قبل التكليف، فالدرجة التي لا مكان لها تُكتشف عند الرصد بعد أن يكون الصف قد أدّى العمل (BR-LRN-004)."
                    : "This homework carries marks but names no grading component to feed — name it before issuing. A mark with nowhere to land is discovered at release, after the class has already done the work (BR-LRN-004).",

                HomeworkIssueRefusal.UngradedWithBlueprintComponent => arabic
                    ? "هذا الواجب بلا درجة ومع ذلك يشير إلى مكوّن في نموذج الدرجات — إمّا أن تضع له درجة عظمى أو تزيل المكوّن، فالإشارة إليه تَعِد الوحدة 17 بدرجة لن تصل (BR-LRN-004)."
                    : "This homework is ungraded yet names a grading component — either give it a maximum mark or clear the component. Naming one promises Module 17 a mark that will never arrive (BR-LRN-004).",

                HomeworkIssueRefusal.DueDateOutsideAcademicYear => arabic
                    ? "تاريخ التسليم خارج حدود العام الدراسي — اختر تاريخاً داخل العام (BR-GLB-051)."
                    : "The due date falls outside the academic year — choose a date inside it (BR-GLB-051).",

                HomeworkIssueRefusal.DueDateNotAWorkingDay => arabic
                    ? "تاريخ التسليم ليس يوم دوام في تقويم المدرسة — عمل يُطلَب تسليمه في يوم عطلة هو عمل لا يجد أحداً يستلمه. اختر يوم دوام (BR-GLB-052)."
                    : "The due date is not a working day in the school calendar — work due on a holiday is work due on a day nobody is there to receive it. Choose a working day (BR-GLB-052).",

                _ => arabic
                    ? "لا يمكن تكليف الصف بهذا الواجب في وضعه الحالي (BR-LRN-004)."
                    : "This homework cannot be issued to the class as it stands (BR-LRN-004).",
            },

            HomeworkTransitionException ht => arabic
                ? (ht.From == HomeworkStatus.Released
                    ? "هذا الواجب رُصدت درجاته في الوحدة 17، ومن تلك اللحظة صارت الدرجة ملكها — أيّ تصحيح يجري هناك بضوابط تغيير الدرجات، لا بإرجاع الواجب هنا (BR-LRN-012)."
                    : ht.From == HomeworkStatus.Withdrawn
                        ? "هذا الواجب مسحوب، والمسحوب سجلّ يُقرأ لا مسوّدة تُعدَّل — كلِّف الصف بواجب جديد إن أردت إعادته (BR-LRN-016)."
                        : "لا تُتاح هذه الحركة من حالة الواجب الحالية. ولا يوجد «إلغاء تكليف»: ما رآه الصف يُسحب بسبب معلن، لا يختفي بصمت (BR-LRN-003/016).")
                : (ht.From == HomeworkStatus.Released
                    ? "This homework's marks are in Module 17, and from that moment the mark is theirs — a correction happens there under mark-change control, not by rewinding the homework here (BR-LRN-012)."
                    : ht.From == HomeworkStatus.Withdrawn
                        ? "This homework is withdrawn, and withdrawn work is readable history rather than an editable draft — set new work if you want it back (BR-LRN-016)."
                        : "The homework's current state does not offer this move. There is no un-issue: what the class has seen is withdrawn with a stated reason, never made to vanish quietly (BR-LRN-003/016)."),

            HomeworkWithdrawalBlockedException hw => arabic
                ? $"مرّ موعد التسليم وسلّم {hw.SubmissionCount} من الطلاب بالفعل — لا يمكن سحب عمل أُنجز وكأنه لم يُطلب قط. عالج الأمر بالتصحيح أو بملاحظة للصف (الوثيقة 37 §9)."
                : $"The due date has passed and {hw.SubmissionCount} student(s) have already submitted — work that has been done cannot be made to have never been asked for. Handle it in marking, or with a note to the class (doc/Modules/37 §9).",

            LessonTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة الدرس الحالية: المسوّدة تُنشر أو تُسحب، والمنشور يُسحب ولا يعود مسوّدة — فالدرس الذي قرأه الطالب أمس لا يختفي اليوم بلا سبب مسجَّل (BR-LRN-003/016)."
                : "That move is not available from the lesson's current state: a draft is published or retired, and a published lesson is retired rather than returned to draft — content a student read yesterday does not vanish today with no reason recorded (BR-LRN-003/016).",

            LessonRetiredException => arabic
                ? "هذا الدرس مسحوب، والمسحوب سجلّ يُقرأ لا مسوّدة تُحرَّر — أنشئ درساً جديداً بدلاً من إحيائه (BR-LRN-016)."
                : "This lesson is retired, and a retired lesson is history to read rather than a draft to edit — create a new lesson instead of reviving it (BR-LRN-016).",

            ResourceNotScanCleanException => arabic
                ? "لم يكتمل فحص هذا الملف أو تبيّن أنه مصاب، ولا يُعرض ملف غير مفحوص على طالب ولا على معلّم — انتظر انتهاء الفحص أو ارفع نسخة سليمة (BR-LRN-006)."
                : "This file is not virus-scan clean, and an unscanned file is shown to no one, staff or student — wait for the scan to finish or upload a clean copy (BR-LRN-006).",

            // ---- M37 the homework loop: hand-in, marking, release (§8.4/§8.5/§8.10)

            PortalSubmissionIdentityException => arabic
                ? "التسليم يكون من حساب الطالب نفسه — حساب وليّ الأمر يرى ما كُلِّف به ابنه ودرجته، ولا يسلّم نيابةً عنه، لأنّ العمل المُسلَّم لا بدّ أن يكون عمل من تُنسب إليه الدرجة (BR-LRN-013)."
                : "Work is handed in from the student's own account — a parent's account sees what was set and what it scored, but never submits on the student's behalf: submitted work has to be the work of whoever the mark is credited to (BR-LRN-013).",

            HomeworkNotOfferedToStudentException => arabic
                ? "هذا الواجب ليس من واجباتك — إمّا أنّه مُكلَّف لشعبة أخرى أو أنّه لم يُنشر بعد. صفحة «أعمالي» تحمل ما كُلِّفت به أنت وحدك (BR-LRN-003/013)."
                : "This homework is not yours — it is either set to another section or not yet issued. The \"my work\" page holds what was set to you and nothing else (BR-LRN-003/013).",

            HomeworkClosedToSubmissionsException hc => arabic
                ? (hc.Status == HomeworkStatus.Withdrawn
                    ? "سُحب هذا الواجب وأُبلغ بسبب سحبه كلّ من كان قد سلّم — ولا تسليم على عمل مسحوب (BR-LRN-016)."
                    : "أُغلق هذا الواجب ودخل التصحيح، فلم يعد يقبل تسليماً. والتأخير وحده لا يُغلق واجباً أبداً — العمل المتأخر مقبول ما دام الواجب مفتوحاً؛ راجع معلّم المادة (BR-LRN-005).")
                : (hc.Status == HomeworkStatus.Withdrawn
                    ? "This homework was withdrawn and everyone who had submitted was told why — withdrawn work takes no further hand-ins (BR-LRN-016)."
                    : "This homework has closed and moved into marking, so it no longer takes hand-ins. Lateness alone never closes a homework — late work is accepted for as long as the homework is open; speak to your subject teacher (BR-LRN-005)."),

            SubmissionMarkingClosedException => arabic
                ? "رُصدت درجات هذا الواجب في الوحدة 17، ومن تلك اللحظة صارت الدرجة ملكها — أيّ تصحيح يجري هناك بضوابط تغيير الدرجات، لا بإعادة التصحيح هنا (BR-LRN-012)."
                : "This homework's marks are in Module 17, and from that moment the mark is theirs — a correction happens there under mark-change control, not by re-marking here (BR-LRN-012).",

            SubmissionScoreOutOfRangeException sr => arabic
                ? (sr.MaxMarks is null
                    ? "هذا الواجب تدريب بلا درجة، فهو لا يصل إلى سجل الدرجات أصلاً — اكتب ملاحظتك للطالب دون درجة (BR-LRN-004)."
                    : $"الدرجة {Amount(sr.Score)} خارج مدى هذا الواجب — المدى من صفر إلى {Amount(sr.MaxMarks.Value)} (BR-LRN-004).")
                : (sr.MaxMarks is null
                    ? "This homework is ungraded practice and never reaches the gradebook — leave feedback for the student without a mark (BR-LRN-004)."
                    : $"The mark {Amount(sr.Score)} is outside this homework's range — it runs from 0 to {Amount(sr.MaxMarks.Value)} (BR-LRN-004)."),

            HomeworkReleaseRefusedException hr => hr.Reason switch
            {
                HomeworkReleaseRefusal.NotBeingMarked => arabic
                    ? "الرصد يكون من حالة «قيد التصحيح» وحدها — أغلق الواجب وابدأ تصحيحه أولاً (الوثيقة 37 §4)."
                    : "Release is the step out of marking and only out of marking — close the homework and start marking it first (doc/Modules/37 §4).",

                HomeworkReleaseRefusal.UngradedPractice => arabic
                    ? "هذا الواجب تدريب بلا درجة عظمى، فليس فيه ما يُرصد — التدريب لا يصل إلى الوحدة 17 بالتصميم (BR-LRN-004)."
                    : "This homework is ungraded practice with no maximum mark, so there is nothing to release — practice never reaches Module 17, by design (BR-LRN-004).",

                HomeworkReleaseRefusal.NoBlueprintComponent => arabic
                    ? "هذا الواجب عليه درجة ولا يشير إلى مكوّن في نموذج الدرجات، فلا مكان تهبط فيه درجاته — حدِّد المكوّن ثم ارصد (BR-LRN-004/012)."
                    : "This homework carries marks but names no grading component, so its marks have nowhere to land — name the component, then release (BR-LRN-004/012).",

                HomeworkReleaseRefusal.SubmissionsUnscored => arabic
                    ? $"ما زال {hr.UnscoredSubmissionCount} من التسليمات بلا درجة — الرصد يسلّم درجات الصفّ كلّه دفعةً واحدة، ولا يُرصد نصف صفّ (BR-LRN-011)."
                    : $"{hr.UnscoredSubmissionCount} hand-in(s) still carry no score — release hands the whole class's marks over at once, and half a class is never released (BR-LRN-011).",

                _ => arabic
                    ? "لا يمكن رصد درجات هذا الواجب في وضعه الحالي (BR-LRN-011/012)."
                    : "This homework's marks cannot be released as it stands (BR-LRN-011/012).",
            },

            HomeworkMarksheetUnresolvedException hm => arabic
                ? (hm.EnrollmentId is null
                    ? "لا يوجد في الوحدة 17 كشف درجات لهذه الشعبة يحمل المكوّن الذي يُغذّيه هذا الواجب — أنشئ الكشف هناك أوّلاً. هذه الوحدة تسلّم درجة خاماً إلى مخزن الدرجات الوحيد ولا تنشئ لنفسها مخزناً ثانياً (BR-LRN-012)."
                    : "كشف درجات الوحدة 17 لا يشمل أحد الطلاب الذين سلّموا — حدِّث تسجيل الشعبة في الكشف هناك ثمّ أعد الرصد. ورصد نصف صفّ أسوأ من تأجيل الرصد (BR-LRN-012).")
                : (hm.EnrollmentId is null
                    ? "Module 17 holds no marksheet for this section carrying the component this homework feeds — create it there first. This module hands a raw mark to the one marks store and never builds itself a second one (BR-LRN-012)."
                    : "Module 17's marksheet does not cover one of the students who submitted — refresh the section's enrolment on that sheet, then release again. Releasing half a class is worse than releasing late (BR-LRN-012)."),

            HomeworkReleaseMarksheetPublishedException => arabic
                ? "كشف درجات هذه الشعبة منشور بالفعل، والكتابة فيه من هنا تلتفّ على ضبط تغيير الدرجات — أعِد الكشف إلى التحرير في الوحدة 17 بسبب مسجَّل، ثمّ ارصد (BR-LRN-012, BR-GRA-005)."
                : "This section's marksheet is already published, and writing into it from here would slip past mark-change control — return the sheet to draft in Module 17 with a recorded reason, then release (BR-LRN-012, BR-GRA-005).",

            // ---- M37 the question bank (§8.6)

            QuestionShapeException qs => qs.Refusal switch
            {
                QuestionShapeRefusal.TooFewOptions => arabic
                    ? "سؤال الاختيار يحتاج خيارين على الأقل — الخيار الواحد تعليمات لا سؤال (BR-LRN-011)."
                    : "A choice question needs at least two options — one option is an instruction, not a question (BR-LRN-011).",

                QuestionShapeRefusal.NoCorrectOption => arabic
                    ? "لم تُحدَّد إجابة صحيحة، ولا يمكن تصحيح سؤال آلياً بلا إجابة يُقاس عليها (BR-LRN-011)."
                    : "No option is marked correct, and a question with nothing to measure against cannot be marked automatically (BR-LRN-011).",

                QuestionShapeRefusal.TooManyCorrectOptions => arabic
                    ? "هذا النوع يقبل إجابة صحيحة واحدة — إمّا أن تُبقي واحدة، أو تحوّله إلى «اختيار متعدّد» (BR-LRN-011)."
                    : "This type takes exactly one correct answer — leave one correct, or change the type to multiple choice (BR-LRN-011).",

                QuestionShapeRefusal.EveryOptionCorrect => arabic
                    ? "كلّ الخيارات صحيحة، وهذا ليس سؤالاً — يُصحَّح للصف كلّه مهما اختار، ويظهر في التحليلات سؤالاً سليماً وهو ليس كذلك (BR-LRN-011)."
                    : "Every option is correct, which is not a question — it marks the whole class right whatever they pick, and hides in the analytics as a sound item (BR-LRN-011).",

                QuestionShapeRefusal.OptionsOnANonChoiceType => arabic
                    ? "هذا النوع لا يحمل خيارات — يبدو أنّ النوع اختير خطأً (BR-LRN-011)."
                    : "This type carries no options — the type looks like it was chosen by mistake (BR-LRN-011).",

                QuestionShapeRefusal.NoAcceptedAnswer => arabic
                    ? "اذكر إجابةً مقبولة واحدة على الأقل، فبها يُصحَّح هذا السؤال آلياً (BR-LRN-011)."
                    : "List at least one accepted answer — it is what marks this question automatically (BR-LRN-011).",

                QuestionShapeRefusal.NonNumericAcceptedAnswer => arabic
                    ? "السؤال العددي يقبل أرقاماً فقط في إجاباته المقبولة — اكتب الرقم، أو حوّل السؤال إلى إجابة نصية قصيرة (BR-LRN-011)."
                    : "A numeric question accepts only numbers as accepted answers — enter the number, or change the question to short text (BR-LRN-011).",

                QuestionShapeRefusal.AcceptedAnswersOnAChoiceType => arabic
                    ? "سؤال الاختيار يُصحَّح بخياراته لا بإجابات مكتوبة — احذف الإجابات المقبولة (BR-LRN-011)."
                    : "A choice question is marked by its options, not by typed answers — clear the accepted answers (BR-LRN-011).",

                QuestionShapeRefusal.ToleranceOnANonNumericType => arabic
                    ? "هامش الخطأ للأسئلة العددية وحدها — فلا معنى لـ«±٠٫٥» في إجابة نصية (BR-LRN-011)."
                    : "A tolerance belongs to numeric questions only — \"±0.5\" means nothing on a text answer (BR-LRN-011).",

                QuestionShapeRefusal.NegativeTolerance => arabic
                    ? "هامش الخطأ لا يكون سالباً — فهامش سالب لا يقبل أيّ إجابة إطلاقاً. اجعله صفراً للمطابقة التامّة (BR-LRN-011)."
                    : "A tolerance cannot be negative — a negative one accepts no answer at all. Use zero for an exact match (BR-LRN-011).",

                QuestionShapeRefusal.MarksNotPositive => arabic
                    ? "درجة السؤال يجب أن تكون أكبر من صفر (BR-LRN-011)."
                    : "A question's marks must be greater than zero (BR-LRN-011).",

                _ => arabic
                    ? "لا يمكن طرح هذا السؤال بشكله الحالي (BR-LRN-011)."
                    : "This question cannot be asked as it stands (BR-LRN-011).",
            },

            QuestionDeprecatedException => arabic
                ? "هذا السؤال مسحوب من البنك، والمسحوب سجلّ يبقى على كلّ ورقة استُعمل فيها ولا يُحرَّر — أضف سؤالاً جديداً بدلاً من إحيائه (BR-LRN-007)."
                : "This question is withdrawn from the bank, and a withdrawn question stays on every paper that used it rather than being edited — add a new question instead of reviving it (BR-LRN-007).",

            QuestionNotCurrentVersionException => arabic
                ? "هذه نسخة سابقة من السؤال، وهي سجلّ ما أجاب عنه الطلاب فعلاً — التعديل يبدأ من النسخة الحالية (BR-LRN-007)."
                : "This is an earlier version of the question and is the record of what students actually answered — edits start from the current version (BR-LRN-007).",

            QuestionBankRetiredException => arabic
                ? "هذا البنك متقاعد فلا يقبل أسئلة جديدة، ويحتفظ بكلّ ما فيه لأنّ أسئلته قد تكون على ورقة أُجيبت فعلاً (BR-GLB-006)."
                : "This bank is retired and takes no new questions, and it keeps every one it holds because they may sit on a paper already answered (BR-GLB-006).",

            // ---- M37 the paper builder (§8.7)

            PaperRefusedException pr => pr.Refusal switch
            {
                // BR-LRN-008 requires the refusal to NAME BOTH TOTALS. "Does not
                // reconcile" is not a sentence an author can act on; "you are
                // three marks over twenty" is.
                PaperRefusal.MarksDoNotReconcile => arabic
                    ? $"مجموع درجات الورقة {Amount(pr.PaperTotalMarks)} ومكوّن الدرجات ينتظر {Amount(pr.ComponentMaxScore)} — {(pr.PaperTotalMarks > pr.ComponentMaxScore ? $"زيادة {Amount(pr.PaperTotalMarks - pr.ComponentMaxScore)}" : $"نقص {Amount(pr.ComponentMaxScore - pr.PaperTotalMarks)}")}. عدّل درجات الأسئلة أو أضف سؤالاً أو احذف واحداً حتى يتطابق الرقمان (BR-LRN-008)."
                    : $"The paper adds up to {Amount(pr.PaperTotalMarks)} and the grading component expects {Amount(pr.ComponentMaxScore)} — {(pr.PaperTotalMarks > pr.ComponentMaxScore ? $"{Amount(pr.PaperTotalMarks - pr.ComponentMaxScore)} over" : $"{Amount(pr.ComponentMaxScore - pr.PaperTotalMarks)} short")}. Adjust a question's marks, or add or remove one, until the two numbers meet (BR-LRN-008).",

                PaperRefusal.NoItems => arabic
                    ? "الورقة فارغة — أضف أسئلة إليها قبل إرسالها للاعتماد (BR-LRN-008)."
                    : "The paper is empty — put questions on it before sending it for approval (BR-LRN-008).",

                PaperRefusal.ContainsWithdrawnQuestion => arabic
                    ? $"على الورقة {pr.WithdrawnQuestionCount} من الأسئلة سُحبت من البنك بعد إضافتها — احذفها أو استبدلها. فالسحب يمنع السؤال من أوراق قادمة، وهذه الورقة منها ما دامت لم تُعتمد (BR-LRN-007)."
                    : $"{pr.WithdrawnQuestionCount} question(s) on this paper were withdrawn from the bank after they were added — remove or replace them. Withdrawal keeps a question out of future papers, and an unapproved paper is one of them (BR-LRN-007).",

                PaperRefusal.WrongStatus => arabic
                    ? "لا تُتاح هذه الحركة من حالة الورقة الحالية (الوثيقة 37 §4)."
                    : "That move is not available from the paper's current state (doc/Modules/37 §4).",

                _ => arabic
                    ? "لا يمكن نقل هذه الورقة في وضعها الحالي (BR-LRN-008)."
                    : "This paper cannot be moved as it stands (BR-LRN-008).",
            },

            OnlinePaperTransitionException opt => arabic
                ? (opt.From == OnlinePaperStatus.Approved
                    ? "هذه الورقة معتمدة، وقائمة أسئلتها هي ما وقّع عليه رئيس القسم — تعديلها بعد الاعتماد يجعل التوقيع على مستند آخر. اسحبها وابنِ غيرها إن لزم (الوثيقة 37 §4)."
                    : opt.From == OnlinePaperStatus.Withdrawn
                        ? "هذه الورقة مسحوبة، والمسحوب سجلّ يُقرأ لا مسوّدة تُحرَّر — أنشئ ورقة جديدة بدلاً من إحيائها (BR-LRN-016)."
                        : "لا تُتاح هذه الحركة من حالة الورقة الحالية (الوثيقة 37 §4).")
                : (opt.From == OnlinePaperStatus.Approved
                    ? "This paper is approved, and its question list is what the head of department signed — editing it afterwards would leave that signature on a different document. Withdraw it and build another if it must change (doc/Modules/37 §4)."
                    : opt.From == OnlinePaperStatus.Withdrawn
                        ? "This paper is withdrawn, and withdrawn work is readable history rather than an editable draft — create a new paper instead of reviving it (BR-LRN-016)."
                        : "That move is not available from the paper's current state (doc/Modules/37 §4)."),

            PaperNotEditableException pne => arabic
                ? (pne.Status == OnlinePaperStatus.PendingApproval
                    ? "الورقة عند رئيس القسم للاعتماد، فهي مستند قيد المراجعة لا مسوّدة — استردّها إن أردت تعديلها (الوثيقة 37 §4)."
                    : "لم تعد أسئلة هذه الورقة قابلة للتغيير في حالتها الحالية (الوثيقة 37 §4).")
                : (pne.Status == OnlinePaperStatus.PendingApproval
                    ? "The paper is with the head of department, so it is a document under review rather than a draft — take it back if you need to edit it (doc/Modules/37 §4)."
                    : "This paper's questions can no longer be changed in its current state (doc/Modules/37 §4)."),

            QuestionNotInBankException => arabic
                ? "هذا السؤال من بنك آخر — والورقة تسحب من بنك واحد، وهو ما يُبقي كلّ أسئلتها داخل مقرر واحد (BR-LRN-001)."
                : "That question belongs to another bank — a paper draws on one bank, which is what keeps every question on it inside one subject (BR-LRN-001).",

            // ---------------------------------------------------------------- M16 grading

            GradingScaleLockedException => arabic
                ? "سلّم التقدير مقفل ولم يعد يُعدَّل — فقد صدرت عليه نتائج منشورة، وتغيير حدوده الآن يغيّر تقديرات أُعلنت للأسر؛ أنشئ سلّماً جديداً للعام القادم (BR-GRA-001)."
                : "The grading scale is locked and can no longer be edited — published results already rest on it, and moving its bands now would change grades families have already been told; make a new scale for next year instead (BR-GRA-001).",

            BlueprintWeightMismatchException e => arabic
                ? $"مجموع أوزان مكوّنات التقييم {Amount(e.ActualSum)} وليس 100 — عدّل الأوزان حتى تبلغ مئة قبل الاعتماد (BR-GRA-003)."
                : $"The assessment component weights add up to {Amount(e.ActualSum)}, not 100 — adjust them until they reach one hundred before finalising (BR-GRA-003).",

            BlueprintNotFinalizedException => arabic
                ? "لم يُعتمد توزيع الدرجات بعد، ولا تُفتح كشوف الرصد على توزيع ما زال يُعدَّل — اعتمده أولاً (BR-GRA-003)."
                : "The mark design has not been finalised, and marksheets are not opened against a design that is still being changed — finalise it first (BR-GRA-003).",

            BlueprintLockedException => arabic
                ? "توزيع الدرجات معتمد ولم تعد مكوّناته تُعدَّل — فقد فُتحت عليه كشوف رصد؛ أنشئ توزيعاً جديداً بدلاً منه (BR-GRA-003)."
                : "The mark design is finalised and its components no longer change — marksheets have been opened against it; create a new design instead (BR-GRA-003).",

            InvalidMarksheetStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة كشف الرصد الحالية: المسودة تُرفَع، والمرفوعة تُعتمد أو تُعاد، والمعتمدة تُنشَر (BR-GRA-005)."
                : "That move is not available from the marksheet's current state: a draft is submitted, a submitted one is approved or returned, and an approved one is published (BR-GRA-005).",

            UnresolvedMarkEntriesException e => arabic
                ? $"بقي {Count(e.UnresolvedCount)} طالباً بلا درجة ولا غياب ولا إعفاء — الكشف يُرفَع مكتملاً حتى لا يُحسَب أحد صفراً بغير قصد (BR-GRA §9)."
                : $"{Count(e.UnresolvedCount)} student(s) still have neither a mark, an absence, nor an exemption — a marksheet goes up complete, so that nobody is scored zero by omission (BR-GRA §9).",

            GradingScaleInUseException e => arabic
                ? $"يشير إلى هذا السلّم {Count(e.BlueprintCount)} توزيع درجات، فلا يُحذف — انقلها إلى سلّم آخر أولاً (BR-GRA-001)."
                : $"{Count(e.BlueprintCount)} mark design(s) point at this scale, so it cannot be deleted — move them to another scale first (BR-GRA-001).",

            BlueprintInUseException e => arabic
                ? $"فُتح على هذا التوزيع {Count(e.MarksheetCount)} كشف رصد، فلا يُحذف — الدرجات المرصودة سجل لا يُنقض (BR-GRA-003)."
                : $"{Count(e.MarksheetCount)} marksheet(s) were opened on this design, so it cannot be deleted — the marks in them are a record and stay (BR-GRA-003).",

            MarksheetInUseException e => arabic
                ? (e.Blocker == MarksheetDeleteBlocker.MarksEntered
                    ? "رُصدت درجات في هذا الكشف، والدرجة مُدقَّقة من لحظة كتابتها — لا يُحذف الكشف بعدها (BR-GRA-011)."
                    : "لم يعد الكشف مسودة، ولا يُحذف إلا كشف مسودة لم يُمسّ (BR-GRA-011).")
                : (e.Blocker == MarksheetDeleteBlocker.MarksEntered
                    ? "Marks have been entered on this marksheet, and a mark is audited from the moment it is typed — the sheet is not deleted afterwards (BR-GRA-011)."
                    : "This marksheet has left draft, and only an untouched draft is deleted (BR-GRA-011)."),

            // ---------------------------------------------------------------- M17 examinations

            ExamBlueprintMismatchException => arabic
                ? "مكوّن الدرجات المختار لا يخص هذه المادة أو هذه الفترة — اختر مكوّناً من توزيع درجات المادة نفسها في فترة جولة الاختبار (BR-EXM-002)."
                : "The chosen mark component does not belong to this subject or this term — pick one from the subject's own design for the exam round's term (BR-EXM-002).",

            ExamScheduleClashException e => arabic
                ? $"بلغ هذا الصف حدّه من الاختبارات في يوم {Day(e.Date)} — وزّع الاختبار على يوم آخر؛ الحدّ موضوع حتى لا يُمتحن الطلاب أكثر مما يحتملون في اليوم (BR-EXM-003)."
                : $"This grade already has as many exams as it may hold on {Day(e.Date)} — move this one to another day; the cap exists so students are not examined more in a day than they can bear (BR-EXM-003).",

            InvalidExamRoundStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة جولة الاختبارات الحالية — افتحها لترى ما وصلت إليه (BR-EXM §4)."
                : "That move is not available from the exam round's current state — open it to see where it stands (BR-EXM §4).",

            SittingFullException => arabic
                ? "بلغت القاعة سعتها للاختبار — سعة الاختبار أقل من السعة العادية لتباعد الطلاب؛ افتح جلسة أخرى أو اختر قاعة أوسع (BR-EXM-004)."
                : "The room is at its exam capacity — exam seating spreads students out, so it holds fewer than usual; open another sitting or choose a larger room (BR-EXM-004).",

            StudentNotSeatedException => arabic
                ? "هذا الطالب غير مُقعَد في هذه الجلسة — أقعِده أولاً ثم سجّل حضوره أو درجته."
                : "This student is not seated in this sitting — seat them first, then record their attendance or mark.",

            // ---------------------------------------------------------------- M14 attendance

            DuplicateAttendanceRecordException e => arabic
                ? $"رُصد حضور هذا الطالب في {Day(e.Date)} من قبل — السجل واحد لكل يوم؛ افتح اليوم وعدّله بدل رصده مرة أخرى (BR-ATD-003)."
                : $"This student's attendance for {Day(e.Date)} is already captured — there is one record per day; open that day and change it rather than capturing it again (BR-ATD-003).",

            NoSectionMembershipOnDateException e => arabic
                ? $"لم يكن هذا الطالب في أي شعبة يوم {Day(e.Date)}، ولا يُرصد الحضور لمن لا شعبة له — راجع تاريخ التحاقه بالشعبة (BR-ATD-003)."
                : $"This student belonged to no section on {Day(e.Date)}, and attendance is not captured for a student with no section — check when they joined it (BR-ATD-003).",

            InvalidJustificationReviewException => arabic
                ? "لا تُتاح هذه الحركة من حالة العذر الحالية — قد يكون قد بُتّ فيه بالفعل (BR-ATD-005)."
                : "That move is not available from the excuse's current state — it may already have been decided (BR-ATD-005).",

            InvalidLeavePassTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة إذن الخروج الحالية: المطلوب يُعتمد أو يُرفض، والمعتمد يُستخدم عند البوابة (BR-ATD-006)."
                : "That move is not available from the leave pass's current state: a requested pass is approved or refused, and an approved one is used at the gate (BR-ATD-006).",

            // ---------------------------------------------------------------- M15 timetable

            PlacementConflictException e => arabic
                ? e.Conflict switch
                {
                    PlacementConflictKind.Teacher => "المعلّم مشغول بحصة أخرى في هذا الوقت — اختر معلّماً آخر أو حصة أخرى (BR-TTB-004).",
                    PlacementConflictKind.Section => "الشعبة عندها حصة أخرى في هذا الوقت — لا تجلس شعبة في درسين معاً (BR-TTB-004).",
                    _ => "القاعة مشغولة في هذا الوقت — اختر قاعة أخرى أو حصة أخرى (BR-TTB-004).",
                }
                : e.Conflict switch
                {
                    PlacementConflictKind.Teacher => "The teacher is already teaching in this period — choose another teacher, or another period (BR-TTB-004).",
                    PlacementConflictKind.Section => "The section already has a lesson in this period — one class cannot sit in two (BR-TTB-004).",
                    _ => "The room is already occupied in this period — choose another room, or another period (BR-TTB-004).",
                },

            TeacherNotAssignedException => arabic
                ? "هذا المعلّم غير مُسنَد لتدريس هذه المادة لهذه الشعبة — أسنِده في شاشة الإسناد التدريسي أولاً (BR-TCH-002)."
                : "This teacher is not assigned to teach this subject to this section — make the assignment on the teaching-assignments screen first (BR-TCH-002).",

            IncompletePlacementException e => arabic
                ? $"ينقص هذه المادة {Count(e.Shortfall)} حصة من نصابها الأسبوعي — الجدول لا يُعتمد وفيه نقص، فأكمل الحصص أو عدّل الخطة الأسبوعية (BR-TTB-003)."
                : $"This subject is {Count(e.Shortfall)} period(s) short of its weekly plan — a timetable is not approved with gaps, so place the rest or change the plan (BR-TTB-003).",

            InvalidTimetableVersionStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة نسخة الجدول الحالية: المسودة تُدقَّق، والمدقَّقة تُنشر أو تُعاد للمسودة (BR-TTB-002)."
                : "That move is not available from the timetable version's current state: a draft is validated, and a validated one is published or sent back to draft (BR-TTB-002).",

            TimetableVersionLockedException e => arabic
                ? (e.Status == Sms.Domain.Timetable.TimetableVersionStatus.Published
                    ? "نسخة الجدول منشورة والحصص مقفلة — التعديل بعد النشر يكون بنسخة جديدة، حتى يبقى ما رآه المعلّمون والأسر ثابتاً (BR-TTB-002/009)."
                    : "نسخة الجدول قيد التدقيق والحصص مقفلة — أعِدها إلى المسودة إن أردت تعديلها (BR-TTB-002/009).")
                : (e.Status == Sms.Domain.Timetable.TimetableVersionStatus.Published
                    ? "The timetable version is published and its placements are locked — after publication a change means a new version, so what teachers and families were shown stays put (BR-TTB-002/009)."
                    : "The timetable version is under review and its placements are locked — send it back to draft if you need to change it (BR-TTB-002/009)."),

            PeriodSlotInUseException e => arabic
                ? $"تشير إلى هذه الحصة {Count(e.PlacementCount)} حصة مجدولة، فلا تُحذف — انقلها أو احذفها أولاً (BR-TTB-001)."
                : $"{Count(e.PlacementCount)} scheduled lesson(s) sit in this period slot, so it cannot be removed — move or delete them first (BR-TTB-001).",

            SubstituteNotEligibleException => arabic
                ? "هذا المعلّم لا يصلح بديلاً لهذه الحصة — إمّا أنه غير مؤهّل لتدريس المادة، وإمّا أنه مشغول في وقتها (BR-TTB-007)."
                : "This teacher cannot cover the lesson — they are either not qualified for the subject, or already busy at that time (BR-TTB-007).",

            // ---------------------------------------------------------------- M18 certificates

            InvalidCertificateRequestStatusTransitionException => arabic
                ? "لا تُتاح هذه الحركة من حالة طلب الوثيقة الحالية — افتحه لترى ما وصل إليه (BR-CRT-003)."
                : "That move is not available from the document request's current state — open it to see where it stands (BR-CRT-003).",

            CertificateFeeClearanceBlockedException e => arabic
                ? $"على الأسرة مستحقات قدرها {Amount(e.Position)}، وهذه الوثيقة لا تُصرف قبل إخلاء الطرف المالي — سدّد المستحق، أو اطلب تجاوزاً من المدير مع كتابة سببه (BR-CRT-008)."
                : $"The family owes {Amount(e.Position)}, and this document is not released before fee clearance — settle the balance, or ask the Principal to override it and say why (BR-CRT-008).",

            CertificatePrerequisitesNotMetException => arabic
                ? "لم تكتمل شروط هذه الوثيقة — قد تكون النتائج لم تُنشر بعد، أو لم يُخلَ الطرف المالي؛ افتح الطلب لترى الشرط الناقص (BR-CRT-001/003)."
                : "This document's prerequisites are not met — results may not be published yet, or fees not cleared; open the request to see which one is missing (BR-CRT-001/003).",

            CertificateKindNotGateableException => arabic
                ? "لا تسمح حزمة الدولة المفعّلة بحجب هذا النوع من الوثائق بسبب رسوم غير مسددة، فلا يُضبَط عليه شرط إخلاء طرف (BR-CRT-008)."
                : "The active country pack does not allow this kind of document to be withheld over unpaid fees, so no clearance rule can be set on it (BR-CRT-008).",

            FeeClearanceRuleNotSupportedException => arabic
                ? "قاعدة «لا متأخرات» لا يمكن تقييمها بعد، لأن المطالبات لا تحمل تواريخ استحقاق حتى الآن — اختر قاعدة أخرى إلى أن تُفعَّل جداول الأقساط (BR-CRT-008)."
                : "The \"no overdue\" rule cannot be evaluated yet, because charges do not carry due dates — choose another rule until instalment schedules are in use (BR-CRT-008).",

            CertificateNotIssuedException => arabic
                ? "لا يُلغى إلا مستند صادر — هذه الوثيقة لم تصدر بعد أو أُلغيت من قبل (BR-CRT-006)."
                : "Only an issued document can be revoked — this one has not been issued, or was revoked already (BR-CRT-006).",

            // ---------------------------------------------------------------- M04 school calendar

            CalendarPastDateEditException e => arabic
                ? $"لا يُعدَّل تقويم يوم مضى — {Day(e.Date)} في الماضي، والحضور والحصص مرصودة عليه كما كان (BR-CAL-004)."
                : $"A day that has passed is not re-written — {Day(e.Date)} is in the past, and attendance and lessons were recorded against it as it stood (BR-CAL-004).",

            CalendarDateOutsideYearException e => arabic
                ? $"{Day(e.Date)} خارج نطاق تواريخ العام الدراسي — اختر يوماً داخل العام، أو عدّل تواريخ العام أولاً (BR-GLB-051)."
                : $"{Day(e.Date)} falls outside the academic year's dates — choose a day inside the year, or change the year's dates first (BR-GLB-051).",

            // ---------------------------------------------------------------- M03 year rollover

            RolloverYearStatusException e => arabic
                ? $"لا تُشغَّل الترحيلة إلا من عام فعّال إلى عام قيد الإعداد — العام المختار حالته «{Labels.YearStatus(e.Actual, true)}» والمطلوب «{Labels.YearStatus(e.Expected, true)}» (BR-AYR-008)."
                : $"A rollover runs from the active year into the year being prepared — the chosen year is {Labels.YearStatus(e.Actual, false)}, and this step needs it to be {Labels.YearStatus(e.Expected, false)} (BR-AYR-008).",

            RolloverBatchStatusException e => RolloverStep(e, arabic),

            PromotionPathIncompleteException e => arabic
                ? (e.HasCycle
                    ? "مسار الترفيع يدور على نفسه — صف يُرفَّع إلى صف يعود إليه؛ راجع السلسلة حتى تنتهي إلى صف التخرّج (BR-GRD-002)."
                    : $"{Count(e.GradeLevelIdsMissingTarget.Count)} صفاً فيه طلاب مقيّدون ولا يحمل صفاً يُرفَّعون إليه ولا يُعدّ صف تخرّج — حدّد مسار الترفيع لكل منها قبل تشغيل الترحيلة (BR-GRD-002).")
                : (e.HasCycle
                    ? "The promotion path loops back on itself — a grade promoting into one that promotes back into it; follow the chain through until it ends at the leaving grade (BR-GRD-002)."
                    : $"{Count(e.GradeLevelIdsMissingTarget.Count)} grade(s) have enrolled students but name no grade to promote into and are not marked as leaving — set a promotion path on each before running the rollover (BR-GRD-002)."),

            TargetGradeProfileMissingException => arabic
                ? "الصف الذي يُرفَّع إليه هذا الطالب غير معرَّف في العام الهدف — عرّفه في هيكل العام الجديد، أو أعد فتح الترحيلة لنسخ الصفوف، قبل البتّ."
                : "The grade this student would move into does not exist in the target year — define it in the new year's structure, or re-open the batch to copy the grades across, before deciding.",

            InvalidPromotionDecisionException e => arabic
                ? e.Fault switch
                {
                    PromotionDecisionFault.MustDecide => "القرار اليدوي لا بد أن يبتّ — «غير محدد» ليس قراراً.",
                    PromotionDecisionFault.GradeDoesNotGraduate => "لا يتخرّج طالب من صف ليس صف تخرّج — اختر الترفيع أو الإعادة.",
                    PromotionDecisionFault.NoPromotionTarget => "صف هذا الطالب لا يحمل صفاً يُرفَّع إليه — حدّد مسار الترفيع للصف أولاً (BR-GRD-002).",
                    _ => "الطلاب المتخرجون لا يُعاد تسجيلهم — أنهِ قيدهم بدل ترحيلهم.",
                }
                : e.Fault switch
                {
                    PromotionDecisionFault.MustDecide => "A manual decision has to decide — \"undecided\" is not one.",
                    PromotionDecisionFault.GradeDoesNotGraduate => "A student does not graduate from a grade that is not the last one — promote or retain them instead.",
                    PromotionDecisionFault.NoPromotionTarget => "This student's grade names no grade to promote into — set the grade's promotion path first (BR-GRD-002).",
                    _ => "Graduating students are not re-registered — close their enrolment rather than rolling them over.",
                },

            PromotionsUndecidedException e => arabic
                ? $"بقي {Count(e.UndecidedCount)} طالباً بلا قرار ترفيع — الترحيلة لا تُعتمد وفيها من لم يُبتّ في أمره (BR-AYR-008)."
                : $"{Count(e.UndecidedCount)} student(s) still have no promotion decision — the batch is not approved while anyone is undecided (BR-AYR-008).",

            NoSeatAvailableException => arabic
                ? "لا مقعد متبقياً في هذا الصف للعام القادم — المقاعد المخططة هي عدد الشعب في سعة الشعبة؛ زد شعبة أو ارفع السعة المخططة أولاً (BR-GRD-006)."
                : "There is no seat left in this grade for next year — planned seats are sections × section size; add a section or raise the planned size first (BR-GRD-006).",

            PromotionNotDecidedException => arabic
                ? "لم يُبتّ في ترفيع هذا الطالب بعد، ولا تُسنَد شعبة قبل معرفة الصف — احسم القرار أولاً."
                : "This student's promotion has not been decided, and no section is assigned before the grade is known — decide it first.",

            NoPayerForStudentException => arabic
                ? "لا يوجد لهذا الطالب من تُقيَّد عليه الرسوم — اربط وليّ أمر مسؤولاً مالياً وله سجل دافع قبل ترحيل الرسوم (BR-FEE-004)."
                : "This student has nobody to bill — link a financially responsible guardian with a payer record before fees can be carried over (BR-FEE-004).",

            ChecklistNotGreenException e => arabic
                ? $"قائمة «{e.ChecklistName}» لم تكتمل بعد — المتبقي: {string.Join("، ", e.Items.Where(i => !i.IsSatisfied).Select(i => i.Code))}. افتح القائمة لترى تفصيل كل بند وما ينقصه (BR-AYR-004/005)."
                : $"The {e.ChecklistName} checklist is not green yet — outstanding: {string.Join(", ", e.Items.Where(i => !i.IsSatisfied).Select(i => i.Code))}. Open it to see what each item is still missing (BR-AYR-004/005).",

            CarryForwardReconciliationException e => arabic
                ? $"لا يتوازن الترحيل المالي: المستحقات في نهاية العام {Amount(e.ClosingReceivables)} والأرصدة المفتتحة المرحّلة {Amount(e.OpeningBalances)} — لا يُقفل العام حتى يتطابقا (BR-AYR-009)."
                : $"The carry-forward does not reconcile: closing receivables are {Amount(e.ClosingReceivables)} while the opening balances posted are {Amount(e.OpeningBalances)} — the year does not close until they agree (BR-AYR-009).",

            _ => null,
        };

        /// <summary>
        /// The rollover's step guards. Two of the three are not about the batch at all — they fire
        /// when the student has already been enrolled by some other route — so they are separated
        /// out rather than folded into one sentence about a batch status the operator did not ask
        /// about.
        /// </summary>
        private static string RolloverStep(RolloverBatchStatusException e, bool arabic) => e.Blocker switch
        {
            RolloverStepBlocker.AlreadyEnrolledInTargetYear => arabic
                ? "هذا الطالب مقيَّد في العام الهدف بالفعل — إن أردت التراجع فاستخدم انسحاب القيد، لا الترحيلة."
                : "This student is already enrolled in the target year — to undo that, withdraw the enrolment; the rollover is not the way back.",

            RolloverStepBlocker.AlreadyEnrolled => arabic
                ? "هذا الطالب مقيَّد بالفعل — نقله الآن يكون تحويلاً من شاشة القبول والتسجيل، لا ترحيلاً."
                : "This student is already enrolled — moving them now is a transfer from the admissions screens, not a rollover.",

            _ => arabic
                ? $"لا تُتاح هذه الخطوة والترحيلة في مرحلة «{BatchStage(e.Actual, true)}» — تُتاح في: {string.Join("، ", e.Allowed.Select(s => BatchStage(s, true)))}."
                : $"This step is not available while the batch is {BatchStage(e.Actual, false)} — it runs from: {string.Join(", ", e.Allowed.Select(s => BatchStage(s, false)))}.",
        };

        /// <summary>
        /// The rollover batch's stage as the rollover console names it. Kept here rather than in
        /// <c>Labels</c> because no screen shows this enum yet; when one does, it moves there and
        /// this goes away.
        /// </summary>
        private static string BatchStage(Sms.Domain.Rollover.RolloverBatchStatus status, bool arabic) => status switch
        {
            Sms.Domain.Rollover.RolloverBatchStatus.Open => arabic ? "مفتوحة" : "open",
            Sms.Domain.Rollover.RolloverBatchStatus.PromotionsApproved => arabic ? "الترفيعات معتمدة" : "promotions approved",
            Sms.Domain.Rollover.RolloverBatchStatus.Activated => arabic ? "مفعّلة" : "activated",
            _ => arabic ? "مغلقة" : "closed",
        };
    }
}
