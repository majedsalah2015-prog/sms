using System;
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
}
