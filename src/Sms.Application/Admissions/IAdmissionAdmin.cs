using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Admissions;
using Sms.Domain.Common;
using Sms.Domain.Students;
using AdmissionApplication = Sms.Domain.Admissions.Application;

namespace Sms.Application.Admissions
{
    /// <summary>doc/Modules/09 §8 pipeline-board/registration-wizard screens backing (screens deferred, the operations are core).</summary>
    public interface IAdmissionAdmin
    {
        Task<AdmissionCampaign> DefineCampaignAsync(
            int gradeYearProfileId, DateTime openDate, DateTime closeDate, bool requiresAssessment,
            decimal? applicationFeeAmount, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.DuplicateLiveApplicationException"/> or <see cref="Common.Exceptions.AgeIneligibleException"/>.</summary>
        Task<AdmissionApplication> SubmitApplicationAsync(
            int campaignId, string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId, int? parentId = null, CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidApplicationStatusTransitionException"/>.</summary>
        Task ChangeStatusAsync(int applicationId, ApplicationStatus newStatus, CancellationToken cancellationToken = default);

        Task<ApplicationAssessment> RecordAssessmentAsync(
            int applicationId, decimal score, int assessedByUserId, string? notes = null, CancellationToken cancellationToken = default);

        Task<WaitingListEntry> AddToWaitingListAsync(int applicationId, int gradeYearProfileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-ADM-007: one transaction — creates the Student (via
        /// IStudentAdmin, issuing its permanent number), enrolls, assigns
        /// the section (via ISectionAdmin), and links the parent if one is
        /// set on the application. Fee generation (Module 19) and portal
        /// provisioning are deferred — neither module exists yet.
        /// </summary>
        Task<Student> RegisterAsync(
            int applicationId, int sectionId, DateTime enrollmentDate, int guardianRelationshipLookupId, CancellationToken cancellationToken = default);
    }
}
