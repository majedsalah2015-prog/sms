using System;
using Sms.Domain.Common;

namespace Sms.Domain.Installments
{
    /// <summary>
    /// ppl.CollectionNotice (doc/Modules/20 §8.5, BR-INS-008/010, BR-GLB-102):
    /// one arrears notice a human decided to issue, to one student's responsible
    /// guardian, for one due-date window.
    /// <para>
    /// <b>Why this is not a <see cref="DunningEvent"/>.</b> That row is the
    /// automatic ladder's memory, and <c>DunningLadderEvaluator</c> reads its
    /// highest fired step as the floor for the next one. Writing a manually
    /// issued letter into it would make the ladder skip every rung below —
    /// an officer printing a reminder in week one would silently cancel the
    /// +3/+14/+30 notices that BR-INS-008 requires. So the human act gets its
    /// own append-only log and the ladder is left alone.
    /// </para>
    /// <para>
    /// Append-only, and therefore never <c>[Audited]</c> — the row *is* the
    /// record of the send (BR-NOT-006, BR-GLB-102's "retained and auditable"),
    /// and auditing a log is circular. Nothing amends a notice: a wrong one is
    /// followed by a right one, which is what a school's paper trail does too.
    /// </para>
    /// <para>
    /// The amount is snapshotted rather than recomputed. A statement that says
    /// "as at 1 March you owed 4,200" must keep saying that after the family
    /// pays, or the letter in their hand stops matching the system that sent it.
    /// </para>
    /// </summary>
    public class CollectionNotice : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        /// <summary>Series DUN (doc 08). Numbered because BR-INS-008's letter stage is a formal document a family may bring back.</summary>
        public string NoticeNo { get; set; } = string.Empty;

        public int StudentId { get; set; }

        /// <summary>
        /// The payer billed for the arrears — BR-PAR-005's financially responsible
        /// guardian, resolved at issue time. Null when the school has made nobody
        /// responsible: the notice still exists (someone printed it) but it was
        /// never addressed, and the roll says so rather than inventing a payer.
        /// </summary>
        public int? PayerId { get; set; }

        public CollectionNoticeChannel Channel { get; set; }

        /// <summary>Inclusive start of the due-date window the notice was raised over. Null means "everything up to <see cref="WindowTo"/>".</summary>
        public DateTime? WindowFrom { get; set; }

        /// <summary>Inclusive end of that window.</summary>
        public DateTime? WindowTo { get; set; }

        /// <summary>What was outstanding in that window when the notice was issued (BR-GLB-060 decimal).</summary>
        public decimal AmountDue { get; set; }

        public DateTime IssuedAtUtc { get; set; }
    }
}
