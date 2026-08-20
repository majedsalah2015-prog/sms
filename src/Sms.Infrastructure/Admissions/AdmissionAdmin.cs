using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Admissions;
using Sms.Application.Common.Exceptions;
using Sms.Application.Grades;
using Sms.Application.Numbering;
using Sms.Application.Sections;
using Sms.Application.Students;
using Sms.Domain.Admissions;
using Sms.Domain.Common;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;
using AdmissionApplication = Sms.Domain.Admissions.Application;

namespace Sms.Infrastructure.Admissions
{
    /// <summary>
    /// DefineCampaignAsync/SubmitApplicationAsync/ChangeStatusAsync/
    /// RecordAssessmentAsync/AddToWaitingListAsync are standalone — they
    /// save themselves. RegisterAsync is the exception: it composes
    /// IStudentAdmin + ISectionAdmin (both standalone-admin services in
    /// their own right) under one explicit transaction, so their internal
    /// SaveChangesAsync calls join it instead of each opening their own
    /// (per SmsDbContext's ownsTransaction ambient-transaction detection),
    /// giving BR-ADM-007's "one transaction" requirement.
    /// </summary>
    public class AdmissionAdmin : IAdmissionAdmin
    {
        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IStudentAdmin _studentAdmin;
        private readonly ISectionAdmin _sectionAdmin;

        public AdmissionAdmin(AppDbContext db, INumberIssuer numberIssuer, IStudentAdmin studentAdmin, ISectionAdmin sectionAdmin)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _studentAdmin = studentAdmin;
            _sectionAdmin = sectionAdmin;
        }

        public async Task<AdmissionCampaign> DefineCampaignAsync(
            int gradeYearProfileId, DateTime openDate, DateTime closeDate, bool requiresAssessment,
            decimal? applicationFeeAmount, CancellationToken cancellationToken = default)
        {
            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == gradeYearProfileId, cancellationToken);

            var campaign = new AdmissionCampaign
            {
                SchoolId = profile.SchoolId,
                AcademicYearId = profile.AcademicYearId,
                GradeYearProfileId = gradeYearProfileId,
                OpenDate = openDate,
                CloseDate = closeDate,
                RequiresAssessment = requiresAssessment,
                ApplicationFeeAmount = applicationFeeAmount,
                IsActive = true,
            };
            _db.AdmissionCampaigns.Add(campaign);

