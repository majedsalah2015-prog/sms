namespace Sms.Domain.Learning
{
    /// <summary>
    /// doc/Modules/37 §4/§8.4 — where one student's hand-in stands on the
    /// homework's own spine: it arrives, it is marked, its mark is released to
    /// Module 17.
    ///
    /// Starts at 1 per the SMALLINT convention (docs/Database/01) — module 37
    /// gives no explicit numbering for this column.
    ///
    /// <para>
    /// <b>"Missing" and "Late" are deliberately not members here, and no later
    /// slice may add them.</b> §8.4's tracker reads "submitted / late / missing",
    /// which is three <em>columns of a roster</em>, not three states of a row:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Missing</b> is the absence of a row. A student who handed nothing in
    /// has no <see cref="HomeworkSubmission"/> at all, so the roster derives
    /// "missing" by left-joining the section against this table. A
    /// <c>Missing</c> status would mean pre-seeding an empty submission per
    /// student per homework — rows that claim a hand-in happened, and a
    /// <c>SubmittedAtUtc</c> that would have to lie.
    /// </description></item>
    /// <item><description>
    /// <b>Late</b> is <see cref="HomeworkSubmission.IsLate"/>, a flag that is
    /// orthogonal to every status here: late work is marked and released like
    /// any other (BR-LRN-005 — accepted and flagged, never refused). As a status
    /// it would be mutually exclusive with <see cref="Marked"/>, and the first
    /// screen to mark a late hand-in would have to choose which truth to throw
    /// away.
    /// </description></item>
    /// </list>
    ///
    /// <see cref="Released"/> is terminal for the same reason
    /// <c>HomeworkStatus.Released</c> is (BR-LRN-012): the mark is Module 17's
    /// from the moment it lands there, and correcting it is a mark change under
    /// that module's change control, never a rewind here.
    /// </summary>
    public enum SubmissionStatus
    {
        /// <summary>Work is in. Whether it was on time is <see cref="HomeworkSubmission.IsLate"/>, not a different status.</summary>
        Submitted = 1,

        /// <summary>A score has been entered (BR-LRN-011). Not yet handed to Module 17.</summary>
        Marked = 2,

        /// <summary>BR-LRN-012: the raw mark has been written into Module 17's marksheet. Terminal here.</summary>
        Released = 3,
    }
}
