namespace Sms.Domain.Learning
{
    /// <summary>
    /// BR-LRN-007's "shared between teachers of the same subject (optionally, per
    /// settings)". Expressed on the bank as a scope rather than as a boolean
    /// share flag, because "who may see this" has a third answer the product
    /// needs — the department — and a boolean would have to be replaced the
    /// first time a head of department asked for it.
    ///
    /// Starts at 1 per the SMALLINT convention (docs/Database/01).
    /// </summary>
    public enum QuestionShareScope
    {
        /// <summary>Only the teacher who wrote it. The default: a draft question is not a shared asset.</summary>
        AuthorOnly = 1,

        /// <summary>Every teacher holding a placement on this offering (BR-LRN-002).</summary>
        Offering = 2,

        /// <summary>Every teacher in the offering's department — the head of department's own view.</summary>
        Department = 3,
    }
}
