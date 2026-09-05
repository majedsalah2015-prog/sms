using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.QuestionOption (doc/Modules/37 §7): one choice under a
    /// <see cref="Question"/> of a choice type, bilingual, with the correct flag.
    ///
    /// <para>
    /// Options belong to a <em>version</em> of a question rather than to its
    /// root, and that is the half of BR-LRN-007 that is easy to get wrong: a
    /// revision that reworded the stem but reused the original's option rows
    /// would silently rewrite the choices a student was shown last term. Each
    /// version carries its own copy.
    /// </para>
    ///
    /// <para>
    /// Not <c>[Audited]</c> and carries no lifecycle of its own. It is part of
    /// the question's shape, audited with it as a definition (BR-LRN-015 T2), and
    /// it is never edited after its version is frozen — a change makes a new
    /// version of the question, which brings new options with it.
    /// </para>
    /// </summary>
    public class QuestionOption : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int QuestionId { get; set; }

        public string TextAr { get; set; } = string.Empty;

        public string TextEn { get; set; } = string.Empty;

        /// <summary>
        /// More than one may be true, and that is what separates
        /// <see cref="QuestionType.MultipleChoice"/> from
        /// <see cref="QuestionType.SingleChoice"/>. The count is checked by
        /// <c>QuestionTypeRules</c>, not by a screen.
        /// </summary>
        public bool IsCorrect { get; set; }

        /// <summary>Authoring order. A sitting may shuffle it per student (BR-LRN-009); the bank keeps the order the author meant.</summary>
        public int DisplayOrder { get; set; }
    }
}
