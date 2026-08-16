using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Teachers;
using Sms.Domain.Employees;
using Sms.Domain.Teachers;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Teachers
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class TeacherAdmin : ITeacherAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;

        public TeacherAdmin(AppDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        private async Task<bool> HasActiveContractAsync(int employeeId, CancellationToken cancellationToken)
            => await _db.Contracts.AnyAsync(
                c => c.EmployeeId == employeeId && c.Status == ContractStatus.Active
                    && c.StartDate <= _clock.UtcNow && c.EndDate >= _clock.UtcNow,
                cancellationToken);

        public async Task<TeacherProfile> DesignateTeacherAsync(int employeeId, int maxWeeklyPeriods, CancellationToken cancellationToken = default)
        {
            if (!await HasActiveContractAsync(employeeId, cancellationToken))
            {
                throw new EmployeeNotEligibleForTeachingException(employeeId);
            }

            var profile = new TeacherProfile
            {
                EmployeeId = employeeId,
                MaxWeeklyPeriods = maxWeeklyPeriods,
            };
            _db.TeacherProfiles.Add(profile);

            await _db.SaveChangesAsync(cancellationToken);
            return profile;
        }

        public async Task<TeacherAssignment> AssignAsync(
            int teacherProfileId, int curriculumOfferingId, int sectionId, TeacherRole role, DateTime effectiveFromUtc,
            bool overrideLoad = false, CancellationToken cancellationToken = default)
        {
            var profile = await _db.TeacherProfiles.SingleAsync(p => p.Id == teacherProfileId, cancellationToken);
            var offering = await _db.CurriculumOfferings.SingleAsync(o => o.Id == curriculumOfferingId, cancellationToken);

            if (!await HasActiveContractAsync(profile.EmployeeId, cancellationToken))
            {
                throw new EmployeeNotEligibleForTeachingException(profile.EmployeeId);
            }

            if (role == TeacherRole.Primary)
            {
                var primaryExists = await _db.TeacherAssignments.AnyAsync(
                    a => a.CurriculumOfferingId == curriculumOfferingId && a.SectionId == sectionId
                        && a.Role == TeacherRole.Primary && a.EffectiveToUtc == null,
                    cancellationToken);
                if (primaryExists)
                {
                    throw new DuplicatePrimaryTeacherException(curriculumOfferingId, sectionId);
                }
            }

            if (!overrideLoad)
            {
                var assignedPeriods = await (
                    from a in _db.TeacherAssignments
                    join o in _db.CurriculumOfferings on a.CurriculumOfferingId equals o.Id
                    where a.TeacherProfileId == teacherProfileId && a.EffectiveToUtc == null
                    select o.WeeklyPeriods).ToArrayAsync(cancellationToken);

                var currentLoad = TeacherLoadCalculator.CurrentLoad(assignedPeriods);
                if (TeacherLoadCalculator.ExceedsMax(currentLoad, offering.WeeklyPeriods, profile.MaxWeeklyPeriods))
                {
                    throw new LoadExceededException(teacherProfileId, currentLoad, profile.MaxWeeklyPeriods);
                }
            }

            var assignment = new TeacherAssignment
            {
                AcademicYearId = offering.AcademicYearId,
                TeacherProfileId = teacherProfileId,
                CurriculumOfferingId = curriculumOfferingId,
                SectionId = sectionId,
                Role = role,
                EffectiveFromUtc = effectiveFromUtc,
            };
            _db.TeacherAssignments.Add(assignment);

            await _db.SaveChangesAsync(cancellationToken);
            return assignment;
        }
    }
}
