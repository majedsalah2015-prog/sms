using System;
using Sms.Application.Learning;
using Sms.Domain.Learning;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-LRN-003/016 (doc/Modules/37 §4): the content lifecycle does not offer this move. Notably there is no un-publish — published content leaves the portal by being retired.</summary>
    public class LessonTransitionException : InvalidOperationException
    {
        public LessonTransitionException(int lessonId, LessonStatus from, LessonStatus to)
            : base($"Lesson {lessonId} cannot move from {from} to {to} (BR-LRN-003/016).")
        {
            From = from;
            To = to;
        }

        public LessonStatus From { get; }

        public LessonStatus To { get; }
    }

    /// <summary>BR-LRN-002: the author holds no placement on this offering, does not head its department, and has no school-wide reach.</summary>
    public class TeachingReachException : InvalidOperationException
    {
        public TeachingReachException(int curriculumOfferingId)
            : base($"No teaching reach over curriculum offering {curriculumOfferingId} (BR-LRN-002).")
        {
            CurriculumOfferingId = curriculumOfferingId;
        }

        public int CurriculumOfferingId { get; }
    }

    /// <summary>BR-LRN-016: a retired lesson is readable history, not an editable draft.</summary>
    public class LessonRetiredException : InvalidOperationException
    {
        public LessonRetiredException(int lessonId)
            : base($"Lesson {lessonId} is retired and can no longer be edited (BR-LRN-016).")
        {
        }
    }

    /// <summary>BR-LRN-001: a lesson bound to a dated session must be bound to one that teaches the same offering — otherwise "what happened that period" names a period that never taught it.</summary>
    public class LessonSessionMismatchException : InvalidOperationException
    {
        public LessonSessionMismatchException(int sessionId, int curriculumOfferingId)
            : base($"Session {sessionId} does not teach curriculum offering {curriculumOfferingId} (BR-LRN-001).")
        {
        }
    }

    /// <summary>BR-LRN-006 / BR-ATT-009: an unscanned or infected file is never served, to staff or to the portal.</summary>
    public class ResourceNotScanCleanException : InvalidOperationException
    {
        public ResourceNotScanCleanException(int attachmentId)
            : base($"Attachment {attachmentId} is not virus-scan clean and cannot be served (BR-LRN-006).")
        {
            AttachmentId = attachmentId;
        }

        public int AttachmentId { get; }
    }

    /// <summary>BR-LRN-003/012/016 (doc/Modules/37 §4): the homework lifecycle does not offer this move. There is no un-issue, and nothing moves out of Released — that mark is Module 17's from the moment it lands.</summary>
    public class HomeworkTransitionException : InvalidOperationException
    {
        public HomeworkTransitionException(int homeworkId, HomeworkStatus from, HomeworkStatus to)
            : base($"Homework {homeworkId} cannot move from {from} to {to} (doc/Modules/37 §4).")
        {
            From = from;
            To = to;
        }

        public HomeworkStatus From { get; }

        public HomeworkStatus To { get; }
    }

    /// <summary>
    /// BR-LRN-004: the homework is not in a state to be put in front of a class.
    /// Carries the specific refusal so the Web boundary can name the actual
    /// problem — "this needs a component" and "that day is a holiday" are
    /// different conversations with the teacher.
    /// </summary>
    public class HomeworkIssueRefusedException : InvalidOperationException
    {
        public HomeworkIssueRefusedException(int homeworkId, HomeworkIssueRefusal reason)
            : base($"Homework {homeworkId} cannot be issued: {reason} (BR-LRN-004).")
        {
            Reason = reason;
        }

        public HomeworkIssueRefusal Reason { get; }
    }

    /// <summary>
    /// doc/Modules/37 §9: withdrawal after the due date is blocked once
    /// submissions exist. Before the due date it is allowed and the students who
    /// submitted are told (§12 <c>HomeworkWithdrawn</c>); after it, work already
    /// handed in cannot be made to have never been asked for.
    /// </summary>
    public class HomeworkWithdrawalBlockedException : InvalidOperationException
    {
        public HomeworkWithdrawalBlockedException(int homeworkId, int submissionCount)
            : base($"Homework {homeworkId} is past its due date and has {submissionCount} submission(s); it can no longer be withdrawn (doc/Modules/37 §9).")
        {
            SubmissionCount = submissionCount;
        }

        public int SubmissionCount { get; }
    }

    // ---------------------------------------------------------------- §8.4/§8.5/§8.10 submissions

    /// <summary>
    /// BR-LRN-013: the acting account is not the student's own. This is the rule
    /// that refuses a <b>parent</b> submitting on a child's behalf — and it is
    /// deliberately not
    /// <see cref="PortalAccessDeniedException"/>, which means "no visibility into
    /// this student" (BR-SEC-011). A parent has visibility; what they do not have
    /// is a hand. Telling them they cannot see their child would be both wrong
    /// and, at the Web boundary, untranslatable into the sentence they need:
    /// homework is handed in from the student's own account.
    /// </summary>
    public class PortalSubmissionIdentityException : InvalidOperationException
    {
        public PortalSubmissionIdentityException(int requestingUserAccountId)
            : base($"Account {requestingUserAccountId} is not a student account and cannot submit homework (BR-LRN-013).")
        {
            RequestingUserAccountId = requestingUserAccountId;
        }

        public int RequestingUserAccountId { get; }
    }

    /// <summary>
    /// BR-LRN-002/003/013: this homework is not this student's to hand in — it is
    /// set to another section, it is not visible in the portal (a draft, or
    /// withdrawn), the student has no enrollment in its year, or it does not
    /// exist at all.
    /// <para>
    /// All of those are one exception on purpose. BR-SEC-010's posture is that
    /// unauthorized surface <em>disappears</em>: an id that is not yours and an
    /// id that is nothing must answer identically, or the difference between the
    /// two answers is a way to enumerate the school's homework.
    /// </para>
    /// </summary>
    public class HomeworkNotOfferedToStudentException : InvalidOperationException
    {
        public HomeworkNotOfferedToStudentException(int homeworkId)
            : base($"Homework {homeworkId} is not set to this student's section, or is not visible in the portal (BR-LRN-003/013).")
        {
            HomeworkId = homeworkId;
        }

        public int HomeworkId { get; }
    }

    /// <summary>
    /// doc/Modules/37 §4: the homework no longer accepts work.
    /// <para>
    /// Note what this is <b>not</b>: it is never a refusal for lateness.
    /// BR-LRN-005 keeps a homework open past its due date — late work is accepted
    /// and flagged — so only the status closes the door, and the status says
    /// which door it was: marking has begun, the marks are already Module 17's,
    /// or the work was withdrawn.
    /// </para>
    /// </summary>
    public class HomeworkClosedToSubmissionsException : InvalidOperationException
    {
        public HomeworkClosedToSubmissionsException(int homeworkId, HomeworkStatus status)
            : base($"Homework {homeworkId} is {status} and no longer accepts submissions (doc/Modules/37 §4).")
        {
            HomeworkId = homeworkId;
            Status = status;
        }

        public int HomeworkId { get; }

        public HomeworkStatus Status { get; }
    }

    /// <summary>
    /// BR-LRN-012: marking is closed on this homework. Once it is Released the
    /// mark belongs to Module 17 and a correction is a mark change under that
    /// module's change control (T1, reason mandatory), never a re-mark here;
    /// once it is Withdrawn there is no mark to give.
    /// </summary>
    public class SubmissionMarkingClosedException : InvalidOperationException
    {
        public SubmissionMarkingClosedException(int submissionId, HomeworkStatus status)
            : base($"Submission {submissionId} belongs to a {status} homework and can no longer be marked (BR-LRN-012).")
        {
            SubmissionId = submissionId;
            Status = status;
        }

        public int SubmissionId { get; }

        public HomeworkStatus Status { get; }
    }

    /// <summary>
    /// BR-LRN-004: the mark does not fit the homework it is being given for —
    /// negative, above its max marks, or entered at all against ungraded
    /// practice, which by definition has no scale to be out of.
    /// <para>
    /// Caught here rather than at release, because a mark of 30 out of 20 handed
    /// to Module 17 becomes a term percentage over 100 that nothing downstream
    /// will question.
    /// </para>
    /// </summary>
    public class SubmissionScoreOutOfRangeException : InvalidOperationException
    {
        public SubmissionScoreOutOfRangeException(int submissionId, decimal score, decimal? maxMarks)
            : base(maxMarks is null
                ? $"Submission {submissionId} belongs to ungraded practice and cannot carry a score of {score} (BR-LRN-004)."
                : $"Score {score} is outside 0..{maxMarks} for submission {submissionId} (BR-LRN-004).")
        {
            SubmissionId = submissionId;
            Score = score;
            MaxMarks = maxMarks;
        }

        public int SubmissionId { get; }

        public decimal Score { get; }

        /// <summary>Null means the homework is ungraded practice — the refusal is that a score exists at all, not that it is too large.</summary>
        public decimal? MaxMarks { get; }
    }

    /// <summary>
    /// BR-LRN-011/012 (doc/Modules/37 §8.5): the homework's marks cannot be
    /// handed to Module 17 yet. Carries the specific refusal and, for
    /// <see cref="HomeworkReleaseRefusal.SubmissionsUnscored"/>, how many hand-ins
    /// are still unmarked — so the Web boundary can say "4 submissions are not
    /// yet marked" rather than "release failed", which tells a teacher nothing
    /// about what to do next.
    /// </summary>
    public class HomeworkReleaseRefusedException : InvalidOperationException
    {
        public HomeworkReleaseRefusedException(int homeworkId, HomeworkReleaseRefusal reason, int unscoredSubmissionCount = 0)
            : base($"Homework {homeworkId} cannot be released: {reason} ({unscoredSubmissionCount} unscored) (BR-LRN-011/012).")
        {
            HomeworkId = homeworkId;
            Reason = reason;
            UnscoredSubmissionCount = unscoredSubmissionCount;
        }

        public int HomeworkId { get; }

        public HomeworkReleaseRefusal Reason { get; }

        /// <summary>Meaningful only for <see cref="HomeworkReleaseRefusal.SubmissionsUnscored"/>; zero otherwise.</summary>
        public int UnscoredSubmissionCount { get; }
    }

    /// <summary>
    /// BR-LRN-012: the marks have nowhere to land. Either no Module 17 marksheet
    /// exists for this homework's blueprint and section, or one exists but
    /// carries no entry for a student who submitted (they joined the section
    /// after the sheet was created).
    ///
    /// <para>
    /// This module <b>refuses rather than inventing a second mark store</b>. §1
    /// names that as the failure the whole design is arranged to prevent: an LMS
    /// that keeps its own marks is how a school ends up with two report cards
    /// that disagree. The fix is in Module 17 — create or refresh the marksheet —
    /// and the release is then re-run unchanged.
    /// </para>
    /// </summary>
    public class HomeworkMarksheetUnresolvedException : InvalidOperationException
    {
        public HomeworkMarksheetUnresolvedException(int homeworkId, int blueprintComponentId, int? enrollmentId = null)
            : base(enrollmentId is null
                ? $"No Module 17 marksheet covers homework {homeworkId}'s component {blueprintComponentId} for its section (BR-LRN-012)."
                : $"Module 17's marksheet for homework {homeworkId}'s component {blueprintComponentId} has no entry for enrollment {enrollmentId} (BR-LRN-012).")
        {
            HomeworkId = homeworkId;
            BlueprintComponentId = blueprintComponentId;
            EnrollmentId = enrollmentId;
        }

        public int HomeworkId { get; }

        public int BlueprintComponentId { get; }

        /// <summary>Null when no marksheet exists at all; set when the sheet exists but does not cover this student.</summary>
        public int? EnrollmentId { get; }
    }

    /// <summary>
    /// BR-LRN-012: the marksheet these marks would land in is already Published.
    /// Writing into it from here would change a mark a family has already been
    /// shown, bypassing Module 17's WF-08 post-publication correction (P4
    /// Principal, reason mandatory) — which is precisely the "never bypasses the
    /// approval chain" half of the rule.
    ///
    /// <para>
    /// An Approved-but-unpublished sheet is deliberately <em>not</em> refused:
    /// nothing has reached a family yet, Module 17's own
    /// <c>EnterMarkAsync</c> places no guard there, and this module must not
    /// invent a stricter rule than the module that owns the mark.
    /// </para>
    /// </summary>
    public class HomeworkReleaseMarksheetPublishedException : InvalidOperationException
    {
        public HomeworkReleaseMarksheetPublishedException(int homeworkId, int marksheetId)
            : base($"Marksheet {marksheetId} is already published; homework {homeworkId}'s marks must go through Module 17's WF-08 correction (BR-LRN-012).")
        {
            HomeworkId = homeworkId;
            MarksheetId = marksheetId;
        }

        public int HomeworkId { get; }

        public int MarksheetId { get; }
    }

    /// <summary>
    /// BR-LRN-011 (doc/Modules/37 §8.6): a question that cannot be marked as it
    /// stands. Carries the specific refusal rather than a sentence, so the Web
    /// boundary translates it and the engine stays language-free.
    /// </summary>
    public class QuestionShapeException : InvalidOperationException
    {
        public QuestionShapeException(QuestionShapeRefusal refusal, QuestionType type)
            : base($"A {type} question is not answerable as it stands: {refusal} (BR-LRN-011).")
        {
            Refusal = refusal;
            Type = type;
        }

        public QuestionShapeRefusal Refusal { get; }

        public QuestionType Type { get; }
    }

    /// <summary>
    /// BR-LRN-007: a deprecated question is history. Reviving it under a new
    /// wording is creating a question, not editing one.
    /// </summary>
    public class QuestionDeprecatedException : InvalidOperationException
    {
        public QuestionDeprecatedException(int questionId)
            : base($"Question {questionId} is deprecated and cannot be revised (BR-LRN-007).")
            => QuestionId = questionId;

        public int QuestionId { get; }
    }

    /// <summary>
    /// BR-LRN-007: a question is revised from its current version. An older one is
    /// the record of what a student answered and is never the base of an edit.
    /// </summary>
    public class QuestionNotCurrentVersionException : InvalidOperationException
    {
        public QuestionNotCurrentVersionException(int questionId, int version)
            : base($"Question {questionId} is version {version} and is not the current one (BR-LRN-007).")
        {
            QuestionId = questionId;
            Version = version;
        }

        public int QuestionId { get; }

        public int Version { get; }
    }

    /// <summary>BR-GLB-006: a retired bank takes no new questions, and keeps every one it has.</summary>
    public class QuestionBankRetiredException : InvalidOperationException
    {
        public QuestionBankRetiredException(int questionBankId)
            : base($"Question bank {questionBankId} is retired (BR-GLB-006).")
            => QuestionBankId = questionBankId;

        public int QuestionBankId { get; }
    }
}