            await _db.SaveChangesAsync(cancellationToken);
            return campaign;
        }

        public async Task<AdmissionApplication> SubmitApplicationAsync(
            int campaignId, string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId, int? parentId = null, CancellationToken cancellationToken = default)
        {
            var campaign = await _db.AdmissionCampaigns.SingleAsync(c => c.Id == campaignId, cancellationToken);
            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == campaign.GradeYearProfileId, cancellationToken);

            if (profile.AgeCutoffDate.HasValue)
            {
                var eligible = AgeEligibilityEvaluator.IsEligible(
                    dateOfBirth, profile.AgeCutoffDate.Value, profile.MinAgeAtCutoff, profile.MaxAgeAtCutoff);
                if (!eligible)
                {
                    throw new AgeIneligibleException();
                }
            }

            if (parentId.HasValue)
            {
                // BR-ADM-002 "same applicant": same family (parent) + same child (first
                // name in either language + date of birth). Siblings/twins applying to
                // the same grade are legitimate and must not be blocked.
                var duplicate = await _db.Applications.FirstOrDefaultAsync(
                    a => a.CampaignId == campaignId && a.ParentId == parentId
                        && a.DateOfBirth == dateOfBirth
                        && (a.FirstNameAr == firstNameAr || a.FirstNameEn == firstNameEn)
                        && a.Status != ApplicationStatus.Rejected && a.Status != ApplicationStatus.Lapsed,
                    cancellationToken);
                if (duplicate != null)
                {
                    throw new DuplicateLiveApplicationException(duplicate.Id);
                }
            }

            var applicationNo = await _numberIssuer.IssueAsync("APP", cancellationToken);

            var application = new AdmissionApplication
            {
                SchoolId = campaign.SchoolId,
                AcademicYearId = campaign.AcademicYearId,
                CampaignId = campaignId,
                ApplicationNo = applicationNo,
                FirstNameAr = firstNameAr,
                FatherNameAr = fatherNameAr,
                GrandfatherNameAr = grandfatherNameAr,
                FamilyNameAr = familyNameAr,
                FirstNameEn = firstNameEn,
                FatherNameEn = fatherNameEn,
                GrandfatherNameEn = grandfatherNameEn,
                FamilyNameEn = familyNameEn,
                Gender = gender,
                DateOfBirth = dateOfBirth,
                NationalityLookupId = nationalityLookupId,
                ParentId = parentId,
                Status = ApplicationStatus.Draft,
            };
            _db.Applications.Add(application);

            await _db.SaveChangesAsync(cancellationToken);
            return application;
        }

        public async Task ChangeStatusAsync(int applicationId, ApplicationStatus newStatus, CancellationToken cancellationToken = default)
        {
            var application = await _db.Applications.SingleAsync(a => a.Id == applicationId, cancellationToken);
            if (!ApplicationStatusTransitions.CanTransition(application.Status, newStatus))
            {
                throw new InvalidApplicationStatusTransitionException(application.Status, newStatus);
            }

            application.Status = newStatus;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<ApplicationAssessment> RecordAssessmentAsync(
            int applicationId, decimal score, int assessedByUserId, string? notes = null, CancellationToken cancellationToken = default)
        {
            var application = await _db.Applications.SingleAsync(a => a.Id == applicationId, cancellationToken);

            var assessment = new ApplicationAssessment
            {
                SchoolId = application.SchoolId,
                ApplicationId = applicationId,
                Score = score,
                Notes = notes,
                AssessedByUserId = assessedByUserId,
                AssessedAtUtc = DateTime.UtcNow,
            };
            _db.ApplicationAssessments.Add(assessment);

            await _db.SaveChangesAsync(cancellationToken);
            return assessment;
        }

        public async Task<WaitingListEntry> AddToWaitingListAsync(int applicationId, int gradeYearProfileId, CancellationToken cancellationToken = default)
        {
            var application = await _db.Applications.SingleAsync(a => a.Id == applicationId, cancellationToken);

            var nextRank = await _db.WaitingListEntries
                .Where(w => w.GradeYearProfileId == gradeYearProfileId)
                .Select(w => (int?)w.OrderRank)
                .MaxAsync(cancellationToken) ?? 0;

            var entry = new WaitingListEntry
            {
                SchoolId = application.SchoolId,
                AcademicYearId = application.AcademicYearId,
                ApplicationId = applicationId,
                GradeYearProfileId = gradeYearProfileId,
                OrderRank = nextRank + 1,
            };
            _db.WaitingListEntries.Add(entry);

            await _db.SaveChangesAsync(cancellationToken);
            return entry;
        }

        public async Task<AdmissionCampaign> UpdateCampaignAsync(int campaignId, DateTime openDate, DateTime closeDate, bool requiresAssessment, decimal? applicationFeeAmount, CancellationToken cancellationToken = default)
        {
            if (closeDate < openDate)
            {
                throw new InvalidOperationException("Campaign close date must be on or after the open date (BR-ADM-001).");
            }

            var campaign = await _db.AdmissionCampaigns.SingleAsync(c => c.Id == campaignId, cancellationToken);
            campaign.OpenDate = openDate;
            campaign.CloseDate = closeDate;
            campaign.RequiresAssessment = requiresAssessment;
            campaign.ApplicationFeeAmount = applicationFeeAmount;
            await _db.SaveChangesAsync(cancellationToken);
            return campaign;
        }

        public async Task DeactivateCampaignAsync(int campaignId, CancellationToken cancellationToken = default)
        {
            var campaign = await _db.AdmissionCampaigns.SingleAsync(c => c.Id == campaignId, cancellationToken);
            var live = await _db.Applications.CountAsync(a => a.CampaignId == campaignId
                && a.Status != ApplicationStatus.Registered && a.Status != ApplicationStatus.Rejected && a.Status != ApplicationStatus.Lapsed, cancellationToken);
            if (live > 0)
            {
                throw new InvalidOperationException("Campaign still has " + live + " open application(s); decide them first (BR-ADM-005).");
            }

            campaign.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteCampaignAsync(int campaignId, CancellationToken cancellationToken = default)
        {
            var campaign = await _db.AdmissionCampaigns.SingleAsync(c => c.Id == campaignId, cancellationToken);

            // Hard delete: the campaign goes with every application filed against it (plus their
            // assessments / waiting-list rows). Students already registered from an application are
            // NOT touched — they live on in Students; only their admission trail is removed.
            var applications = await _db.Applications.Where(a => a.CampaignId == campaignId).ToListAsync(cancellationToken);
            var applicationIds = applications.Select(a => a.Id).ToList();
            _db.ApplicationAssessments.RemoveRange(await _db.ApplicationAssessments.Where(x => applicationIds.Contains(x.ApplicationId)).ToListAsync(cancellationToken));
            _db.WaitingListEntries.RemoveRange(await _db.WaitingListEntries.Where(x => applicationIds.Contains(x.ApplicationId)).ToListAsync(cancellationToken));
            _db.Applications.RemoveRange(applications);
            _db.AdmissionCampaigns.Remove(campaign);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Campaign cannot be deleted: other records still reference it (" + (ex.InnerException?.Message ?? ex.Message) + ").");
            }
        }

        public async Task DeleteApplicationAsync(int applicationId, CancellationToken cancellationToken = default)
        {
            var application = await _db.Applications.SingleAsync(a => a.Id == applicationId, cancellationToken);
            if (application.Status == ApplicationStatus.Registered || application.RegisteredStudentId != null)
            {
                throw new InvalidOperationException("A registered application is linked to a student record and cannot be deleted.");
            }

            _db.ApplicationAssessments.RemoveRange(await _db.ApplicationAssessments.Where(x => x.ApplicationId == applicationId).ToListAsync(cancellationToken));
            _db.WaitingListEntries.RemoveRange(await _db.WaitingListEntries.Where(x => x.ApplicationId == applicationId).ToListAsync(cancellationToken));
            _db.Applications.Remove(application);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Application cannot be deleted: other records still reference it (" + (ex.InnerException?.Message ?? ex.Message) + ").");
            }
        }

        public async Task RemoveFromWaitingListAsync(int waitingListEntryId, CancellationToken cancellationToken = default)
        {
            var entry = await _db.WaitingListEntries.SingleAsync(w => w.Id == waitingListEntryId, cancellationToken);
            var application = await _db.Applications.SingleAsync(a => a.Id == entry.ApplicationId, cancellationToken);
            _db.WaitingListEntries.Remove(entry);
            if (application.Status == ApplicationStatus.Waitlisted)
            {
                application.Status = ApplicationStatus.Lapsed;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<AdmissionApplication> UpdateApplicationAsync(
            int applicationId, string firstNameAr, string fatherNameAr, string grandfatherNameAr, string familyNameAr,
            string firstNameEn, string fatherNameEn, string grandfatherNameEn, string familyNameEn,
            Gender gender, DateTime dateOfBirth, int nationalityLookupId, int? parentId, CancellationToken cancellationToken = default)
        {
            var application = await _db.Applications.SingleAsync(a => a.Id == applicationId, cancellationToken);
            if (application.Status is not (ApplicationStatus.Draft or ApplicationStatus.Submitted or ApplicationStatus.UnderReview or ApplicationStatus.Recommended))
            {
                throw new InvalidApplicationStatusTransitionException(application.Status, application.Status);
            }

            var campaign = await _db.AdmissionCampaigns.AsNoTracking().SingleAsync(c => c.Id == application.CampaignId, cancellationToken);
            var profile = await _db.GradeYearProfiles.AsNoTracking().SingleAsync(p => p.Id == campaign.GradeYearProfileId, cancellationToken);
            if (profile.AgeCutoffDate.HasValue && !AgeEligibilityEvaluator.IsEligible(dateOfBirth, profile.AgeCutoffDate.Value, profile.MinAgeAtCutoff, profile.MaxAgeAtCutoff))
            {
                throw new AgeIneligibleException();
            }

            application.FirstNameAr = firstNameAr; application.FatherNameAr = fatherNameAr; application.GrandfatherNameAr = grandfatherNameAr; application.FamilyNameAr = familyNameAr;
            application.FirstNameEn = firstNameEn; application.FatherNameEn = fatherNameEn; application.GrandfatherNameEn = grandfatherNameEn; application.FamilyNameEn = familyNameEn;
            application.Gender = gender; application.DateOfBirth = dateOfBirth; application.NationalityLookupId = nationalityLookupId; application.ParentId = parentId;
            await _db.SaveChangesAsync(cancellationToken);
            return application;
        }

        public async Task OfferSeatAsync(int waitingListEntryId, DateTime offerExpiresAtUtc, CancellationToken cancellationToken = default)
        {
            var entry = await _db.WaitingListEntries.SingleAsync(w => w.Id == waitingListEntryId, cancellationToken);
            var application = await _db.Applications.SingleAsync(a => a.Id == entry.ApplicationId, cancellationToken);
            if (application.Status != ApplicationStatus.Waitlisted)
            {
                throw new InvalidApplicationStatusTransitionException(application.Status, ApplicationStatus.Approved);
            }

            entry.OfferedAtUtc = DateTime.UtcNow;
            entry.OfferExpiresAtUtc = offerExpiresAtUtc;
            entry.IsOfferAccepted = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task RespondToOfferAsync(int waitingListEntryId, bool accepted, CancellationToken cancellationToken = default)
        {
            var entry = await _db.WaitingListEntries.SingleAsync(w => w.Id == waitingListEntryId, cancellationToken);
            if (entry.OfferedAtUtc == null)
            {
                throw new InvalidOperationException("No seat has been offered to this waiting-list entry (BR-ADM-006).");
            }

            var application = await _db.Applications.SingleAsync(a => a.Id == entry.ApplicationId, cancellationToken);
            var target = accepted ? ApplicationStatus.Approved : ApplicationStatus.Lapsed;
            if (!ApplicationStatusTransitions.CanTransition(application.Status, target))
            {
                throw new InvalidApplicationStatusTransitionException(application.Status, target);
            }

            entry.IsOfferAccepted = accepted;
            application.Status = target;
            if (accepted)
            {
                application.RegistrationDeadlineUtc = entry.OfferExpiresAtUtc;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Student> RegisterAsync(
            int applicationId, int sectionId, DateTime enrollmentDate, int guardianRelationshipLookupId, CancellationToken cancellationToken = default)
        {
            var application = await _db.Applications.SingleAsync(a => a.Id == applicationId, cancellationToken);

            if (application.Status != ApplicationStatus.Approved)
            {
                throw new ApplicationNotReadyForRegistrationException($"status is '{application.Status}', not Approved");
            }

            if (application.ParentId == null)
            {
                throw new ApplicationNotReadyForRegistrationException("no parent linked to the application");
            }

            var section = await _db.Sections.SingleAsync(s => s.Id == sectionId, cancellationToken);

            using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            var student = await _studentAdmin.RegisterStudentAsync(
                application.FirstNameAr, application.FatherNameAr, application.GrandfatherNameAr, application.FamilyNameAr,
                application.FirstNameEn, application.FatherNameEn, application.GrandfatherNameEn, application.FamilyNameEn,
                application.Gender, application.DateOfBirth, application.NationalityLookupId,
                cancellationToken: cancellationToken);

            var enrollment = await _studentAdmin.EnrollAsync(
                student.Id, section.GradeYearProfileId, enrollmentDate, EnrollmentSourceType.Admission, cancellationToken);

            await _sectionAdmin.AssignMembershipAsync(sectionId, enrollment.Id, enrollmentDate, cancellationToken);

            await _studentAdmin.LinkGuardianAsync(
                student.Id, application.ParentId.Value, guardianRelationshipLookupId, isPrimaryContact: true,
                isFinanciallyResponsible: true, isPickupAuthorized: true, isPortalVisible: true,
                effectiveFromUtc: enrollmentDate, cancellationToken: cancellationToken);

            application.Status = ApplicationStatus.Registered;
            application.RegisteredStudentId = student.Id;
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return student;
        }
    }
}
