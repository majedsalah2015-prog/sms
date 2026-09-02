using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Common.Guards;
using Sms.Domain.Students;

namespace Sms.Application.Students
{
    /// <summary>
    /// The enrollment usage guard, plus the one question a screen asks that a single-record guard
    /// answers badly: <b>all</b> of one student's enrollments at once.
    /// <para>
    /// The write path only ever holds one id, so <see cref="IUsageInspector{T}"/> is the right shape
    /// for it. The student file is the other case: it draws a remove button per row of the academic
    /// history, and each button has to know in advance whether it could work. Asking row by row
    /// meant a dozen references counted a dozen times over — around eighty round trips on a page a
    /// registrar opens all day, to decide the state of a handful of buttons.
    /// </para>
    /// <para>
    /// So the batch is one grouped query per referencing table rather than one query per table per
    /// row, and it lives beside the single-record method so both are built from the same list of
    /// things that can reference an enrollment. A second list maintained in a controller is how the
    /// screen and the rule come to disagree about what "in use" means.
    /// </para>
    /// </summary>
    public interface IEnrollmentUsageInspector : IUsageInspector<Enrollment>
    {
        /// <summary>
        /// What each of <paramref name="enrollmentIds"/> is referenced by. Every id asked for is
        /// present in the result; one nothing references maps to <see cref="UsageReport.Free"/>.
        /// </summary>
        Task<IReadOnlyDictionary<int, UsageReport>> InspectManyAsync(
            IReadOnlyList<int> enrollmentIds, CancellationToken cancellationToken = default);
    }
}
