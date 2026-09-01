using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Sms.Application.Common.Exceptions;
using Sms.Application.Security;

namespace Sms.Web.Api
{
    /// <summary>
    /// Turns a refusal into the sentence the person holding the phone reads.
    /// <para>
    /// The standing rule in this repository is that every refusal a user can
    /// trigger is translated at the web boundary and an engine's English
    /// exception text never reaches a screen. A JSON body is a screen: an app
    /// that renders <c>ex.Message</c> shows Arabic users English, and the
    /// failure is invisible from the server side because the request succeeded
    /// in returning its error.
    /// </para>
    /// <para>
    /// <b>Why an explicit table and not <c>catch (InvalidOperationException)</c>.</b>
    /// Every domain exception in this product derives from
    /// <see cref="InvalidOperationException"/> — and so does "Sequence contains
    /// no matching element", which is the shape a genuine bug takes here (see
    /// the soft-active lookup trap in CLAUDE.md). Catching the base type would
    /// dress that bug up as a business rule and return it as a tidy 409, which
    /// is the worst possible outcome: the client shows a plausible refusal and
    /// nobody ever learns the screen is broken. So known types are mapped one by
    /// one; <see cref="TryTranslate"/> declines everything else and lets it
    /// become a 500 with a log entry.
    /// </para>
    /// </summary>
    public static class ApiProblem
    {
        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        /// <summary>
        /// The status and body for <paramref name="exception"/>, or null when
        /// this is not a refusal — meaning it is a fault, and the caller must
        /// let it through rather than tidy it away.
        /// </summary>
        public static bool TryTranslate(Exception exception, out int status, out ApiError error)
        {
            switch (exception)
            {
                // ---------------------------------------------------------- sign-in (doc 06 §3)
                case InvalidCredentialsException:
                    status = StatusCodes.Status401Unauthorized;
                    error = new ApiError("invalid_credentials",
                        T("Invalid username or password.", "اسم المستخدم أو كلمة المرور غير صحيحة."));
                    return true;

                case AccountLockedOutException locked:
                {
                    var minutes = Math.Max(1, (int)Math.Ceiling((locked.UnlocksAtUtc - DateTime.UtcNow).TotalMinutes));
                    status = StatusCodes.Status423Locked;
                    error = new ApiError("account_locked",
                        T($"Account is temporarily locked. Try again in {minutes} minute(s).",
                          $"الحساب مقفل مؤقتاً. حاول مرة أخرى بعد {minutes} دقيقة."));
                    return true;
                }

                case InvalidTwoFactorCodeException:
                    status = StatusCodes.Status401Unauthorized;
                    error = new ApiError("invalid_two_factor_code",
                        T("The verification code is not valid.", "رمز التحقق غير صحيح."));
                    return true;

                case PasswordPolicyViolationException policy:
                    status = StatusCodes.Status422UnprocessableEntity;
                    error = new ApiError("password_policy",
                        T("The new password does not meet the policy.", "كلمة المرور الجديدة لا تحقق السياسة."),
                        new Dictionary<string, string[]>
                        {
                            ["newPassword"] = policy.Violations.Select(Describe).ToArray(),
                        });
                    return true;

                // ---------------------------------------------------------- portal visibility (BR-SEC-011)
                //
                // 404, not 403, and deliberately so. Answering "forbidden" tells a
                // parent that the student id they guessed exists; the browser portal
                // has always hidden that and the API does not get to be more candid.
                case PortalAccessDeniedException:
                    status = StatusCodes.Status404NotFound;
                    error = NotFound();
                    return true;

                // ---------------------------------------------------------- cross-cutting guards
                case CrossSchoolWriteException:
                    status = StatusCodes.Status403Forbidden;
                    error = new ApiError("cross_school_write",
                        T("That record belongs to another school.", "هذا السجل يخص مدرسة أخرى."));
                    return true;

                case HardDeleteForbiddenException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("hard_delete_forbidden",
                        T("Records are deactivated here, never deleted.", "السجلات تُعطَّل هنا ولا تُحذف."));
                    return true;

                case MissingAuditReasonException:
                    status = StatusCodes.Status422UnprocessableEntity;
                    error = new ApiError("audit_reason_required",
                        T("This change needs a stated reason.", "هذا التعديل يحتاج سبباً مذكوراً."));
                    return true;

                // ---------------------------------------------------------- e-learning (doc/Modules/37)
                case TeachingReachException:
                    status = StatusCodes.Status403Forbidden;
                    error = new ApiError("outside_teaching_reach",
                        T("You do not teach this class or subject.", "أنت لا تُدرِّس هذا الصف أو هذه المادة."));
                    return true;

                case LessonTransitionException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("lesson_transition_refused",
                        T("The lesson is not in a state that allows this.", "حالة الدرس لا تسمح بهذا الإجراء."));
                    return true;

                case LessonRetiredException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("lesson_retired",
                        T("This lesson has been withdrawn.", "تم سحب هذا الدرس."));
                    return true;

                case LessonSessionMismatchException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("lesson_session_mismatch",
                        T("That session does not belong to this lesson's offering.",
                          "هذه الحصة لا تتبع مقرر هذا الدرس."));
                    return true;

                case ResourceNotScanCleanException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("resource_not_scan_clean",
                        T("The file is still being scanned, or was rejected.",
                          "الملف ما زال قيد الفحص أو تم رفضه."));
                    return true;

                case HomeworkTransitionException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("homework_transition_refused",
                        T("The homework is not in a state that allows this.", "حالة الواجب لا تسمح بهذا الإجراء."));
                    return true;

                case HomeworkIssueRefusedException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("homework_issue_refused",
                        T("The homework cannot be issued yet.", "لا يمكن إصدار الواجب بعد."));
                    return true;

                case HomeworkWithdrawalBlockedException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("homework_withdrawal_blocked",
                        T("The homework can no longer be withdrawn.", "لم يعد بالإمكان سحب الواجب."));
                    return true;

                // ---------------------------------------------------------- students (doc/Modules/10)
                case LastFinanciallyResponsibleGuardianException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("last_financially_responsible_guardian",
                        T("A student must keep one financially responsible guardian.",
                          "يجب أن يبقى للطالب ولي أمر مسؤول مالياً واحد على الأقل."));
                    return true;

                case InvalidStudentStatusTransitionException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("invalid_student_status_transition",
                        T("That status change is not allowed from the current status.",
                          "تغيير الحالة هذا غير مسموح من الحالة الحالية."));
                    return true;

                case DuplicateEnrollmentException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("duplicate_enrollment",
                        T("The student is already enrolled for that year.", "الطالب مُقيَّد بالفعل لهذا العام."));
                    return true;


                // ---------------------------------------------------------- employees (doc/Modules/12)
                case InvalidEmployeeStatusTransitionException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("invalid_employee_status_transition",
                        T("That status change is not allowed from the current status.",
                          "تغيير الحالة هذا غير مسموح من الحالة الحالية."));
                    return true;

                case InvalidContractStatusTransitionException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("invalid_contract_status_transition",
                        T("That contract status change is not allowed.", "تغيير حالة العقد هذا غير مسموح."));
                    return true;

                case OverlappingContractException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("overlapping_contract",
                        T("The employee already has a contract covering those dates.",
                          "لدى الموظف عقد يغطي هذه الفترة بالفعل."));
                    return true;

                case ContractNotEditableException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("contract_not_editable",
                        T("An active or ended contract cannot be edited.", "لا يمكن تعديل عقد نشط أو منتهٍ."));
                    return true;

                case OrgUnitInUseException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("org_unit_in_use",
                        T("The unit still has staff or child units on it.",
                          "الوحدة ما زال عليها موظفون أو وحدات فرعية."));
                    return true;

                case QualificationNotFoundException:
                    status = StatusCodes.Status404NotFound;
                    error = new ApiError("qualification_not_found",
                        T("That qualification is not on this file.", "هذا المؤهل ليس على هذا الملف."));
                    return true;

                // ---------------------------------------------------------- fees (doc/Modules/19)
                case InvalidFeeStructureLineStatusTransitionException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("invalid_fee_line_status_transition",
                        T("That fee line status change is not allowed.", "تغيير حالة بند الرسوم هذا غير مسموح."));
                    return true;

                case FeeStructureLineNotApprovedException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("fee_line_not_approved",
                        T("The fee line has not been approved yet.", "بند الرسوم لم يُعتمد بعد."));
                    return true;

                case FeeStructureLineNotDraftException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("fee_line_not_draft",
                        T("Only a draft fee line can be changed.", "لا يمكن تعديل إلا بند رسوم في حالة مسودة."));
                    return true;

                case FeeStructureLineInUseException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("fee_line_in_use",
                        T("Charges have already been posted from this fee line.",
                          "تم ترحيل رسوم من هذا البند بالفعل."));
                    return true;

                case FeeStructureLineAlreadyExistsException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("fee_line_already_exists",
                        T("That grade already has a line for this fee category.",
                          "هذا الصف لديه بند لهذا التصنيف بالفعل."));
                    return true;

                case FeeCategoryInUseException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("fee_category_in_use",
                        T("The category is still used by a fee structure.",
                          "التصنيف ما زال مستخدماً في هيكل رسوم."));
                    return true;

                case ChargeNotPostedException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("charge_not_posted",
                        T("The charge is not posted.", "الرسم غير مُرحَّل."));
                    return true;

                case ChargeHasActivityException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("charge_has_activity",
                        T("The charge already carries payments or credit notes.",
                          "على الرسم دفعات أو إشعارات دائنة بالفعل."));
                    return true;

                case CreditNoteExceedsChargeException:
                    status = StatusCodes.Status422UnprocessableEntity;
                    error = new ApiError("credit_note_exceeds_charge",
                        T("The credit note is larger than what is left on the charge.",
                          "الإشعار الدائن أكبر من المتبقي على الرسم."));
                    return true;

                // ---------------------------------------------------------- payments (doc/Modules/21)
                case TillSessionNotOpenException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("till_session_not_open",
                        T("No till session is open for this cashier.", "لا توجد وردية صندوق مفتوحة لهذا الصراف."));
                    return true;

                case CashierAlreadyHasOpenTillException cashierTill:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("cashier_till_already_open",
                        T($"You already have till {cashierTill.TillCode} open — close it first.",
                          $"لديك وردية مفتوحة على الصندوق {cashierTill.TillCode} — أغلقها أولاً."));
                    return true;

                case TillAlreadyOpenException openTill:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("till_already_open",
                        T($"Till {openTill.TillCode} is already open for another cashier.",
                          $"الصندوق {openTill.TillCode} مفتوح لأمين آخر."));
                    return true;

                case InvalidPdcStatusTransitionException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("invalid_pdc_status_transition",
                        T("That cheque status change is not allowed.", "تغيير حالة الشيك هذا غير مسموح."));
                    return true;

                case InvalidRefundVoucherStatusTransitionException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("invalid_refund_status_transition",
                        T("That refund status change is not allowed.", "تغيير حالة سند الاسترداد هذا غير مسموح."));
                    return true;

                case RefundExceedsPositionException:
                    status = StatusCodes.Status422UnprocessableEntity;
                    error = new ApiError("refund_exceeds_position",
                        T("The refund is larger than the credit on the account.",
                          "مبلغ الاسترداد أكبر من الرصيد الدائن على الحساب."));
                    return true;

                // ---------------------------------------------------------- installments (doc/Modules/20)
                case InvalidTemplateSplitException:
                    status = StatusCodes.Status422UnprocessableEntity;
                    error = new ApiError("invalid_template_split",
                        T("The instalment percentages must add up to 100.",
                          "نسب الأقساط يجب أن يكون مجموعها 100."));
                    return true;

                case PlanTemplateNotApprovedException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("plan_template_not_approved",
                        T("The plan template has not been approved.", "قالب الخطة لم يُعتمد."));
                    return true;

                case PlanTemplateNotDraftException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("plan_template_not_draft",
                        T("Only a draft plan template can be changed.", "لا يمكن تعديل إلا قالب خطة في حالة مسودة."));
                    return true;

                case TemplateCategoryNotMandatoryException:
                    status = StatusCodes.Status422UnprocessableEntity;
                    error = new ApiError("template_category_not_mandatory",
                        T("That fee category cannot carry an instalment plan.",
                          "هذا التصنيف لا يمكن أن يحمل خطة أقساط."));
                    return true;

                case NoChargesToScheduleException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("no_charges_to_schedule",
                        T("There are no posted charges to schedule.", "لا توجد رسوم مُرحَّلة لجدولتها."));
                    return true;

                case PlanAssignmentExistsException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("plan_assignment_exists",
                        T("The student already has a plan for this year.", "لدى الطالب خطة لهذا العام بالفعل."));
                    return true;

                case ExceptionAssignmentReasonRequiredException:
                    status = StatusCodes.Status422UnprocessableEntity;
                    error = new ApiError("assignment_reason_required",
                        T("An out-of-policy assignment needs a stated reason.",
                          "التخصيص الاستثنائي يحتاج سبباً مذكوراً."));
                    return true;

                case RescheduleRemainderMismatchException:
                    status = StatusCodes.Status422UnprocessableEntity;
                    error = new ApiError("reschedule_remainder_mismatch",
                        T("The proposed instalments do not add up to what is left.",
                          "الأقساط المقترحة لا تساوي المتبقي."));
                    return true;

                case RescheduleCaseNotPendingException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("reschedule_case_not_pending",
                        T("That reschedule case has already been decided.", "تم البت في طلب إعادة الجدولة هذا."));
                    return true;

                case PromiseDateOutOfRangeException:
                    status = StatusCodes.Status422UnprocessableEntity;
                    error = new ApiError("promise_date_out_of_range",
                        T("The promised date is outside the allowed horizon.",
                          "تاريخ الوعد خارج المدى المسموح."));
                    return true;

                case InstallmentNotOverdueException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("installment_not_overdue",
                        T("The instalment is not overdue.", "القسط غير متأخر."));
                    return true;

                case InstallmentNotOpenException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("installment_not_open",
                        T("The instalment is not open.", "القسط غير مفتوح."));
                    return true;

                case PdcNotCoverableException:
                    status = StatusCodes.Status409Conflict;
                    error = new ApiError("pdc_not_coverable",
                        T("That cheque cannot cover this instalment.", "هذا الشيك لا يمكنه تغطية هذا القسط."));
                    return true;

                default:
                    status = 0;
                    error = null!;
                    return false;
            }
        }

