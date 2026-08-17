using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Certificates;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.Numbering;
using Sms.Domain.Certificates;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Certificates
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class CertificateAdmin : ICertificateAdmin
    {
        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;
        private readonly IFeeAdmin _feeAdmin;
        private readonly ITenantContext _tenant;
        private readonly IWorkingYearContext _workingYear;

        public CertificateAdmin(
            AppDbContext db, INumberIssuer numberIssuer, IClock clock, IAuditContext audit, IFeeAdmin feeAdmin,
            ITenantContext tenant, IWorkingYearContext workingYear)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
            _audit = audit;
            _feeAdmin = feeAdmin;
            _tenant = tenant;
            _workingYear = workingYear;
        }

        public async Task<CertificateType> DefineTypeAsync(
            CertificateKind kind, string nameAr, string nameEn, bool requiresPublishedResults, FeeClearanceRule feeClearanceRule, bool isPortalRequestable,
            int? validityDays = null, string numberingSeriesCode = "CERT", CancellationToken cancellationToken = default)
        {
            // BR-CRT-008: the country-pack legal gate is enforced at configuration time — a school can't
            // even define a fee-gated type for a kind the pack says may not be withheld (KSA-01: TC).
            if (feeClearanceRule != FeeClearanceRule.Disabled && !CertificateWithholdingPolicy.MayBeGatedForFees(kind))
            {
                throw new CertificateKindNotGateableException(kind);
            }

            // Charge has no due date yet, so "no overdue" is undefined — refuse rather than silently
            // evaluate it as full clearance (stricter) or disabled (weaker).
            if (feeClearanceRule == FeeClearanceRule.NoOverdue)
            {
                throw new FeeClearanceRuleNotSupportedException(feeClearanceRule);
            }

            var type = new CertificateType
            {
                Kind = kind, NameAr = nameAr, NameEn = nameEn, RequiresPublishedResults = requiresPublishedResults,
                FeeClearanceRule = feeClearanceRule, IsPortalRequestable = isPortalRequestable,
                ValidityDays = validityDays, NumberingSeriesCode = numberingSeriesCode,
            };
            _db.CertificateTypes.Add(type);
            await _db.SaveChangesAsync(cancellationToken);
            return type;
        }

        public async Task<CertificateRequest> RequestAsync(
            int certificateTypeId, int studentId, int requestedByUserId, CancellationToken cancellationToken = default)
        {
            var request = new CertificateRequest
            {
                CertificateTypeId = certificateTypeId, StudentId = studentId, RequestedByUserId = requestedByUserId,
                RequestedAtUtc = _clock.UtcNow,
            };
            _db.CertificateRequests.Add(request);
            await _db.SaveChangesAsync(cancellationToken);
            return request;
        }

        private async Task<bool> HasPublishedResultsAsync(int studentId, CancellationToken cancellationToken)
        {
            var enrollment = await _db.Enrollments
                .Where(e => e.StudentId == studentId && e.AcademicYearId == _workingYear.AcademicYearId)
                .SingleOrDefaultAsync(cancellationToken);
            if (enrollment == null)
            {
                return false;
            }

            return await _db.TermResults.AnyAsync(r => r.EnrollmentId == enrollment.Id, cancellationToken);
        }

        public async Task ApproveAsync(int certificateRequestId, string? clearanceOverrideReason = null, CancellationToken cancellationToken = default)
        {
            var request = await _db.CertificateRequests.SingleAsync(r => r.Id == certificateRequestId, cancellationToken);
            if (!CertificateRequestStatusTransitions.CanTransition(request.Status, CertificateRequestStatus.Approved))
            {
                throw new InvalidCertificateRequestStatusTransitionException(request.Status, CertificateRequestStatus.Approved);
            }

            var type = await _db.CertificateTypes.SingleAsync(t => t.Id == request.CertificateTypeId, cancellationToken);

            // Published-results prerequisite is never overridable (BR-CRT-003 hard check; the override in
            // BR-CRT-008 is scoped to the clearance gate only).
            var hasPublishedResults = await HasPublishedResultsAsync(request.StudentId, cancellationToken);
            if (!CertificatePrerequisiteEvaluator.AreMet(type.RequiresPublishedResults, hasPublishedResults, requiresFeeClearance: false, isFeeClear: true))
            {
                throw new CertificatePrerequisitesNotMetException(certificateRequestId);
            }

            if (type.FeeClearanceRule != FeeClearanceRule.Disabled)
            {
                var position = await _feeAdmin.ComputeStudentPositionAsync(request.StudentId, cancellationToken);
                // No due-date data exists, so the overdue slice is unknown; DefineTypeAsync already refuses
                // NoOverdue, so only FullClearance reaches here and the overdue argument is never consulted.
                var isFeeClear = FeeClearanceRuleEvaluator.IsClear(type.FeeClearanceRule, position, overduePosition: position);
                if (!isFeeClear)
                {
                    if (string.IsNullOrWhiteSpace(clearanceOverrideReason))
                    {
                        throw new CertificateFeeClearanceBlockedException(certificateRequestId, position);
                    }

                    // BR-CRT-008 Principal override: T1 + reason. CertificateRequest.ClearanceOverridden is
                    // [RequiresAuditReason], so the ambient reason must be set before this save or the
                    // audit captor throws MissingAuditReasonException.
                    _audit.Reason = clearanceOverrideReason;
                    request.ClearanceOverridden = true;
                    request.ClearanceOverrideReason = clearanceOverrideReason;
                }
            }

            request.Status = CertificateRequestStatus.Approved;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task RejectAsync(int certificateRequestId, string reason, CancellationToken cancellationToken = default)
        {
            var request = await _db.CertificateRequests.SingleAsync(r => r.Id == certificateRequestId, cancellationToken);
            if (!CertificateRequestStatusTransitions.CanTransition(request.Status, CertificateRequestStatus.Rejected))
            {
                throw new InvalidCertificateRequestStatusTransitionException(request.Status, CertificateRequestStatus.Rejected);
            }

            request.Status = CertificateRequestStatus.Rejected;
            request.RejectionReason = reason;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public Task<CertificateIssue> IssueAsync(int certificateRequestId, CancellationToken cancellationToken = default)
            => IssueCoreAsync(certificateRequestId, reissuedFromCertificateIssueId: null, cancellationToken);

        private async Task<CertificateIssue> IssueCoreAsync(int certificateRequestId, int? reissuedFromCertificateIssueId, CancellationToken cancellationToken)
        {
            var request = await _db.CertificateRequests.SingleAsync(r => r.Id == certificateRequestId, cancellationToken);
            if (!CertificateRequestStatusTransitions.CanTransition(request.Status, CertificateRequestStatus.Issued))
            {
                throw new InvalidCertificateRequestStatusTransitionException(request.Status, CertificateRequestStatus.Issued);
            }

            var type = await _db.CertificateTypes.SingleAsync(t => t.Id == request.CertificateTypeId, cancellationToken);
            var student = await _db.Students.SingleAsync(s => s.Id == request.StudentId, cancellationToken);
            var school = await _db.Schools.SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId, cancellationToken);

            var certificateNo = await _numberIssuer.IssueAsync(type.NumberingSeriesCode, cancellationToken);
            var issuedAtUtc = _clock.UtcNow;
            var expiresAtUtc = type.ValidityDays.HasValue ? issuedAtUtc.AddDays(type.ValidityDays.Value) : (DateTime?)null;
            var verificationCode = CertificateVerificationCodeBuilder.Build(certificateNo, issuedAtUtc);

            // BR-CRT-004: student identity + school identity (BR-SCH-002) frozen at issuance, bilingual.
            // Results data (for transcripts) would join here once BR-GRA-009 transcript composition exists.
            var snapshot = JsonSerializer.Serialize(new
            {
                CertificateNo = certificateNo,
                student.StudentNo,
                student.FirstNameAr,
                student.FatherNameAr,
                student.GrandfatherNameAr,
                student.FamilyNameAr,
                student.FirstNameEn,
                student.FatherNameEn,
                student.GrandfatherNameEn,
                student.FamilyNameEn,
                TypeNameAr = type.NameAr,
                TypeNameEn = type.NameEn,
                SchoolNameAr = school?.NameAr,
                SchoolNameEn = school?.NameEn,
                SchoolLicenseNumber = school?.LicenseNumber,
                SchoolMinistryCode = school?.MinistryCode,
                IssuedAtUtc = issuedAtUtc,
                ExpiresAtUtc = expiresAtUtc,
            });

            var issue = new CertificateIssue
            {
                CertificateRequestId = certificateRequestId,
                CertificateTypeId = type.Id,
                StudentId = student.Id,
                CertificateNo = certificateNo,
                DataSnapshotJson = snapshot,
                VerificationCode = verificationCode,
                Status = CertificateIssueStatus.Issued,
                IssuedAtUtc = issuedAtUtc,
                ExpiresAtUtc = expiresAtUtc,
                ReissuedFromCertificateIssueId = reissuedFromCertificateIssueId,
            };
            _db.CertificateIssues.Add(issue);

            request.Status = CertificateRequestStatus.Issued;
            await _db.SaveChangesAsync(cancellationToken);
            return issue;
        }

        public async Task<CertificateIssue> ReissueAsync(int certificateIssueId, string? revokeOriginalReason = null, CancellationToken cancellationToken = default)
        {
            var original = await _db.CertificateIssues.SingleAsync(i => i.Id == certificateIssueId, cancellationToken);
            var originalRequest = await _db.CertificateRequests.SingleAsync(r => r.Id == original.CertificateRequestId, cancellationToken);

            // BR-CRT-004: a reissue is a brand-new certificate — its own request, re-checked prerequisites,
            // new number, fresh snapshot. The original stays in the register (BR-NUM-002) and is only
            // revoked when the caller says so.
            var request = await RequestAsync(original.CertificateTypeId, original.StudentId, originalRequest.RequestedByUserId, cancellationToken);
            await ApproveAsync(request.Id, clearanceOverrideReason: null, cancellationToken);
            var reissue = await IssueCoreAsync(request.Id, original.Id, cancellationToken);

            if (!string.IsNullOrWhiteSpace(revokeOriginalReason) && original.Status == CertificateIssueStatus.Issued)
            {
                await RevokeAsync(original.Id, revokeOriginalReason, cancellationToken);
            }

            return reissue;
        }

        public async Task<CertificateBatchResult> IssueBatchAsync(int certificateTypeId, int gradeYearProfileId, int requestedByUserId, CancellationToken cancellationToken = default)
        {
            var result = new CertificateBatchResult();
            var studentIds = await _db.Enrollments
                .Where(e => e.GradeYearProfileId == gradeYearProfileId && e.AcademicYearId == _workingYear.AcademicYearId && e.Status == EnrollmentStatus.Active)
                .OrderBy(e => e.Id)
                .Select(e => e.StudentId)
                .ToListAsync(cancellationToken);

            // BR-CRT-009: the batch call IS the single approval covering the enumerated list; each member
            // still runs the prerequisite auto-check and gets its own number. Failures don't abort the
            // batch — they go to the exceptions queue (the request row stays Requested for follow-up).
            foreach (var studentId in studentIds)
            {
                var request = await RequestAsync(certificateTypeId, studentId, requestedByUserId, cancellationToken);
                try
                {
                    await ApproveAsync(request.Id, clearanceOverrideReason: null, cancellationToken);
                }
                catch (CertificatePrerequisitesNotMetException ex)
                {
                    result.Exceptions.Add(new CertificateBatchException(studentId, request.Id, ex.Message));
                    continue;
                }

                result.Issued.Add(await IssueCoreAsync(request.Id, reissuedFromCertificateIssueId: null, cancellationToken));
            }

            return result;
        }

        public async Task RevokeAsync(int certificateIssueId, string reason, CancellationToken cancellationToken = default)
        {
            var issue = await _db.CertificateIssues.SingleAsync(i => i.Id == certificateIssueId, cancellationToken);
            if (issue.Status != CertificateIssueStatus.Issued)
            {
                throw new CertificateNotIssuedException(certificateIssueId);
            }

            _audit.Reason = reason;
            issue.Status = CertificateIssueStatus.Revoked;
            issue.RevokedAtUtc = _clock.UtcNow;
            issue.RevokedReason = reason;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<CertificateIssue> ReprintAsync(int certificateIssueId, CancellationToken cancellationToken = default)
        {
            var issue = await _db.CertificateIssues.SingleAsync(i => i.Id == certificateIssueId, cancellationToken);
            issue.ReprintCount++;
            await _db.SaveChangesAsync(cancellationToken);
            return issue;
        }

        public async Task<CertificateIssue?> VerifyAsync(string verificationCode, CancellationToken cancellationToken = default)
        {
            var issue = await _db.CertificateIssues.SingleOrDefaultAsync(i => i.VerificationCode == verificationCode, cancellationToken);

            _db.VerificationLogs.Add(new VerificationLog
            {
                CertificateIssueId = issue?.Id, SubmittedCode = verificationCode, WasFound = issue != null, VerifiedAtUtc = _clock.UtcNow,
            });
            await _db.SaveChangesAsync(cancellationToken);

            return issue;
        }
    }
}
