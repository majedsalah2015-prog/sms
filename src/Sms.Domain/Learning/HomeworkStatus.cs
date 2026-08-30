namespace Sms.Domain.Learning
{
    /// <summary>
    /// doc/Modules/37 §4 homework lifecycle: draft -> issued -> collecting ->
    /// marking -> released, plus the withdrawal the module's notification set
    /// (§12 <c>HomeworkWithdrawn</c>) and BR-LRN-016 both require.
    ///
    /// Starts at 1 per the SMALLINT convention (docs/Database/01) — module 37
    /// gives no explicit numbering for this column.
    ///
    /// <see cref="Released"/> is terminal on purpose: releasing writes a raw
    /// mark into Module 17's marksheet (BR-LRN-012), and from that moment the
    /// mark belongs to Module 17's change control — correcting it is a mark
    /// change there, never a status move back to marking here. Two modules that
    /// can both rewind the same mark is how a school ends up with two report
    /// cards that disagree.
    /// </summary>
    public enum HomeworkStatus
    {
        Draft = 1,

        /// <summary>Published to the section. BR-LRN-003: the moment families see it.</summary>
        Issued = 2,

        /// <summary>At least one submission is in. Distinct from <see cref="Issued"/> because withdrawal reads differently once a student has handed something in (§9).</summary>
        Collecting = 3,

        /// <summary>Closed to new work as far as the teacher's queue is concerned; marking is under way.</summary>
        Marking = 4,

        /// <summary>Marks handed to Module 17 (BR-LRN-012). Terminal here.</summary>
        Released = 5,

        /// <summary>BR-LRN-016: withdrawn with a stated reason, never deleted.</summary>
        Withdrawn = 6,
    }
}
