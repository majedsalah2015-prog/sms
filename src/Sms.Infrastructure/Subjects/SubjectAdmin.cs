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

        public async Task<Subject> UpdateSubjectAsync(
            int subjectId, string code, string nameAr, string nameEn, string category, int? departmentId = null, CancellationToken cancellationToken = default)
        {
            var subject = await _db.Subjects.SingleAsync(s => s.Id == subjectId, cancellationToken);
            if (await _db.Subjects.AnyAsync(s => s.Code == code && s.Id != subjectId, cancellationToken))
            {
                throw new DuplicateSubjectCodeException(code);
            }

            subject.Code = code;
            subject.Name = new LocalizedName(nameAr, nameEn);
            subject.Category = category;
            subject.DepartmentId = departmentId;
            await _db.SaveChangesAsync(cancellationToken);
            return subject;
        }

        public async Task DeactivateSubjectAsync(int subjectId, CancellationToken cancellationToken = default)
        {
            var subject = await _db.Subjects.SingleAsync(s => s.Id == subjectId, cancellationToken);
            var current = await _db.CurriculumOfferings.CountAsync(o => o.SubjectId == subjectId && o.EffectiveToUtc == null, cancellationToken);
            if (current > 0)
            {
                throw new SubjectInUseException($"subject '{subject.Code}' is in {current} current curriculum plan(s) — end-date those offerings first (BR-SUB-004)");
            }

            subject.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Department> UpdateDepartmentAsync(int departmentId, string nameAr, string nameEn, int? headTeacherUserId = null, CancellationToken cancellationToken = default)
        {
            var department = await _db.Departments.SingleAsync(d => d.Id == departmentId, cancellationToken);
            department.Name = new LocalizedName(nameAr, nameEn);
            department.HeadTeacherUserId = headTeacherUserId;
            await _db.SaveChangesAsync(cancellationToken);
            return department;
        }

        public async Task DeactivateDepartmentAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            var department = await _db.Departments.SingleAsync(d => d.Id == departmentId, cancellationToken);
            var subjects = await _db.Subjects.CountAsync(s => s.DepartmentId == departmentId, cancellationToken);
            if (subjects > 0)
            {
                throw new SubjectInUseException($"{subjects} active subject(s) are assigned to this department — move them first");
            }

            department.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
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