        /// <summary>Reused verbatim from the sign-in screen, so both transports refuse a password the same way.</summary>
        private static string Describe(PasswordPolicyViolation violation) => violation switch
        {
            PasswordPolicyViolation.TooShort => T("At least 10 characters.", "10 أحرف على الأقل."),
            PasswordPolicyViolation.MissingUppercase => T("Include an uppercase letter.", "يجب أن تحتوي على حرف كبير."),
            PasswordPolicyViolation.MissingLowercase => T("Include a lowercase letter.", "يجب أن تحتوي على حرف صغير."),
            PasswordPolicyViolation.MissingDigit => T("Include a digit.", "يجب أن تحتوي على رقم."),
            PasswordPolicyViolation.MissingSymbol => T("Include a symbol.", "يجب أن تحتوي على رمز."),
            PasswordPolicyViolation.ReusesRecentPassword => T("Cannot reuse one of your last 5 passwords.", "لا يمكن إعادة استخدام إحدى كلمات المرور الخمس الأخيرة."),
            _ => violation.ToString(),
        };

        public static ApiError Unauthenticated() => new(
            "unauthenticated",
            T("Sign in again.", "سجّل الدخول من جديد."));

        public static ApiError Forbidden() => new(
            "forbidden",
            T("You are not allowed to do that.", "لا تملك صلاحية هذا الإجراء."));

