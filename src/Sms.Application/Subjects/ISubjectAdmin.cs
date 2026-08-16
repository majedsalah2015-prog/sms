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
