using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attendance;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.Learning;
using Sms.Application.Portal;
using Sms.Domain.Attachments;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.Learning;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Portal
{
    /// <summary>Read-only — never calls SaveChangesAsync. Reuses E-301's AttendancePercentageCalculator and E-303's StudentFinancialPositionCalculator rather than recomputing them (BR-ATD-009/BR-FEE-008's "single central computation" mandate).</summary>
    public class ParentPortalQuery : IParentPortalQuery
    {
        private readonly AppDbContext _db;
        private readonly IWorkingYearContext _workingYear;

        public ParentPortalQuery(AppDbContext db, IWorkingYearContext workingYear)
        {
            _db = db;
            _workingYear = workingYear;
        }

        public async Task<IReadOnlyList<PortalChildSummary>> GetVisibleChildrenAsync(int requestingUserAccountId, CancellationToken cancellationToken = default)
        {
            var visibleIds = await GetGuardianVisibleStudentIdsAsync(requestingUserAccountId, cancellationToken);
            if (visibleIds.Count == 0)
            {
                return Array.Empty<PortalChildSummary>();
            }

            var students = await _db.Students.Where(s => visibleIds.Contains(s.Id)).ToListAsync(cancellationToken);
            return students
                .Select(s => new PortalChildSummary { StudentId = s.Id, StudentNo = s.StudentNo, FirstNameAr = s.FirstNameAr, FirstNameEn = s.FirstNameEn })
                .ToList();
        }

        private async Task<IReadOnlyList<int>> GetGuardianVisibleStudentIdsAsync(int requestingUserAccountId, CancellationToken cancellationToken)
        {
            var parent = await _db.Parents.SingleOrDefaultAsync(p => p.UserAccountId == requestingUserAccountId, cancellationToken);
            if (parent == null)
            {
                return Array.Empty<int>();
            }

            var linkRows = await _db.StudentGuardianLinks
                .Where(l => l.ParentId == parent.Id)
                .Select(l => new { l.StudentId, l.IsPortalVisible, l.EffectiveToUtc })
                .ToListAsync(cancellationToken);

            var links = linkRows.Select(l => new GuardianVisibilityEvaluator.GuardianLink(l.StudentId, l.IsPortalVisible, l.EffectiveToUtc));
            return GuardianVisibilityEvaluator.GetVisibleStudentIds(links);
        }

        private async Task EnsureAccessAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken)
        {
            var student = await _db.Students.SingleAsync(s => s.Id == studentId, cancellationToken);
            var visibleIds = await GetGuardianVisibleStudentIdsAsync(requestingUserAccountId, cancellationToken);

            if (!PortalAccessEvaluator.CanView(studentId, student.UserAccountId, requestingUserAccountId, visibleIds))
            {
                throw new PortalAccessDeniedException(studentId);
            }
        }

        public async Task<PortalAttendanceSummary> GetAttendanceSummaryAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default)
        {
            await EnsureAccessAsync(requestingUserAccountId, studentId, cancellationToken);

            var enrollment = await _db.Enrollments
                .Where(e => e.StudentId == studentId && e.AcademicYearId == _workingYear.AcademicYearId)
                .SingleOrDefaultAsync(cancellationToken);
            if (enrollment == null)
            {
                return new PortalAttendanceSummary();
            }

            var statuses = await _db.AttendanceDays.Where(a => a.EnrollmentId == enrollment.Id).Select(a => a.Status).ToListAsync(cancellationToken);
            var scheduled = statuses.Count;
            var exempted = statuses.Count(PortalAttendanceClassifier.IsExempted);
            var absent = statuses.Count(PortalAttendanceClassifier.IsAbsent);

            return new PortalAttendanceSummary
            {
                ScheduledDays = scheduled,
                ExemptedDays = exempted,
                AbsentDays = absent,
                AttendancePercent = AttendancePercentageCalculator.Calculate(scheduled, exempted, absent),
            };
        }

        public async Task<IReadOnlyList<PortalResultSummary>> GetPublishedResultsAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default)
        {
            await EnsureAccessAsync(requestingUserAccountId, studentId, cancellationToken);

            var enrollment = await _db.Enrollments
                .Where(e => e.StudentId == studentId && e.AcademicYearId == _workingYear.AcademicYearId)
                .SingleOrDefaultAsync(cancellationToken);
            if (enrollment == null)
            {
                return Array.Empty<PortalResultSummary>();
            }

            // TermResult rows only ever exist once a Marksheet publishes (E-302) - BR-SEC-012's "published only" is satisfied by construction, no extra filter needed.
            var results = await _db.TermResults.Where(r => r.EnrollmentId == enrollment.Id).ToListAsync(cancellationToken);
            var bandIds = results.Where(r => r.ScaleBandId.HasValue).Select(r => r.ScaleBandId!.Value).Distinct().ToList();
            var bandCodes = await _db.ScaleBands.Where(b => bandIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.BandCode, cancellationToken);

            return results
                .Select(r => new PortalResultSummary
                {
                    CurriculumOfferingId = r.CurriculumOfferingId,
                    TermId = r.TermId,
                    ScorePercent = r.ScorePercent,
                    BandCode = r.ScaleBandId.HasValue && bandCodes.TryGetValue(r.ScaleBandId.Value, out var code) ? code : null,
                    PublishedAtUtc = r.PublishedAtUtc,
                })
                .ToList();
        }

        /// <summary>
        /// doc/Modules/37 §8.10. Work set to the section this student currently
        /// sits in, due date first.
        ///
        /// <para>
        /// The status filter comes from <see cref="HomeworkStatusTransitions.PortalVisibleStatuses"/>
        /// rather than being spelled here, so BR-LRN-003 has one definition. A
        /// draft is invisible by that rule, and so is withdrawn work — a family
        /// that saw a task yesterday finds it gone today only because the
        /// teacher withdrew it with a reason, never because a draft flickered.
        /// </para>
        /// </summary>
        public async Task<IReadOnlyList<PortalSetWork>> GetSetWorkAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default)
        {
            await EnsureAccessAsync(requestingUserAccountId, studentId, cancellationToken);

            var enrollment = await _db.Enrollments
                .Where(e => e.StudentId == studentId && e.AcademicYearId == _workingYear.AcademicYearId)
                .SingleOrDefaultAsync(cancellationToken);
            if (enrollment == null)
            {
                return Array.Empty<PortalSetWork>();
            }

            // The section the student sits in now — an ended membership sets no
            // work for them any more.
            var sectionId = await _db.SectionMemberships
                .Where(m => m.EnrollmentId == enrollment.Id && m.EffectiveToUtc == null)
                .Select(m => (int?)m.SectionId)
                .FirstOrDefaultAsync(cancellationToken);
            if (sectionId is null)
            {
                return Array.Empty<PortalSetWork>();
            }

            var visible = HomeworkStatusTransitions.PortalVisibleStatuses;
            var homework = await _db.Homeworks
                .Where(h => h.SectionId == sectionId && visible.Contains(h.Status))
                .OrderBy(h => h.DueDate)
                .ToListAsync(cancellationToken);
            if (homework.Count == 0)
            {
                return Array.Empty<PortalSetWork>();
            }

            // The subject name is a lookup, not a picker: work set against an
            // offering whose subject was later retired must still render, so the
            // soft-active filter is ignored for this read (SoftActiveLookupTests).
            var offeringIds = homework.Select(h => h.CurriculumOfferingId).Distinct().ToList();
            var offerings = await _db.CurriculumOfferings.IgnoreQueryFilters()
                .Where(o => offeringIds.Contains(o.Id))
                .Select(o => new { o.Id, o.SubjectId })
                .ToListAsync(cancellationToken);
            var subjectIds = offerings.Select(o => o.SubjectId).Distinct().ToList();
            var subjects = await _db.Subjects.IgnoreQueryFilters()
                .Where(s => subjectIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
            var subjectByOffering = offerings.ToDictionary(o => o.Id, o => o.SubjectId);

            return homework
                .Select(h =>
                {
                    LocalizedName? subjectName = null;
                    if (subjectByOffering.TryGetValue(h.CurriculumOfferingId, out var subjectId)
                        && subjects.TryGetValue(subjectId, out var found))
                    {
                        subjectName = found;
                    }

                    return new PortalSetWork
                    {
                        HomeworkId = h.Id,
                        TitleAr = h.TitleAr,
                        TitleEn = h.TitleEn,
                        InstructionsAr = h.InstructionsAr,
                        InstructionsEn = h.InstructionsEn,
                        SubjectNameAr = subjectName?.NameAr ?? string.Empty,
                        SubjectNameEn = subjectName?.NameEn ?? string.Empty,
                        DueDate = h.DueDate,
                        MaxMarks = h.MaxMarks,
                        LatePenaltyApplies = h.LatenessPolicy == LatenessPolicy.AcceptWithPenalty,
                        LatePenaltyPercent = h.LatePenaltyPercent,
                    };
                })
                .ToList();
        }

        public async Task<PortalFeePosition> GetFeePositionAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default)
        {
            await EnsureAccessAsync(requestingUserAccountId, studentId, cancellationToken);

            // Only Posted charges are ever queried here - BR-SEC-012's "posted invoices only" is satisfied by this filter, matching FeeAdmin.ComputeStudentPositionAsync's own scope.
            var chargeRows = await _db.Charges
                .Where(c => c.StudentId == studentId && c.Status == ChargeStatus.Posted)
                .Select(c => new { c.Id, c.ChargeNo, c.GrossAmount, c.PostedAtUtc })
                .ToListAsync(cancellationToken);
            var chargeIds = chargeRows.Select(c => c.Id).ToList();

            // EF Core's Sqlite provider can't translate Sum() over decimal to SQL - materialize then sum in memory (same fix as E-303's FeeAdmin/PaymentAdmin).
            var totalCharges = chargeRows.Sum(c => c.GrossAmount);
            var totalCreditNotes = (await _db.CreditNotes.Where(n => chargeIds.Contains(n.ChargeId)).Select(n => n.Amount).ToListAsync(cancellationToken)).Sum();
            var totalDiscounts = (await _db.DiscountDocuments.Where(d => chargeIds.Contains(d.ChargeId)).Select(d => d.Amount).ToListAsync(cancellationToken)).Sum();
            var totalAllocated = (await _db.PaymentAllocations.Where(a => chargeIds.Contains(a.ChargeId)).Select(a => a.AllocatedAmount).ToListAsync(cancellationToken)).Sum();

            return new PortalFeePosition
            {
                Position = StudentFinancialPositionCalculator.Calculate(totalCharges, totalCreditNotes, totalDiscounts, totalAllocated),
                GrossCharges = totalCharges,
                Discounts = totalDiscounts,
                Charges = chargeRows.Select(c => new PortalChargeLine { ChargeNo = c.ChargeNo, GrossAmount = c.GrossAmount, PostedAtUtc = c.PostedAtUtc }).ToList(),
            };
        }

        /// <summary>
        /// doc/Modules/37 §5. Content follows the <em>offering</em>, not the
        /// section: a lesson is planned once for the grade's subject and every
        /// section studying it reads the same plan, which is why this starts
        /// from the enrollment's <c>GradeYearProfileId</c> where
        /// <see cref="GetSetWorkAsync"/> starts from the section membership.
        /// Homework is set to a section; a syllabus is not.
        /// </summary>
        public async Task<IReadOnlyList<PortalLesson>> GetPublishedLessonsAsync(int requestingUserAccountId, int studentId, CancellationToken cancellationToken = default)
        {
            await EnsureAccessAsync(requestingUserAccountId, studentId, cancellationToken);

            var offerings = await OfferingsOfStudentAsync(studentId, cancellationToken);
            if (offerings.Count == 0)
            {
                return Array.Empty<PortalLesson>();
            }

            var offeringIds = offerings.Select(o => o.Id).ToList();

            // BR-LRN-003: Published only. Draft is invisible in the portal and
            // Retired has been taken back off the week with a reason - neither is
            // a lesson this family is being kept from, and the year filter is
            // explicit because IYearScoped carries no global filter.
            var lessons = await _db.Lessons
                .Where(l => offeringIds.Contains(l.CurriculumOfferingId)
                    && l.AcademicYearId == _workingYear.AcademicYearId
                    && l.Status == LessonStatus.Published)
                .OrderByDescending(l => l.WeekNumber).ThenBy(l => l.Id)
                .ToListAsync(cancellationToken);
            if (lessons.Count == 0)
            {
                return Array.Empty<PortalLesson>();
            }

            var lessonIds = lessons.Select(l => l.Id).ToList();

            // Withdrawn material is excluded by the soft-active filter on
            // LessonResource itself, so IsActive is deliberately not restated.
            var resources = await _db.LessonResources
                .Where(r => lessonIds.Contains(r.LessonId))
                .OrderBy(r => r.DisplayOrder).ThenBy(r => r.Id)
                .ToListAsync(cancellationToken);

            var servable = await ScanCleanAttachmentIdsAsync(
                resources.Select(r => r.AttachmentId).Distinct().ToList(), cancellationToken);

            // The subject name is a lookup, not a picker: a lesson taught under a
            // subject that was later retired must still render its name rather
            // than a blank (SoftActiveLookupTests).
            var subjectIds = offerings.Select(o => o.SubjectId).Distinct().ToList();
            var subjects = await _db.Subjects.IgnoreQueryFilters()
                .Where(s => subjectIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
            var subjectByOffering = offerings.ToDictionary(o => o.Id, o => o.SubjectId);

            return lessons
                .Select(l =>
                {
                    LocalizedName? subjectName = null;
                    if (subjectByOffering.TryGetValue(l.CurriculumOfferingId, out var subjectId)
                        && subjects.TryGetValue(subjectId, out var found))
                    {
                        subjectName = found;
                    }

                    return new PortalLesson
                    {
                        LessonId = l.Id,
                        WeekNumber = l.WeekNumber,
                        TitleAr = l.TitleAr,
                        TitleEn = l.TitleEn,
                        ObjectivesAr = l.ObjectivesAr,
                        ObjectivesEn = l.ObjectivesEn,
                        SubjectNameAr = subjectName?.NameAr ?? string.Empty,
                        SubjectNameEn = subjectName?.NameEn ?? string.Empty,
                        PublishedAtUtc = l.PublishedAtUtc,
                        Resources = resources
                            .Where(r => r.LessonId == l.Id && servable.Contains(r.AttachmentId))
                            .Select(r => new PortalLessonResource
                            {
                                ResourceId = r.Id,
                                TitleAr = r.TitleAr,
                                TitleEn = r.TitleEn,
                                DisplayOrder = r.DisplayOrder,
                            })
                            .ToList(),
                    };
                })
                .ToList();
        }

        /// <summary>
        /// BR-SEC-011 asked about a resource rather than a student. The download
        /// action is handed a resource id by the browser, so the gate has to run
        /// from that end: walk back to the lesson's offering, find which of the
        /// caller's students study it, and let the ordinary access evaluator
        /// answer. A caller with no student on that offering is refused exactly
        /// as they would be on the student's own page.
        /// </summary>
        public async Task<bool> CanReadLessonResourceAsync(int requestingUserAccountId, int resourceId, CancellationToken cancellationToken = default)
        {
            var resource = await _db.LessonResources
                .Where(r => r.Id == resourceId)
                .Select(r => new { r.LessonId, r.AttachmentId })
                .SingleOrDefaultAsync(cancellationToken);
            if (resource == null)
            {
                return false; // no row, or withdrawn - the soft-active filter answers both
            }

            // BR-LRN-003: material hanging off a lesson the school has not
            // published is not portal material, whatever its own state says.
            var offeringId = await _db.Lessons
                .Where(l => l.Id == resource.LessonId
                    && l.AcademicYearId == _workingYear.AcademicYearId
                    && l.Status == LessonStatus.Published)
                .Select(l => (int?)l.CurriculumOfferingId)
                .SingleOrDefaultAsync(cancellationToken);
            if (offeringId == null)
            {
                return false;
            }

            foreach (var studentId in await FamilyStudentIdsAsync(requestingUserAccountId, cancellationToken))
            {
                var offerings = await OfferingsOfStudentAsync(studentId, cancellationToken);
                if (offerings.Any(o => o.Id == offeringId.Value))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The offerings the student's grade studies this year. An end-dated
        /// offering (BR-SUB-004) is kept: its lessons stay readable, because
        /// content is never orphaned by a curriculum change (BR-LRN-001).
        /// </summary>
        private async Task<IReadOnlyList<OfferingRef>> OfferingsOfStudentAsync(int studentId, CancellationToken cancellationToken)
        {
            var profileId = await _db.Enrollments
                .Where(e => e.StudentId == studentId && e.AcademicYearId == _workingYear.AcademicYearId)
                .Select(e => (int?)e.GradeYearProfileId)
                .FirstOrDefaultAsync(cancellationToken);
            if (profileId == null)
            {
                return Array.Empty<OfferingRef>();
            }

            return await _db.CurriculumOfferings
                .Where(o => o.GradeYearProfileId == profileId.Value && o.AcademicYearId == _workingYear.AcademicYearId)
                .Select(o => new OfferingRef(o.Id, o.SubjectId))
                .ToListAsync(cancellationToken);
        }

        /// <summary>The caller's own student record, if they have one, plus every child they may see (BR-SEC-011).</summary>
        private async Task<IReadOnlyList<int>> FamilyStudentIdsAsync(int requestingUserAccountId, CancellationToken cancellationToken)
        {
            var ids = new List<int>();
            var self = await _db.Students
                .Where(s => s.UserAccountId == requestingUserAccountId)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (self != null)
            {
                ids.Add(self.Value);
            }

            ids.AddRange((await GetGuardianVisibleStudentIdsAsync(requestingUserAccountId, cancellationToken)).Where(id => id != self));
            return ids;
        }

        /// <summary>
        /// BR-LRN-006: the attachment ids whose current version has passed its
        /// virus scan. Everything else is neither listed nor served — the same
        /// gate <c>AttachmentIntake.ReadAsync</c> applies to the bytes, applied
        /// here to the row so the portal offers no link it would then refuse.
        /// </summary>
        private async Task<HashSet<int>> ScanCleanAttachmentIdsAsync(IReadOnlyList<int> attachmentIds, CancellationToken cancellationToken)
        {
            if (attachmentIds.Count == 0)
            {
                return new HashSet<int>();
            }

            var current = await _db.Attachments.IgnoreQueryFilters()
                .Where(a => attachmentIds.Contains(a.Id) && a.SchoolId == _db.CurrentSchoolId)
                .Select(a => new { a.Id, a.CurrentVersionNumber })
                .ToListAsync(cancellationToken);

            var versions = await _db.AttachmentVersions
                .Where(v => attachmentIds.Contains(v.AttachmentId))
                .Select(v => new { v.AttachmentId, v.VersionNumber, v.ScanStatus })
                .ToListAsync(cancellationToken);

            var clean = new HashSet<int>();
            foreach (var attachment in current)
            {
                // The current version is the one that would be served; a slot
                // whose pointer is not yet set falls back to the newest row, so a
                // half-written upload is judged on the version it does have
                // rather than skipped as if it were clean.
                var forAttachment = versions.Where(v => v.AttachmentId == attachment.Id).ToList();
                var version = forAttachment.FirstOrDefault(v => v.VersionNumber == attachment.CurrentVersionNumber)
                    ?? forAttachment.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
                if (version?.ScanStatus == ScanStatus.Clean)
                {
                    clean.Add(attachment.Id);
                }
            }

            return clean;
        }

        private sealed record OfferingRef(int Id, int SubjectId);
    }
}
