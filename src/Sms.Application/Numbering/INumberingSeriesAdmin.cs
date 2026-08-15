using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Numbering;

namespace Sms.Application.Numbering
{
    /// <summary>
    /// Series definition and cutover (doc 08 §3, BR-NUM-005). Before the
    /// first number is issued a series is freely editable in place; once
    /// <see cref="NumberingSeries.IsLocked"/>, a further definition call
    /// deactivates it and opens a new version — old versions stay queryable
    /// for continuity reporting, never deleted, mirroring
    /// <see cref="Workflow.IWorkflowService"/>'s versioned definitions.
    /// Permission-gating and the Finance-Manager P2 dual-approval on
    /// financial series are a later slice (doc 06 §4.3 pattern), same as
    /// Configure-Security in E-003.
    /// </summary>
    public interface INumberingSeriesAdmin
    {
        Task<NumberingSeries> DefineSeriesAsync(
            string code,
            string entityName,
            string formatTemplate,
            ResetPolicy resetPolicy,
            GapPolicy gapPolicy,
            DateTime effectiveFromUtc,
            CancellationToken cancellationToken = default);
    }
}
