using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Learning
{
    /// <summary>
    /// lrn.QuestionBank (doc/Modules/37 §7, §8.6, BR-LRN-001/007): the questions
    /// written for one <c>CurriculumOffering</c> — never for a raw Subject, so
    /// the bank is year-correct by construction like everything else in this
    /// module (BR-SUB-002/005).
    ///
    /// <para>
    /// A bank rather than a bare list of questions because BR-LRN-007's sharing
    /// is decided per bank: <see cref="ShareScope"/> says whether the other
    /// teachers of this offering may draw on it, which is a decision about a
    /// collection and would be unanswerable question by question.
    /// </para>
    ///
    /// <para>
    /// <see cref="IActivatable"/> but <b>not</b> <c>ISoftActiveFiltered</c>: §7
    /// puts versioned catalogs outside the filter, and a bank whose questions sit
    /// on a paper somebody already answered must stay loadable after it is
    /// retired. Retiring it stops future picks and nothing else (BR-GLB-006).
    /// </para>
    ///
    /// T2 per BR-LRN-015 — a definition, field-level audited. The marks its
    /// questions later carry are Module 17's T1 concern.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class QuestionBank : AuditableEntity, ISchoolScoped, IYearScoped, IActivatable
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        /// <summary>BR-LRN-001: the anchor.</summary>
        public int CurriculumOfferingId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        /// <summary>BR-LRN-007: whether this offering's other teachers may draw on it.</summary>
        public QuestionShareScope ShareScope { get; set; } = QuestionShareScope.AuthorOnly;

        public bool IsActive { get; set; } = true;
    }
}
