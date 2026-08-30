using Sms.Domain.Learning;

namespace Sms.Application.Learning
{
    /// <summary>
    /// Pure doc/Modules/37 §4 homework spine: draft -> issued -> collecting ->
    /// marking -> released, with withdrawal reachable from every state that is
    /// not already terminal.
    ///
    /// <para>
    /// Two edges are deliberately absent. There is no <c>Issued -> Draft</c>:
    /// BR-LRN-003 makes issue the event families see, so work leaves the portal
    /// by being withdrawn with a stated reason (BR-LRN-016), never by an
    /// un-publish that would make a task a student wrote down on Sunday vanish
    /// on Monday. And there is nothing out of <c>Released</c>: releasing hands a
    /// raw mark to Module 17 (BR-LRN-012), which then owns it — a correction is
    /// a mark change under Module 17's control, not a rewind here.
    /// </para>
    ///
    /// <para>
    /// Withdrawal is permitted from <c>Marking</c> by this table because the
    /// date-and-submission guard that actually restricts it (§9: blocked after
    /// the due date once submissions exist) needs data this pure engine does not
    /// have. The service applies that guard; this table answers only the
    /// question of shape.
    /// </para>
    /// </summary>
    public static class HomeworkStatusTransitions
    {
        public static bool CanTransition(HomeworkStatus from, HomeworkStatus to)
        {
            return (from, to) switch
            {
                (HomeworkStatus.Draft, HomeworkStatus.Issued) => true,
                (HomeworkStatus.Draft, HomeworkStatus.Withdrawn) => true,

                (HomeworkStatus.Issued, HomeworkStatus.Collecting) => true,
                (HomeworkStatus.Issued, HomeworkStatus.Marking) => true,
                (HomeworkStatus.Issued, HomeworkStatus.Withdrawn) => true,

                (HomeworkStatus.Collecting, HomeworkStatus.Marking) => true,
                (HomeworkStatus.Collecting, HomeworkStatus.Withdrawn) => true,

                (HomeworkStatus.Marking, HomeworkStatus.Released) => true,
                (HomeworkStatus.Marking, HomeworkStatus.Withdrawn) => true,

                _ => false,
            };
        }

        /// <summary>
        /// BR-LRN-003: only issued work is visible in the portal (BR-GLB-031,
        /// BR-SEC-012). Withdrawn work stops being visible; a draft never was.
        /// </summary>
        public static bool IsVisibleToPortal(HomeworkStatus status)
            => status is HomeworkStatus.Issued
                or HomeworkStatus.Collecting
                or HomeworkStatus.Marking
                or HomeworkStatus.Released;

        /// <summary>
        /// Whether the row still accepts student work. BR-LRN-005 keeps this
        /// true past the due date — lateness is flagged and penalised, never a
        /// closed door — so the answer turns on status alone, not on the clock.
        /// </summary>
        public static bool AcceptsSubmissions(HomeworkStatus status)
            => status is HomeworkStatus.Issued or HomeworkStatus.Collecting;
    }
}
