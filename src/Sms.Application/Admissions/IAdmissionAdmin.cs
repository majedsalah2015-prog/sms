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

        /// <summary>Edits window / assessment / fee of a campaign; the grade-year cannot change once applications exist.</summary>
        Task<AdmissionCampaign> UpdateCampaignAsync(int campaignId, DateTime openDate, DateTime closeDate, bool requiresAssessment, decimal? applicationFeeAmount, CancellationToken cancellationToken = default);

        /// <summary>Soft-removes a campaign (IsActive = false); refused while any non-terminal application exists on it.</summary>
        Task DeactivateCampaignAsync(int campaignId, CancellationToken cancellationToken = default);

        /// <summary>Hard-deletes an application together with its assessments/waiting-list rows; refused once it is Registered (linked to a student record).</summary>
        Task DeleteApplicationAsync(int applicationId, CancellationToken cancellationToken = default);

        /// <summary>Removes a waiting-list entry; an application still Waitlisted moves to Lapsed (it is no longer queued for a seat).</summary>
        Task RemoveFromWaitingListAsync(int waitingListEntryId, CancellationToken cancellationToken = default);

        /// <summary>Corrects applicant identity fields before a decision (Draft/Submitted/UnderReview/Recommended); re-checks age eligibility.</summary>
        Task<AdmissionApplication> UpdateApplicationAsync(
            int applicationId, string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId, int? parentId, CancellationToken cancellationToken = default);

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

        /// <summary>BR-ADM-006 waitlist path: offers the seat to a Waitlisted entry with an expiry; throws <see cref="Common.Exceptions.InvalidApplicationStatusTransitionException"/> if the application is not Waitlisted.</summary>
        Task OfferSeatAsync(int waitingListEntryId, DateTime offerExpiresAtUtc, CancellationToken cancellationToken = default);

        /// <summary>BR-ADM-006: records the family's answer — accepted moves the application to Approved (registration deadline = offer expiry), declined/expired moves it to Lapsed.</summary>
        Task RespondToOfferAsync(int waitingListEntryId, bool accepted, CancellationToken cancellationToken = default);

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
