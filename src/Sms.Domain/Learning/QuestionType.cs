namespace Sms.Domain.Learning
{
    /// <summary>
    /// doc/Modules/37 §7 and BR-LRN-011's auto-marking list, which is the reason
    /// this enum exists in exactly this shape: "single choice, multiple choice,
    /// true/false, numeric with tolerance, exact-match short text" mark
    /// themselves, and everything else is a constructed response for the manual
    /// queue.
    ///
    /// <para>
    /// The five auto-markable members are numbered first and
    /// <see cref="QuestionTypeRules.IsAutoMarkable"/> reads the boundary off that
    /// order, so adding a sixth objective type is one member and one number —
    /// while adding a constructed one cannot be mistaken for objective by
    /// accident. A type that quietly fell on the wrong side of that line would
    /// award marks nobody checked.
    /// </para>
    ///
    /// Starts at 1 per the SMALLINT convention (docs/Database/01).
    /// </summary>
    public enum QuestionType
    {
        /// <summary>Options, exactly one correct.</summary>
        SingleChoice = 1,

        /// <summary>Options, more than one correct. Partial credit is the paper's business, not the bank's.</summary>
        MultipleChoice = 2,

        /// <summary>Two options the product supplies rather than the author typing them twice in every question.</summary>
        TrueFalse = 3,

        /// <summary>A number, marked within a stated tolerance — 3.14 and 3.14159 are the same answer to a physics question and different answers to a rounding one, so the author says which.</summary>
        Numeric = 4,

        /// <summary>A short text matched against the accepted answers the author listed. Comparison rules live in <see cref="QuestionTypeRules"/>, never in a screen.</summary>
        ShortText = 5,

        /// <summary>BR-LRN-011: a constructed response. Never auto-marked, always queued for a person.</summary>
        Essay = 6,
    }
}
