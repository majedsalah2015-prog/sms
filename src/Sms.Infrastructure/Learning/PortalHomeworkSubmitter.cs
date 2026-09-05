using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Learning;
using Sms.Domain.Learning;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Learning
{
    /// <summary>
    /// BR-LRN-013 (doc/Modules/37 §8.10) — the portal's first write.
    ///
    /// <para>
    /// Everything this class does about identity it does from the signed-in
    /// account outwards: account -> <c>ppl.Student</c> -> this year's
    /// <c>ppl.Enrollment</c> -> the section that enrollment currently sits in ->
    /// the homework set to it. The caller never names whose work it is. That is
    /// the whole enforcement of "a parent account may view but never submit on a
    /// child's behalf": a parent's account resolves to no student row, so the
    /// first step refuses, and no controller has to remember to check.
    /// </para>
    ///
    /// <para>
    /// Standalone shape, and explicitly transactional: BR-LRN-005's live row, the
    /// <see cref="SubmissionVersion"/> beneath it and that version's files are one
    /// hand-in and must land or fail together. A submission whose version never
    /// saved would be a row claiming work exists with nothing behind it.
    /// </para>
    /// </summary>
    public class PortalHomeworkSubmitter : IPortalHomeworkSubmitter
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;

        public PortalHomeworkSubmitter(AppDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<HomeworkSubmission> SubmitAsync(
            int requestingUserAccountId,
            int homeworkId,
            string? textResponse = null,
            IReadOnlyList<int>? attachmentIds = null,
            CancellationToken cancellationToken = default)
        {
            // BR-LRN-013: the submitting identity is the student's OWN account.
            // A parent, a teacher and an unlinked account all fail here — a
            // parent's account carries a ppl.Parent row and no ppl.Student one,
            // so "view but never submit" needs no separate check for it.
            var student = await _db.Students
                .Where(s => s.UserAccountId == requestingUserAccountId)
                .Select(s => new { s.Id })
                .SingleOrDefaultAsync(cancellationToken);

            if (student is null)
            {
                throw new PortalSubmissionIdentityException(requestingUserAccountId);
            }

            // Not-found and not-yours answer identically from here down
            // (BR-SEC-010): a probe that could tell them apart is a way to
            // enumerate the school's homework.
            var homework = await _db.Homeworks.SingleOrDefaultAsync(h => h.Id == homeworkId, cancellationToken);
            if (homework is null || !HomeworkStatusTransitions.IsVisibleToPortal(homework.Status))
            {
                throw new HomeworkNotOfferedToStudentException(homeworkId);
            }

            var enrollmentId = await _db.Enrollments
                .Where(e => e.StudentId == student.Id && e.AcademicYearId == homework.AcademicYearId)
                .Select(e => (int?)e.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (enrollmentId is null)
            {
                throw new HomeworkNotOfferedToStudentException(homeworkId);
            }

            // The section the student sits in NOW (BR-SCN-005/006: the open-ended
            // membership row). Work set to a class they have transferred out of is
            // not theirs to hand in, which matches what the portal read
            // (IParentPortalQuery.GetSetWorkAsync) already shows them.
            var sectionId = await _db.SectionMemberships
                .Where(m => m.EnrollmentId == enrollmentId && m.EffectiveToUtc == null)
                .Select(m => (int?)m.SectionId)
                .FirstOrDefaultAsync(cancellationToken);

            if (sectionId != homework.SectionId)
            {
                throw new HomeworkNotOfferedToStudentException(homeworkId);
            }

            // BR-LRN-005: NOT a lateness check. The door is closed by status
            // alone — marking has begun, the marks are Module 17's, or the work
            // was withdrawn. Late work is accepted and flagged, never refused.
            if (!HomeworkStatusTransitions.AcceptsSubmissions(homework.Status))
            {
                throw new HomeworkClosedToSubmissionsException(homeworkId, homework.Status);
            }

            var files = await ResolveAttachmentsAsync(attachmentIds, cancellationToken);

            var now = _clock.UtcNow;
            var isLate = SubmissionLatenessEvaluator.IsLate(now, homework.DueDate);

            using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            var submission = await _db.HomeworkSubmissions
                .SingleOrDefaultAsync(s => s.HomeworkId == homeworkId && s.EnrollmentId == enrollmentId, cancellationToken);

            if (submission is null)
            {
                submission = new HomeworkSubmission
                {
                    AcademicYearId = homework.AcademicYearId,
                    HomeworkId = homeworkId,
                    EnrollmentId = enrollmentId.Value,
                    SubmittedAtUtc = now,
                    IsLate = isLate,
                    Status = SubmissionStatus.Submitted,
                    VersionCount = 0,
                };
                _db.HomeworkSubmissions.Add(submission);
            }
            else
            {
                // BR-LRN-005: a resubmission SUPERSEDES. The live row moves to the
                // new hand-in; the old one survives as its version below.
                submission.SubmittedAtUtc = now;
                submission.IsLate = isLate;

                // Any mark already entered described work that has just been
                // replaced. Carrying it forward would release to Module 17 a mark
                // for something the teacher never saw, so it is cleared and
                // BR-LRN-011 holds the release until the new work is marked. The
                // feedback stays: the teacher's words about the earlier attempt
                // are still what they said, and they will overwrite them.
                submission.Score = null;
                submission.MarkedByUserAccountId = null;
                submission.MarkedAtUtc = null;
                submission.Status = SubmissionStatus.Submitted;
            }

            // The live row must exist before the append-only version can name it.
            // Also the point at which the unique index on (HomeworkId,
            // EnrollmentId) settles a race between two concurrent hand-ins —
            // BR-LRN-005's "one live submission" is enforced by the database, not
            // by the SingleOrDefault above.
            submission.VersionCount += 1;
            await _db.SaveChangesAsync(cancellationToken);

            var version = new SubmissionVersion
            {
                AcademicYearId = homework.AcademicYearId,
                HomeworkSubmissionId = submission.Id,
                VersionNumber = submission.VersionCount,
                TextResponse = textResponse,
                SubmittedAtUtc = now,
                IsLate = isLate,
            };
            _db.SubmissionVersions.Add(version);
            await _db.SaveChangesAsync(cancellationToken);

            if (files.Count > 0)
            {
                // Hung off the version, so a later resubmission leaves this
                // hand-in's files with this hand-in (BR-LRN-005).
                foreach (var attachmentId in files)
                {
                    _db.SubmissionAttachments.Add(new SubmissionAttachment
                    {
                        AcademicYearId = homework.AcademicYearId,
                        SubmissionVersionId = version.Id,
                        AttachmentId = attachmentId,
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);
            }

            // BR-LRN-003/§4: the first hand-in of the class is what moves the
            // homework off Issued. Collecting exists because withdrawal reads
            // differently once a student has handed something in (§9).
            if (homework.Status == HomeworkStatus.Issued)
            {
                homework.Status = HomeworkStatus.Collecting;
                await _db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return submission;
        }

        /// <summary>
        /// BR-LRN-006: uploads ride the existing attachment pipeline unchanged —
        /// these ids are <c>doc.Attachment</c> rows the caller already created
        /// through it. Checked for existence only: the typing and the size limit
        /// were applied where the file was accepted, and the virus scan gates
        /// serving rather than accepting, so an unscanned file is taken from the
        /// student and simply never shown to the teacher until it is clean.
        /// <para>
        /// Duplicates are collapsed rather than refused — the same file twice in
        /// one post is a double-click, and the unique index on the join would
        /// otherwise turn it into a raw <c>DbUpdateException</c> at the Web
        /// boundary.
        /// </para>
        /// </summary>
        private async Task<IReadOnlyList<int>> ResolveAttachmentsAsync(
            IReadOnlyList<int>? attachmentIds, CancellationToken cancellationToken)
        {
            if (attachmentIds is null || attachmentIds.Count == 0)
            {
                return new List<int>();
            }

            var wanted = attachmentIds.Distinct().ToList();
            var found = await _db.Attachments
                .Where(a => wanted.Contains(a.Id))
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);

            if (found.Count != wanted.Count)
            {
                var missing = wanted.Except(found);
                throw new System.ArgumentException(
                    $"Attachment(s) {string.Join(", ", missing)} do not exist in this school (BR-LRN-006).",
                    nameof(attachmentIds));
            }

            return found;
        }
    }
}