        /// <summary>
        /// Also what a missing permission looks like. Deliberately the same body
        /// as a genuinely absent record: BR-SEC-010 says unauthorized surface
        /// disappears rather than errors, and a distinguishable message would
        /// undo that on the one client that reads it.
        /// </summary>
        public static ApiError NotFound() => new(
            "not_found",
            T("Not found.", "غير موجود."));

        public static ApiError MustChangePassword() => new(
            "must_change_password",
            T("Change your password before continuing.", "غيّر كلمة المرور قبل المتابعة."));

        /// <summary>
        /// The binder's own messages, already bilingual: Startup replaces every
        /// <c>ModelBindingMessageProvider</c> accessor with a per-request
        /// translated one, so what arrives here is in the caller's language
        /// before this method sees it.
        /// <para>
        /// <b>Except one class of them.</b> The JSON reader runs before model
        /// binding, so no message provider covers it: a body the parser cannot
        /// read produces its own sentence — <c>"The JSON value could not be
        /// converted to System.String. Path: $.nameAr | LineNumber: 0 |
        /// BytePositionInLine: 13"</c> — in English whatever the caller asked
        /// for, and MVC copies it into model state verbatim because it treats an
        /// <c>InputFormatterException</c> message as safe to show a client.
        /// Arriving inside an otherwise translated envelope, it reads as
        /// deliberate. Found by smoke-testing the API in Arabic, 2026-08-31.
        /// </para>
        /// <para>
        /// Those errors are told apart by their <b>key</b>, which is the JSON
        /// path and therefore starts with <c>$</c>; a written rule keys on a CLR
        /// property name and never does. The parser's detail is a developer's
        /// diagnostic and not a sentence to show anybody, so the field gets a
        /// translated statement of what is wrong with it instead, under a key
        /// the client can match to the property it sent.
        /// </para>
        /// </summary>
        public static ApiError Validation(ModelStateDictionary modelState)
        {
            var fields = new Dictionary<string, string[]>(StringComparer.Ordinal);

            foreach (var entry in modelState.Where(e => e.Value.Errors.Count > 0))
            {
                var fromTheParser = entry.Key.StartsWith("$", StringComparison.Ordinal);
                var key = Camel(fromTheParser ? entry.Key.TrimStart('$', '.') : entry.Key);

                // A body that is not JSON at all has no path, so the whole document is the
                // subject and there is no field to name.
                if (key.Length == 0)
                {
                    key = "body";
                }

                fields[key] = entry.Value.Errors
                    .Select(error => Describe(error, fromTheParser))
                    .ToArray();
            }

            return new ApiError("validation_failed",
                T("Some fields need attention.", "بعض الحقول تحتاج إلى تصحيح."),
                fields);
        }

