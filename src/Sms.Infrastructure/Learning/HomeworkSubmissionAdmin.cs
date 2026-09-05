using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Grading;
using Sms.Application.Learning;
using Sms.Application.Notifications;
using Sms.Domain.Grading;
using Sms.Domain.Learning;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Learning
{
    /// <summary>
    /// doc/Modules/37 §8.4 (submission tracker) and §8.5 (marking queue).
    /// Standalone shape: each method saves itself.
    ///
    /// <para>
    /// BR-LRN-002 reach is resolved through <see cref="IHomeworkAdmin.ReachableSectionsAsync"/>
    /// rather than re-derived here, so the desk that offered the homework and
    /// the queue that marks it can never disagree about who reaches what — and
    /// so head-of-department reach does not have to be spelled twice and drift.
    /// </para>
    ///
    /// <para>
    /// BR-LRN-012's handoff goes through <see cref="IGradingAdmin.EnterMarkAsync"/>
    /// and nothing else. This module computes no grade, writes no
    /// <c>MarkEntry</c> of its own, and keeps no second copy of a mark: §1's
    /// design centre is that an LMS which quietly re-computes a grade is how a
    /// school ends up with two report cards that disagree.
    /// </para>
    /// </summary>
    public class HomeworkSubmissionAdmin : IHomeworkSubmissionAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly ICurrentUser _user;
        private readonly IHomeworkAdmin _homeworkAdmin;
        private readonly IGradingAdmin _grading;
        private readonly INotificationPublisher _notifications;

        public HomeworkSubmissionAdmin(
            AppDbContext db,
            IClock clock,
            ICurrentUser user,
            IHomeworkAdmin homeworkAdmin,
            IGradingAdmin grading,
            INotificationPublisher notifications)
        {
            _db = db;
            _clock = clock;
            _user = user;
            _homeworkAdmin = homeworkAdmin;
            _grading = grading;
            _notifications = notifications;
        }

        /// <summary>doc/Modules/37 §12, catalogued in <c>NotificationEventCatalog</c> under module LRN.</summary>
        private const string OverdueEventCode = "HomeworkOverdue";

        private const string MarkReleasedEventCode = "MarkReleased";

        public async Task<IReadOnlyList<HomeworkRosterRow>> RosterAsync(
            int homeworkId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var homework = await _db.Homeworks.SingleAsync(h => h.Id == homeworkId, cancellationToken);
            await GuardReachAsync(homework, hasSchoolWideReach, cancellationToken);

            // §8.4: the roster is the SECTION, not the submissions. A student who
            // handed nothing in is the row the teacher opened this screen to
            // find, and starting from HomeworkSubmission would omit exactly them.
            // Current membership only (BR-SCN-005/006: EffectiveToUtc null) — a
            // student who transferred out in March is not chased for work set to
            // a class they no longer sit in.
            var enrollmentIds = await _db.SectionMemberships
                .Where(m => m.SectionId == homework.SectionId
                    && m.AcademicYearId == homework.AcademicYearId
                    && m.EffectiveToUtc == null)
                .Select(m => m.EnrollmentId)
                .ToListAsync(cancellationToken);

            if (enrollmentIds.Count == 0)
            {
                return new List<HomeworkRosterRow>();
            }

            var enrollments = await _db.Enrollments
                .Where(e => enrollmentIds.Contains(e.Id))
                .Select(e => new { e.Id, e.StudentId })
                .ToListAsync(cancellationToken);

            // The student row is a LOOKUP here, not a picker: the section says who
            // is in this class, and a student deactivated mid-year must still
            // appear on the roster of work they were set. Filtering them out is
            // how a tracker silently loses a row (SoftActiveLookupTests' family of
            // failures) — so the filter is ignored for the name resolution while
            // the membership query above stays the authoritative list.
            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
            var students = await _db.Students.IgnoreQueryFilters()
                .Where(s => s.SchoolId == _db.CurrentSchoolId && studentIds.Contains(s.Id))
                .Select(s => new
                {
                    s.Id,
                    s.StudentNo,
                    s.FirstNameAr, s.FatherNameAr, s.FamilyNameAr,
                    s.FirstNameEn, s.FatherNameEn, s.FamilyNameEn,
                })
                .ToListAsync(cancellationToken);
            var studentById = students.ToDictionary(s => s.Id);

            var submissions = await _db.HomeworkSubmissions
                .Where(s => s.HomeworkId == homeworkId)
                .ToListAsync(cancellationToken);
            var submissionByEnrollment = submissions.ToDictionary(s => s.EnrollmentId);

            var penaltyPercent = homework.LatenessPolicy == LatenessPolicy.AcceptWithPenalty
                ? homework.LatePenaltyPercent
                : null;

            var rows = new List<HomeworkRosterRow>(enrollments.Count);
            foreach (var enrollment in enrollments)
            {
                if (!studentById.TryGetValue(enrollment.StudentId, out var student))
                {
                    // An enrollment whose student row is gone is a defect
                    // elsewhere; skipping it keeps the tracker usable rather than
                    // throwing the whole screen away over one broken link.
                    continue;
                }

                var row = new HomeworkRosterRow
                {
                    EnrollmentId = enrollment.Id,
                    StudentId = enrollment.StudentId,
                    StudentNo = student.StudentNo,
                    StudentNameAr = JoinName(student.FirstNameAr, student.FatherNameAr, student.FamilyNameAr),
                    StudentNameEn = JoinName(student.FirstNameEn, student.FatherNameEn, student.FamilyNameEn),
                    LatePenaltyPercent = penaltyPercent,
                };

                if (submissionByEnrollment.TryGetValue(enrollment.Id, out var submission))
                {
                    row.SubmissionId = submission.Id;
                    row.SubmittedAtUtc = submission.SubmittedAtUtc;
                    row.IsLate = submission.IsLate;
                    row.Score = submission.Score;
                    row.Feedback = submission.Feedback;
                    row.Status = submission.Status;
                    row.VersionCount = submission.VersionCount;
                }

                rows.Add(row);
            }

            // Ordered by the student number rather than by name: a roster sorted
            // by name has to pick a language, and this layer has none. The number
            // reads the same in both directions and the screen re-sorts by
            // whichever name it is displaying.
            return rows.OrderBy(r => r.StudentNo).ToList();
        }

        public async Task BeginMarkingAsync(
            int homeworkId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var homework = await _db.Homeworks.SingleAsync(h => h.Id == homeworkId, cancellationToken);
            await GuardReachAsync(homework, hasSchoolWideReach, cancellationToken);

            if (!HomeworkStatusTransitions.CanTransition(homework.Status, HomeworkStatus.Marking))
            {
                throw new HomeworkTransitionException(homeworkId, homework.Status, HomeworkStatus.Marking);
            }

            homework.Status = HomeworkStatus.Marking;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ScoreAsync(
            int submissionId,
            decimal? score,
            string? feedback,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var submission = await _db.HomeworkSubmissions.SingleAsync(s => s.Id == submissionId, cancellationToken);
            var homework = await _db.Homeworks.SingleAsync(h => h.Id == submission.HomeworkId, cancellationToken);
            await GuardReachAsync(homework, hasSchoolWideReach, cancellationToken);

            // BR-LRN-012: once released the mark is Module 17's, and a correction
            // there is a mark change under its change control — not a re-mark
            // here. A withdrawn homework has no mark to give.
            if (homework.Status is HomeworkStatus.Released or HomeworkStatus.Withdrawn)
            {
                throw new SubmissionMarkingClosedException(submissionId, homework.Status);
            }

            if (score is { } entered)
            {
                // BR-LRN-004: ungraded practice has no scale to be out of, so a
                // score against it is not "too high" — it is meaningless. Feedback
                // with no score is the intended path there.
                if (!HomeworkIssueGate.IsGraded(homework.MaxMarks))
                {
                    throw new SubmissionScoreOutOfRangeException(submissionId, entered, null);
                }

                if (entered < 0m || entered > homework.MaxMarks!.Value)
                {
                    throw new SubmissionScoreOutOfRangeException(submissionId, entered, homework.MaxMarks);
                }
            }

            // BR-LRN-005: the lateness penalty applies HERE, at marking — never
            // automatically at submit. The stored value is the mark that counts,
            // so the tracker, the portal and Module 17 all read one number.
            submission.Score = SubmissionLatenessEvaluator.PenalisedScore(
                score, submission.IsLate, homework.LatenessPolicy, homework.LatePenaltyPercent);
            submission.Feedback = feedback;

            // Marking is the act, whether it produced a score or only words; the
            // status records only whether a score now exists, because that is
            // what BR-LRN-011 counts.
            submission.MarkedByUserAccountId = _user.UserId;
            submission.MarkedAtUtc = _clock.UtcNow;
            submission.Status = submission.Score is null ? SubmissionStatus.Submitted : SubmissionStatus.Marked;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ReleaseAsync(
            int homeworkId,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            var homework = await _db.Homeworks.SingleAsync(h => h.Id == homeworkId, cancellationToken);
            await GuardReachAsync(homework, hasSchoolWideReach, cancellationToken);

            var submissions = await _db.HomeworkSubmissions
                .Where(s => s.HomeworkId == homeworkId)
                .ToListAsync(cancellationToken);

            // BR-LRN-011 counts hand-ins that carry no score. A student who never
            // handed in is NOT counted: there is nothing to mark, and counting
            // them would leave a homework unreleasable whenever anyone was absent.
            var unscored = submissions.Count(s => s.Score is null);

            var refusal = HomeworkReleaseGate.Check(
                homework.Status, homework.MaxMarks, homework.BlueprintComponentId, unscored);
            if (refusal != HomeworkReleaseRefusal.None)
            {
                throw new HomeworkReleaseRefusedException(homeworkId, refusal, unscored);
            }

            var componentId = homework.BlueprintComponentId!.Value;
            var marksheet = await ResolveMarksheetAsync(homework, componentId, cancellationToken);

            var scored = submissions.Where(s => s.Score is not null).ToList();

            // Resolved BEFORE the first write, so "this sheet does not cover that
            // student" cannot leave half a class released. IGradingAdmin's own
            // EnterMarkAsync would throw a raw English SingleAsync failure on the
            // same condition, which §9 forbids surfacing.
            var covered = await _db.MarkEntries
                .Where(e => e.MarksheetId == marksheet.Id && e.BlueprintComponentId == componentId)
                .Select(e => e.EnrollmentId)
                .ToListAsync(cancellationToken);
            var coveredSet = new HashSet<int>(covered);

            foreach (var submission in scored)
            {
                if (!coveredSet.Contains(submission.EnrollmentId))
                {
                    throw new HomeworkMarksheetUnresolvedException(homeworkId, componentId, submission.EnrollmentId);
                }
            }

            // BR-LRN-012: a RAW mark into Module 17's marksheet, and nothing else.
            // No ChangeTracker.Clear() in this loop, deliberately: it is bounded
            // by one section's roster (tens of rows, not the thousand-row rollover
            // the rule was written for), and the homework header this method
            // mutates afterwards must stay tracked to be saved at all.
            //
            // IGradingAdmin saves per call, so a failure part-way leaves marks
            // written and the homework still in Marking. Re-running is safe and is
            // the recovery: EnterMarkAsync sets a value rather than accumulating,
            // so the same marks land on the same entries.
            foreach (var submission in scored)
            {
                await _grading.EnterMarkAsync(
                    marksheet.Id, componentId, submission.EnrollmentId, submission.Score,
                    isAbsent: false, isExempt: false, cancellationToken);
            }

            // Students who submitted nothing are left alone rather than written as
            // zero: "did not hand in" is a judgement to record in Module 17's own
            // marksheet as an absence, an exemption or a nil mark, and posting
            // zeros from here would be this module deciding a grade.
            foreach (var submission in scored)
            {
                submission.Status = SubmissionStatus.Released;
            }

            homework.Status = HomeworkStatus.Released;

            // §12 MarkReleased. Ambient publish, so the message and the status
            // move commit together: a family told the work was marked, on a
            // transaction that then rolled back, would be told about a mark that
            // does not exist.
            //
            // Only the students whose work was actually marked. Someone who
            // handed nothing in has had no mark released, and BR-LRN-012 left
            // their row to Module 17's judgement rather than posting a zero — so
            // there is nothing here to tell their family either.
            await _notifications.PublishAsync(
                MarkReleasedEventCode,
                await StudentAndFamilyRecipientsAsync(
                    scored.Select(s => s.EnrollmentId).ToList(), cancellationToken),
                await HomeworkPayloadAsync(homework, cancellationToken),
                cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> ChaseAsync(
            int homeworkId,
            IReadOnlyCollection<int> enrollmentIds,
            bool hasSchoolWideReach = false,
            CancellationToken cancellationToken = default)
        {
            if (enrollmentIds is null || enrollmentIds.Count == 0)
            {
                return 0;
            }

            var homework = await _db.Homeworks.SingleAsync(h => h.Id == homeworkId, cancellationToken);
            await GuardReachAsync(homework, hasSchoolWideReach, cancellationToken);

            // The roster is the authority on who may be chased, and it is already
            // reach-checked and restricted to the section's current membership.
            // Intersecting against it is what stops a hand-edited form from
            // messaging a family in a class this user does not teach.
            var roster = await RosterAsync(homeworkId, hasSchoolWideReach, cancellationToken);

            var chase = roster
                .Where(r => !r.HasSubmitted && enrollmentIds.Contains(r.EnrollmentId))
                .Select(r => r.EnrollmentId)
                .ToList();

            if (chase.Count == 0)
            {
                return 0;
            }

            await _notifications.PublishAsync(
                OverdueEventCode,
                await StudentAndFamilyRecipientsAsync(chase, cancellationToken),
                await HomeworkPayloadAsync(homework, cancellationToken),
                cancellationToken);

            // Nothing of this module's own is written — a chase is a message, not
            // a state change, and recording "chased" on the submission row would
            // be inventing a status BR-LRN-005 does not have. The delivery rows
            // the publisher writes are the record that it happened, and they are
            // Module 33's to keep.
            await _db.SaveChangesAsync(cancellationToken);

            return chase.Count;
        }

        /// <summary>
        /// §12 routes these to "student and parents". The student's own portal
        /// account is included when they have one — older grades do, younger ones
        /// do not (<c>Student.UserAccountId</c> is nullable by design) — and the
        /// family's guardians always are, which is why a child with no account of
        /// their own is still reachable.
        /// </summary>
        private async Task<IReadOnlyCollection<NotificationRecipient>> StudentAndFamilyRecipientsAsync(
            IReadOnlyCollection<int> enrollmentIds, CancellationToken cancellationToken)
        {
            var studentIds = await _db.Enrollments
                .Where(e => enrollmentIds.Contains(e.Id))
                .Select(e => e.StudentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (studentIds.Count == 0)
            {
                return new List<NotificationRecipient>();
            }

            var recipients = new Dictionary<int, NotificationRecipient>();

            var studentAccounts = await _db.Students
                .Where(s => studentIds.Contains(s.Id) && s.UserAccountId != null)
                .Select(s => s.UserAccountId!.Value)
                .ToListAsync(cancellationToken);

            foreach (var accountId in studentAccounts)
            {
                // A student row carries no language preference of its own — only
                // Parent does — so the product default stands, exactly as it does
                // on Parent itself.
                recipients[accountId] = new NotificationRecipient(accountId, "ar");
            }

            var parentIds = await _db.StudentGuardianLinks
                .Where(l => studentIds.Contains(l.StudentId) && l.EffectiveToUtc == null)
                .Select(l => l.ParentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var parents = await _db.Parents
                .Where(p => parentIds.Contains(p.Id) && p.UserAccountId != null)
                .Select(p => new { p.UserAccountId, p.PreferredLanguage })
                .ToListAsync(cancellationToken);

            foreach (var parent in parents)
            {
                // A guardian of two children in the same class is one person and
                // gets one message, in their own language.
                recipients[parent.UserAccountId!.Value] =
                    new NotificationRecipient(parent.UserAccountId!.Value, parent.PreferredLanguage);
            }

            return recipients.Values.ToList();
        }

        /// <summary>
        /// The placeholders both module 37 templates name. The subject comes from
        /// the offering rather than the homework, because BR-LRN-001 anchors work
        /// on the offering precisely so the subject is year-correct.
        /// </summary>
        private async Task<IReadOnlyDictionary<string, string>> HomeworkPayloadAsync(
            Domain.Learning.Homework homework, CancellationToken cancellationToken)
        {
            // Looked up, not picked: a retired subject must still name itself on a
            // message about work already set for it (SoftActiveLookupTests).
            var subject = await (
                from o in _db.CurriculumOfferings.IgnoreQueryFilters()
                join s in _db.Subjects.IgnoreQueryFilters() on o.SubjectId equals s.Id
                where o.Id == homework.CurriculumOfferingId && s.SchoolId == _db.CurrentSchoolId
                select new { s.Name.NameAr, s.Name.NameEn })
                .SingleOrDefaultAsync(cancellationToken);

            return new Dictionary<string, string>
            {
                ["Homework"] = homework.TitleAr,
                ["HomeworkEn"] = homework.TitleEn,
                ["Subject"] = subject?.NameAr ?? string.Empty,
                ["SubjectEn"] = subject?.NameEn ?? string.Empty,
                ["DueDate"] = homework.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            };
        }

        /// <summary>
        /// BR-LRN-012: finds the one Module 17 marksheet these marks belong in —
        /// the homework's blueprint component names its blueprint, and the
        /// homework names the section. Refuses rather than inventing a second
        /// mark store when there is none.
        /// </summary>
        private async Task<Marksheet> ResolveMarksheetAsync(
            Domain.Learning.Homework homework, int componentId, CancellationToken cancellationToken)
        {
            var blueprintId = await _db.BlueprintComponents
                .Where(c => c.Id == componentId)
                .Select(c => (int?)c.BlueprintId)
                .SingleOrDefaultAsync(cancellationToken);

            if (blueprintId is null)
            {
                throw new HomeworkMarksheetUnresolvedException(homework.Id, componentId);
            }

            // Unique by (BlueprintId, SectionId) in Module 17's own configuration,
            // so there is exactly one sheet or none.
            var marksheet = await _db.Marksheets
                .SingleOrDefaultAsync(m => m.BlueprintId == blueprintId && m.SectionId == homework.SectionId, cancellationToken);

            if (marksheet is null)
            {
                throw new HomeworkMarksheetUnresolvedException(homework.Id, componentId);
            }

            if (marksheet.Status == MarksheetStatus.Published)
            {
                throw new HomeworkReleaseMarksheetPublishedException(homework.Id, marksheet.Id);
            }

            return marksheet;
        }

        /// <summary>
        /// BR-LRN-002, resolved once through the desk's own reach so the picker
        /// and the guard cannot diverge. Throws <see cref="TeachingReachException"/>
        /// unless the acting user holds this homework's (offering, section) pair
        /// or heads the offering's department.
        /// </summary>
        private async Task GuardReachAsync(
            Domain.Learning.Homework homework, bool hasSchoolWideReach, CancellationToken cancellationToken)
        {
            // Checked first rather than by looking for the pair in the school-wide
            // list: a Vice-Principal must still reach work whose placement has
            // since been taken out of the published timetable.
            if (hasSchoolWideReach)
            {
                return;
            }

            var reachable = await _homeworkAdmin.ReachableSectionsAsync(false, cancellationToken);

            if (!TeachingReachEvaluator.CanIssueToSection(
                reachable, null, false, homework.CurriculumOfferingId, homework.SectionId))
            {
                throw new TeachingReachException(homework.CurriculumOfferingId);
            }
        }

        /// <summary>Composes a display name from the parts that were filled in, following Security/AccountPeople's precedent for the same question.</summary>
        private static string JoinName(params string?[] parts)
            => string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).Trim();
    }
}
