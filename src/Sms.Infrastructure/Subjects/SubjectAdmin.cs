using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Subjects;
using Sms.Domain.Common;
using Sms.Domain.Subjects;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Subjects
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class SubjectAdmin : ISubjectAdmin
    {
        private readonly AppDbContext _db;

        public SubjectAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Department> DefineDepartmentAsync(
            string nameAr, string nameEn, int? headTeacherUserId = null, CancellationToken cancellationToken = default)
        {
            var department = new Department { Name = new LocalizedName(nameAr, nameEn), HeadTeacherUserId = headTeacherUserId };
            _db.Departments.Add(department);

            await _db.SaveChangesAsync(cancellationToken);
            return department;
        }

        public async Task<Subject> DefineSubjectAsync(
            string code, string nameAr, string nameEn, string category, int? departmentId = null, CancellationToken cancellationToken = default)
        {
            var codeTaken = await _db.Subjects.AnyAsync(s => s.Code == code, cancellationToken);
            if (codeTaken)
            {
                throw new DuplicateSubjectCodeException(code);
            }

            var subject = new Subject
            {
                Code = code,
                Name = new LocalizedName(nameAr, nameEn),
                Category = category,
                DepartmentId = departmentId,
            };
            _db.Subjects.Add(subject);

            await _db.SaveChangesAsync(cancellationToken);
            return subject;
        }

        public async Task<CurriculumOffering> DefineOfferingAsync(
            int gradeYearProfileId, int subjectId, int weeklyPeriods, bool isAssessable, decimal gpaWeight,
            bool isElective, string? electiveGroupTag, DateTime effectiveFromUtc, CancellationToken cancellationToken = default)
        {
            if (!CurriculumPlanValidator.HasValidWeight(isAssessable, gpaWeight))
            {
                throw new InvalidOfferingWeightException();
            }

            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == gradeYearProfileId, cancellationToken);

            var currentExists = await _db.CurriculumOfferings.AnyAsync(
                o => o.GradeYearProfileId == gradeYearProfileId && o.SubjectId == subjectId && o.EffectiveToUtc == null, cancellationToken);
            if (currentExists)
            {
                throw new DuplicateOfferingException(gradeYearProfileId, subjectId);
            }

            var offering = new CurriculumOffering
            {
                AcademicYearId = profile.AcademicYearId,
                GradeYearProfileId = gradeYearProfileId,
                SubjectId = subjectId,
                WeeklyPeriods = weeklyPeriods,
                IsAssessable = isAssessable,
                GpaWeight = gpaWeight,
                IsElective = isElective,
                ElectiveGroupTag = electiveGroupTag,
                EffectiveFromUtc = effectiveFromUtc,
            };
            _db.CurriculumOfferings.Add(offering);

            await _db.SaveChangesAsync(cancellationToken);
            return offering;
        }

        public async Task EndDateOfferingAsync(int offeringId, DateTime effectiveToUtc, CancellationToken cancellationToken = default)
        {
            var offering = await _db.CurriculumOfferings.SingleAsync(o => o.Id == offeringId, cancellationToken);
            offering.EffectiveToUtc = effectiveToUtc;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<TeacherSubjectQualification> DefineQualificationAsync(
            int teacherUserId, int subjectId, int? stageId, QualificationSource source, CancellationToken cancellationToken = default)
        {
            var qualification = new TeacherSubjectQualification
            {
                TeacherUserId = teacherUserId,
                SubjectId = subjectId,
                StageId = stageId,
                Source = source,
            };
            _db.TeacherSubjectQualifications.Add(qualification);

            await _db.SaveChangesAsync(cancellationToken);
            return qualification;
        }
    }
}
