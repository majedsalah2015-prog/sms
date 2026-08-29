using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Discounts;

namespace Sms.Application.Statements
{
    /// <summary>
    /// doc/Modules/19 §8.7 Student/payer position — statement of account,
    /// BR-DIS-010 (gross / discounts / net always separated). BuildAsync is
    /// the on-demand view; IssueAsync is the formal numbered letter (E-501's
    /// dunning "statement letter" flag stage; Module 18 numbering pattern).
    /// </summary>
    public interface IStatementService
    {
        Task<PayerStatement> BuildAsync(int payerId, DateTime? asOfUtc = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// The same statement read down one child instead of across a family — doc/Modules/19 §8.7's
        /// "per student" half of BR-FEE-008, which the payer view cannot answer: a guardian paying
        /// for four children has one balance, and the question at the counter is usually about one
        /// of them.
        /// <para>
        /// Payments arrive here as <b>allocations</b> against this student's charges rather than as
        /// receipts, because a receipt belongs to the payer and BR-PAY-003 spreads it oldest-first
        /// across whichever children it lands on. Allocating is the only step that says which child
        /// a riyal paid for, so it is the only honest payment line on a per-student statement.
        /// A receipt still sitting unallocated is family money and appears on neither child's.
        /// </para>
        /// <para>
        /// Refunds are absent for the same reason and cannot be fixed here: a refund voucher names
        /// a payer and no student, so there is nothing to attribute. A refunded family reconciles
        /// on the payer statement.
        /// </para>
        /// </summary>
        Task<PayerStatement> BuildForStudentAsync(int studentId, DateTime? asOfUtc = null, CancellationToken cancellationToken = default);

        Task<StatementIssue> IssueAsync(int payerId, CancellationToken cancellationToken = default);
    }
}
