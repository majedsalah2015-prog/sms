using System;
using System.Linq;
using Sms.Application.Common.Exceptions;

namespace Sms.Web.Models
{
    /// <summary>
    /// Every refusal the payroll and staff-advance engines can raise, in the reader's language
    /// (owner request, 2026-08-28).
    /// <para>
    /// Its own table rather than a corner of <see cref="Finance"/>: these are met by a payroll
    /// officer on the two or three days a month when salaries are being run, and the thing that
    /// makes them actionable is naming the employee, the month or the payroll number that is in
    /// the way. A finance clerk chasing a parent's balance never sees one of them.
    /// </para>
    /// <para>
    /// Employees are named by their employee number, which reads the same in both languages and is
    /// what the officer has on the register in front of them — see
    /// <see cref="NegativeNetPayException"/> for why the exception carries it rather than a row id.
    /// </para>
    /// </summary>
    public static partial class UserMessage
    {
        private static string? Payroll(Exception exception, bool arabic) => exception switch
        {
            // ---------------------------------------------------------------- the monthly run

            InvalidPayrollPeriodException => arabic
                ? "الشهر المحدد غير صحيح — اختر شهراً بين 1 و12 وسنة صالحة."
                : "That is not a real month — choose a month between 1 and 12 and a valid year.",

            DuplicatePayrollRunException e => arabic
                ? $"يوجد مسير قائم لهذا الشهر بالرقم {e.ExistingRunNo} — افتحه وتابع عليه، أو ألغِه أولاً إن كان قد فُتح بالخطأ. شهر واحد لا يحمل أكثر من مسير."
                : $"Payroll {e.ExistingRunNo} already covers this month — open it and carry on there, or cancel it first if it was opened by mistake. One month never carries more than one payroll.",

            PayrollRunNotEditableException e => arabic
                ? $"المسير {e.RunNo} حالته «{PayrollLabels.RunStatus(e.Status, true)}» ولم يعد يقبل التعديل — أعِده إلى المسودة أولاً، فالاعتماد يُجمّد الحساب عمداً."
                : $"Payroll {e.RunNo} is {PayrollLabels.RunStatus(e.Status, false).ToLowerInvariant()} and no longer accepts changes — reopen it as a draft first; approving freezes the arithmetic on purpose.",

            EmptyPayrollRunException => arabic
                ? "لا يُعتمد مسير بلا موظفين — أنشئ البنود من العقود أولاً، أو أضف الموظفين يدوياً."
                : "A payroll with nobody on it cannot be approved — generate the lines from the contracts first, or add the employees by hand.",

            NegativeNetPayException e => arabic
                ? $"الاستقطاعات تتجاوز الراتب لدى: {Join(e.EmployeeNos, true)}. خفّض الاستقطاع، أو أعفِ قسط السلفة لهذا الشهر من شاشة السلفة، ثم أعِد الاعتماد — لا يُصرف راتب بالسالب."
                : $"Deductions exceed pay for: {Join(e.EmployeeNos, false)}. Reduce the deduction, or waive this month's advance instalment from the advance screen, then approve again — no salary is paid as a negative.",

            NegativePayComponentException => arabic
                ? "المبلغ يجب أن يكون أكبر من صفر — اتجاه البند يُحدَّد من نوعه (إضافة أو استقطاع) لا من إشارة سالبة."
                : "The amount must be greater than zero — whether it adds or deducts is set by the item's kind, not by a minus sign.",

            DuplicatePayrollLineException e => arabic
                ? $"هذا الموظف مدرج في المسير {e.RunNo} أصلاً — لكل موظف بند واحد في الشهر."
                : $"This employee is already on payroll {e.RunNo} — one line per employee per month.",

            NoActiveContractException => arabic
                ? "لا يوجد عقد ساري يغطي هذا الشهر لهذا الموظف — فعّل العقد أولاً، أو أدخل الراتب الأساسي والبدلات يدوياً عند الإضافة."
                : "No active contract covers this month for that employee — activate the contract first, or type the basic and allowances in by hand when adding them.",

            InvalidPayrollRunStatusTransitionException e => arabic
                ? $"لا يمكن نقل المسير من «{PayrollLabels.RunStatus(e.From, true)}» إلى «{PayrollLabels.RunStatus(e.To, true)}» — والمسير المصروف نهائي لا يُفتح ولا يُلغى."
                : $"A payroll cannot move from {PayrollLabels.RunStatus(e.From, false).ToLowerInvariant()} to {PayrollLabels.RunStatus(e.To, false).ToLowerInvariant()} — and a paid one is final: it is neither reopened nor cancelled.",

            // ---------------------------------------------------------------- staff advances

            InvalidAdvanceAmountException => arabic
                ? "مبلغ السلفة يجب أن يكون أكبر من صفر."
                : "The advance amount must be greater than zero.",

            InvalidAdvanceInstallmentCountException e => arabic
                ? $"عدد الأقساط يجب أن يكون بين 1 و{Count(e.Maximum)}، وأن يكون المبلغ كافياً ليصيب كلَّ قسط شيء — لا يُقسَّط مبلغ على أشهر أكثر من قروشه."
                : $"The instalment count must be between 1 and {Count(e.Maximum)}, and the amount must be large enough to give every instalment something — a sum cannot be spread over more months than it has cents.",

            OutstandingAdvanceException e => arabic
                ? $"لهذا الموظف سلفة قائمة بالرقم {e.OutstandingAdvanceNo} — تُسدَّد أو تُغلق قبل منح سلفة جديدة، فلا تجتمع سلفتان على راتب واحد."
                : $"This employee already has advance {e.OutstandingAdvanceNo} running — it must be settled or closed before another is granted; two advances never sit on one salary.",

            InvalidSalaryAdvanceStatusTransitionException e => arabic
                ? $"لا يمكن نقل السلفة من «{PayrollLabels.AdvanceStatus(e.From, true)}» إلى «{PayrollLabels.AdvanceStatus(e.To, true)}» — والمبلغ المصروف لا يُلغى، وإنما يُسدَّد أو يُعفى."
                : $"An advance cannot move from {PayrollLabels.AdvanceStatus(e.From, false).ToLowerInvariant()} to {PayrollLabels.AdvanceStatus(e.To, false).ToLowerInvariant()} — money already handed over is not cancelled away; it is repaid or waived.",

            InstallmentNotWaivableException e => arabic
                ? $"هذا القسط حالته «{PayrollLabels.InstallmentStatus(e.Status, true)}» — ولا يُعفى إلا القسط المجدول الذي لم يُستقطع بعد."
                : $"That instalment is {PayrollLabels.InstallmentStatus(e.Status, false).ToLowerInvariant()} — only a scheduled instalment that has not yet been deducted can be waived.",

            InstallmentLockedByPayrollRunException e => arabic
                ? $"شهر هذا القسط مشمول بالمسير {e.RunNo} وحالته «{PayrollLabels.RunStatus(e.RunStatus, true)}» — أعِد ذلك المسير إلى المسودة أولاً، وإلا اختلف الكشف عمّا صُرف."
                : $"This instalment's month is covered by payroll {e.RunNo}, which is {PayrollLabels.RunStatus(e.RunStatus, false).ToLowerInvariant()} — reopen that payroll as a draft first, or the statement will disagree with what was paid.",

            _ => null,
        };

        /// <summary>Employee numbers listed with the separator the reader's script uses — Arabic's comma is not a comma.</summary>
        private static string Join(System.Collections.Generic.IReadOnlyList<string> values, bool arabic) =>
            string.Join(arabic ? "، " : ", ", values.DefaultIfEmpty("—"));
    }
}
