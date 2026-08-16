using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Teachers;

namespace Sms.Application.Teachers
{
    /// <summary>doc/Modules/13 §8 Assignment matrix / Load board screens backing (screens deferred, the operations are core).</summary>
    public interface ITeacherAdmin
    {
        /// <summary>Throws <see cref="Common.Exceptions.EmployeeNotEligibleForTeachingException"/> if the employee has no active contract (BR-TCH-001).</summary>
        Task<TeacherProfile> DesignateTeacherAsync(int employeeId, int maxWeeklyPeriods, CancellationToken cancellationToken = default);

        /// <summary>
        /// Throws <see cref="Common.Exceptions.DuplicatePrimaryTeacherException"/> (BR-TCH-005) or
        /// <see cref="Common.Exceptions.LoadExceededException"/> (BR-TCH-004) unless overrideLoad is set.
        /// </summary>
        Task<TeacherAssignment> AssignAsync(
            int teacherProfileId, int curriculumOfferingId, int sectionId, TeacherRole role, DateTime effectiveFromUtc,
            bool overrideLoad = false, CancellationToken cancellationToken = default);
    }
}
