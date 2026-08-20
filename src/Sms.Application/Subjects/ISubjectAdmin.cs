using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Subjects;

namespace Sms.Application.Subjects
{
    /// <summary>doc/Modules/07 §8 "Curriculum plan editor"/"Qualification matrix" screens backing (screens deferred, the operations are core).</summary>
    public interface ISubjectAdmin
    {
        Task<Department> DefineDepartmentAsync(
            string nameAr, string nameEn, int? headTeacherUserId = null, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.DuplicateSubjectCodeException"/> on a repeated code.</summary>
        Task<Subject> DefineSubjectAsync(
            string code, string nameAr, string nameEn, string category, int? departmentId = null, CancellationToken cancellationToken = default);

        /// <summary>Edits a subject's code/names/category/department; throws <see cref="Common.Exceptions.DuplicateSubjectCodeException"/> on a repeated code.</summary>
        Task<Subject> UpdateSubjectAsync(
            int subjectId, string code, string nameAr, string nameEn, string category, int? departmentId = null, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes (deactivates) a subject; throws <see cref="Common.Exceptions.SubjectInUseException"/> while any current (not end-dated) curriculum offering references it. Ended offerings and qualifications stay as history.</summary>
        Task DeactivateSubjectAsync(int subjectId, CancellationToken cancellationToken = default);

        /// <summary>Edits a department's names/head teacher.</summary>
        Task<Department> UpdateDepartmentAsync(int departmentId, string nameAr, string nameEn, int? headTeacherUserId = null, CancellationToken cancellationToken = default);

        /// <summary>Soft-deletes (deactivates) a department; throws <see cref="Common.Exceptions.SubjectInUseException"/> while active subjects are assigned to it.</summary>
        Task DeactivateDepartmentAsync(int departmentId, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.DuplicateOfferingException"/> or <see cref="Common.Exceptions.InvalidOfferingWeightException"/>.</summary>
        Task<CurriculumOffering> DefineOfferingAsync(
            int gradeYearProfileId, int subjectId, int weeklyPeriods, bool isAssessable, decimal gpaWeight,
            bool isElective, string? electiveGroupTag, DateTime effectiveFromUtc, CancellationToken cancellationToken = default);

        /// <summary>BR-SUB-004: end-dates rather than removes — the offering stays referenceable by history.</summary>
        Task EndDateOfferingAsync(int offeringId, DateTime effectiveToUtc, CancellationToken cancellationToken = default);

        Task<TeacherSubjectQualification> DefineQualificationAsync(
            int teacherUserId, int subjectId, int? stageId, QualificationSource source, CancellationToken cancellationToken = default);
    }
}
