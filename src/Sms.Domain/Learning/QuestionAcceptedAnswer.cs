using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.QuestionAcceptedAnswer: one spelling a
    /// <see cref="QuestionType.ShortText"/> question will accept.
    ///
    /// <para>
    /// A list rather than one string because BR-LRN-011 promises "exact-match
    /// short text" auto-marking, and exact match against a single answer marks
    /// "H2O" wrong when the author also meant "water" — in a bilingual school,
    /// against a student who wrote "ماء". The author lists what counts; the
    /// engine compares. Anything cleverer than a listed match is a constructed
    /// response and belongs in the manual queue.
    /// </para>
    ///
    /// <para>
    /// Hangs off the question <em>version</em> for the same reason options do: a
    /// revision that added an accepted spelling must not retroactively change
    /// whether last term's answer was right.
    /// </para>
    /// </summary>
    public class QuestionAcceptedAnswer : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int QuestionId { get; set; }

        public string Text { get; set; } = string.Empty;
    }
}
