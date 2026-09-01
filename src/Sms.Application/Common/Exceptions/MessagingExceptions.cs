using System;
using Sms.Domain.Messaging;

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

    /// <summary>
    /// doc/Modules/32 §9: "audience must resolve &gt; 0". A send to nobody is almost
    /// always a mis-picked target rather than an empty class, and letting it through
    /// stamps the announcement Sent with a reach of zero — indistinguishable, afterwards,
    /// from one that failed.
    /// </summary>
    public class EmptyAudienceException : InvalidOperationException
    {
        public EmptyAudienceException(int announcementId)
            : base($"Announcement {announcementId} resolves to no recipients (doc/Modules/32 §9).")
        {
            AnnouncementId = announcementId;
        }

        public int AnnouncementId { get; }
    }

    /// <summary>
    /// The announcement names a scope that needs a target — a section, a grade, a stage —
    /// and does not name one, or names one for a school-wide send that takes none.
    /// </summary>
    public class InvalidAudienceTargetException : InvalidOperationException
    {
        public InvalidAudienceTargetException(AudienceScope scope)
            : base($"Audience scope '{scope}' was given the wrong kind of target (doc/Modules/32 §8.1).")
        {
            Scope = scope;
        }

        public AudienceScope Scope { get; }
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
