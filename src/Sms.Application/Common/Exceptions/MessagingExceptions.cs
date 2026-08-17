using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-MSG-001: a grade/stage/school-wide announcement needs approval before it can send.</summary>
    public class AnnouncementNotApprovedException : InvalidOperationException
    {
        public AnnouncementNotApprovedException(int announcementId)
            : base($"Announcement {announcementId} requires approval before it can send (BR-MSG-001).")
        {
        }
    }

    /// <summary>doc/Modules/32 §7: no CommunicationMatrix entry exists for this topic — routing can't resolve.</summary>
    public class UnroutableTopicException : InvalidOperationException
    {
        public UnroutableTopicException(string topicCode)
            : base($"No communication-matrix routing exists for topic '{topicCode}' (BR-MSG-002).")
        {
        }
    }
}
