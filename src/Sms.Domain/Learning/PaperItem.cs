using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.PaperItem (doc/Modules/37 §7, BR-LRN-007): one question on one paper,
    /// pinned to the exact question <em>version</em> it was added from.
    ///
    /// <para>
    /// <b>This row is what freezes a question.</b> It stores
    /// <see cref="QuestionId"/> — a version's own id, never its root — so a later
    /// revision of that question changes nothing about this paper. That is the
    /// whole mechanism behind BR-LRN-007's "a past paper always renders as it was
    /// answered": not a rule the code remembers to apply, but a foreign key
    /// pointing at a row nobody edits.
    /// </para>
    ///
    /// <para>
    /// <see cref="Marks"/> is copied from the question rather than read through
    /// it, because a paper may weight a question differently from the bank's
    /// default and because the bank's default may later change. What the paper is
    /// worth must not move under a class that has already sat it.
    /// </para>
    ///
    /// Not <c>[Audited]</c> on its own: it is part of the paper's shape and is
    /// audited with it (BR-LRN-015 T2).
    /// </summary>
    public class PaperItem : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int OnlinePaperId { get; set; }

        /// <summary>The frozen question version — <c>Question.Id</c>, not <c>Question.RootQuestionId</c>.</summary>
        public int QuestionId { get; set; }

        public int DisplayOrder { get; set; }

        /// <summary>What this question is worth on this paper. Copied at add time; see the class remarks.</summary>
        public decimal Marks { get; set; }
    }
}
