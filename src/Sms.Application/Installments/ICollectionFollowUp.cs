using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.ReadModels;
using Sms.Domain.Installments;

namespace Sms.Application.Installments
{
    /// <summary>
    /// What the collection roll was asked for. Every field narrows; all of them
    /// are optional, and an empty filter means "everyone who owes anything, ever"
    /// — which is a real question on the first of the month.
    /// </summary>
    /// <param name="From">Inclusive start of the due-date window.</param>
    /// <param name="To">Inclusive end of it.</param>
    /// <param name="AcademicYearId">Whose enrolment decides the grade and section shown. Defaults to the working year.</param>
    /// <param name="GradeLevelId">doc/Modules/20 §10 "by grade".</param>
    /// <param name="SectionId">doc/Modules/19 §10 "by section".</param>
    /// <param name="Query">Student number, or any part of a name in either language.</param>
    /// <param name="Bucket">doc/Modules/20 §10 "by bucket", aged by the oldest unpaid due date in the window.</param>
    /// <param name="NotifiableOnly">Drop families whose whole arrears are covered by a post-dated cheque (BR-INS-009) — the list to actually chase.</param>
    public sealed record CollectionFilter(
        DateTime? From = null,
        DateTime? To = null,
        int? AcademicYearId = null,
        int? GradeLevelId = null,
        int? SectionId = null,
        string? Query = null,
        AgingBucket? Bucket = null,
        bool NotifiableOnly = false);

    /// <summary>
    /// One student on the roll, carrying the names the screen prints rather than
    /// the ids it would have to resolve. Both languages travel together: the same
    /// row is rendered to an Arabic screen, an English CSV and a printed letter,
    /// and re-querying per surface is how one of the three ends up in the wrong
    /// language.
    /// </summary>
    public sealed record CollectionRow(
        int StudentId,
        string StudentNo,
        string StudentNameAr,
        string StudentNameEn,
        string? GradeNameAr,
        string? GradeNameEn,
        string? SectionNameAr,
        string? SectionNameEn,
        int? PayerId,
        string? GuardianNameAr,
        string? GuardianNameEn,
        string? GuardianMobile,
        bool GuardianIsResponsible,
        bool GuardianHasPortalAccount,
        WindowPosition Position,
        AgingBucket Bucket,
        DateTime? LastNoticeAtUtc,
        CollectionNoticeChannel? LastNoticeChannel);

    /// <summary>
    /// The roll, plus what it is not showing. Both totals are over everything the
    /// filter matched rather than over the returned page, so the figure under a
    /// truncated grid is still the school's. <paramref name="MatchCount"/> counts
    /// every student the filter matched and <paramref name="Rows"/> holds at most a
    /// page of them — stated separately because a truncated arrears list that reads
    /// as a complete one is how a family gets missed for a term.
    /// </summary>
    public sealed record CollectionRoll(
        IReadOnlyList<CollectionRow> Rows, int MatchCount, bool IsTruncated, decimal TotalOutstanding, decimal TotalNotifiable);

    /// <summary>One notice, with the row it was raised from — so a print run needs no second query.</summary>
    public sealed record IssuedNotice(CollectionNotice Notice, CollectionRow Row);

    /// <summary>
    /// What a notice run did, and what it declined to do. The skips are counted
    /// rather than swallowed: an officer who selected thirty families and reached
    /// twenty-two is owed the reason for the other eight, and "issued 22" alone
    /// reads as success.
    /// </summary>
    /// <param name="Issued">The notices raised, in roll order.</param>
    /// <param name="SkippedNothingOutstanding">Selected, but the window balance had already been settled.</param>
    /// <param name="SkippedPdcCovered">BR-INS-009: a post-dated cheque already covers the whole window balance.</param>
    /// <param name="SkippedNoPortalAccount">Portal channel only: no guardian with a portal sign-in to receive it.</param>
    public sealed record NoticeBatch(
        IReadOnlyList<IssuedNotice> Issued,
        int SkippedNothingOutstanding,
        int SkippedPdcCovered,
        int SkippedNoPortalAccount);

    /// <summary>
    /// doc/Modules/20 §8.5's collection follow-up — the escalation list and the
    /// letter batches that <c>DunningStep</c> describes as "human-gated ... a
    /// Finance Manager confirms letter batches (screens deferred)", together with
    /// §10's "Overdue installments by payer/grade/bucket" and doc/Modules/19 §10's
    /// aged receivables, read from the student's side.
    /// <para>
    /// Separate from <see cref="IInstallmentAdmin"/> on purpose. That port owns the
    /// schedule and the automatic ladder; this one answers a question across
    /// schedules — and across families with no schedule at all, whose posted
    /// charges are aged by posting date exactly as
    /// <c>SnapshotRefreshService.RefreshAgedReceivablesAsync</c> ages them. A
    /// school that never adopted installment plans still has arrears, and a roll
    /// built only from <c>ppl.Installment</c> would show it an empty screen.
    /// </para>
    /// <para>
    /// Standalone shape (see CLAUDE.md): <see cref="IssueNoticesAsync"/> saves
    /// itself. It is not riding anyone's transaction — the officer pressed a
    /// button — and the notification rows it publishes must commit with the notice
    /// log that records them.
    /// </para>
    /// </summary>
    public interface ICollectionFollowUp
    {
        /// <summary>
        /// The students with money outstanding in the window, newest arrears last.
        /// <para>
        /// Throws <see cref="Common.Exceptions.InvalidCollectionWindowException"/>
        /// when the window runs backwards — an inverted range otherwise returns an
        /// empty arrears screen, which reads as "nobody owes anything".
        /// </para>
        /// </summary>
        Task<CollectionRoll> GetRollAsync(
            CollectionFilter filter, int take = OutstandingWindowEvaluator.DefaultPageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Issues one arrears notice per selected student to the guardian the school
        /// bills (BR-PAR-005), logs each as an append-only <see cref="CollectionNotice"/>
        /// under series DUN, and — for
        /// <see cref="CollectionNoticeChannel.Portal"/> — publishes
        /// <c>InstallmentOverdue</c> through doc 09's engine so it lands in the
        /// family's in-app inbox.
        /// <para>
        /// Skips rather than throws for a family that cannot be reached, and reports
        /// each skip's reason in the returned <see cref="NoticeBatch"/>. BR-INS-009
        /// suppresses a notice whose whole window balance is covered by a post-dated
        /// cheque. Writes no <see cref="DunningEvent"/>: the automatic ladder reads
        /// its own highest fired step as a floor, and a manual letter recorded there
        /// would cancel every rung below it.
        /// </para>
        /// <para>
        /// Throws <see cref="Common.Exceptions.InvalidCollectionWindowException"/>
        /// on a backwards window, for the same reason the roll does — the window is
        /// snapshotted onto every notice it raises.
        /// </para>
        /// </summary>
        Task<NoticeBatch> IssueNoticesAsync(
            IReadOnlyCollection<int> studentIds,
            CollectionNoticeChannel channel,
            CollectionFilter window,
            CancellationToken cancellationToken = default);
    }
}
