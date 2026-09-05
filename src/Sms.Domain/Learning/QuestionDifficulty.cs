namespace Sms.Domain.Learning
{
    /// <summary>
    /// doc/Modules/37 §8.7 — one of the three axes a paper's generation rule
    /// draws on ("by topic/difficulty/type"). Three bands rather than a 1-10
    /// scale on purpose: an author can place a question in three buckets
    /// consistently, and a blueprint that asked for "four questions at level 7"
    /// would be asking for a precision nobody can author to.
    ///
    /// Starts at 1 per the SMALLINT convention (docs/Database/01).
    /// </summary>
    public enum QuestionDifficulty
    {
        Easy = 1,
        Medium = 2,
        Hard = 3,
    }
}
