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

        Task<StatementIssue> IssueAsync(int payerId, CancellationToken cancellationToken = default);
    }
}