        private static string Describe(ModelError error, bool fromTheParser)
        {
            if (fromTheParser || error.Exception != null)
            {
                return T("The value is not in the expected format.", "القيمة ليست بالصيغة المتوقعة.");
            }

            return string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? T("The value is not valid.", "القيمة غير صالحة.")
                : error.ErrorMessage;
        }

        /// <summary>Model-state keys are CLR property names; the JSON the client sent was camelCase.</summary>
        private static string Camel(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return key;
            }

            var parts = key.Split('.');
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0 && char.IsUpper(parts[i][0]))
                {
                    parts[i] = char.ToLowerInvariant(parts[i][0]) + parts[i].Substring(1);
                }
            }

            return string.Join(".", parts);
        }
    }

    /// <summary>
    /// Writing an <see cref="ApiError"/> from the two places that cannot return
    /// an <see cref="IActionResult"/> — the authentication handler, which runs
    /// before MVC, and the status-code re-shaper.
    /// </summary>
    public static class ApiResults
    {
        internal static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            // Arabic must survive the wire as Arabic, not as أحرف.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static async Task WriteAsync(HttpResponse response, int status, ApiError error, CancellationToken cancellationToken = default)
        {
            if (response.HasStarted)
            {
                return;
            }

            response.StatusCode = status;
            response.ContentType = "application/json; charset=utf-8";
            await JsonSerializer.SerializeAsync(response.Body, new ApiErrorResponse(error), Json, cancellationToken);
        }

        public static ObjectResult Error(int status, ApiError error)
            => new(new ApiErrorResponse(error)) { StatusCode = status };
    }
}
