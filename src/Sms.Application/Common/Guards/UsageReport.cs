using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Common.Guards
{
    /// <summary>
    /// One thing that references a record, and how many of them there are.
    /// <para>
    /// Bilingual by construction rather than by a lookup key: the resource name
    /// is the only part of a "cannot delete" message that varies, and carrying
    /// both languages with it keeps the reason legible without a resource file
    /// the modules do not otherwise use.
    /// </para>
    /// </summary>
    public sealed record UsageReference(string ResourceEn, string ResourceAr, int Count);

    /// <summary>
    /// What a record is used by. Empty means nothing references it.
    /// <para>
    /// The answer to "may I delete this?" is a <b>list of reasons</b>, not a
    /// boolean, because a screen that can only say "no" forces the operator to
    /// guess what to clear first. Every refusal in this system that says merely
    /// "in use" is a support call.
    /// </para>
    /// </summary>
    public sealed class UsageReport
    {
        public static readonly UsageReport Free = new(Array.Empty<UsageReference>());

        public UsageReport(IReadOnlyList<UsageReference> references) => References = references;

        public IReadOnlyList<UsageReference> References { get; }

        public bool IsInUse => References.Count > 0;

        /// <summary>Reads as "2 assigned plans, 1 rollover batch" — the list a refusal message needs.</summary>
        public string Describe(bool arabic) => string.Join(
            arabic ? "، " : ", ",
            References.Select(r => $"{r.Count} {(arabic ? r.ResourceAr : r.ResourceEn)}"));

        public static UsageReport From(params UsageReference[] references)
            => new(references.Where(r => r.Count > 0).ToList());
    }

    /// <summary>
    /// Answers what would break if one record went away.
    /// <para>
    /// <b>Not a permission check.</b> Permissions ask whether <i>this user</i> may
    /// act; this asks whether the <i>record</i> can be acted on at all, and no
    /// role overrides it — deleting a fee category that charges point at is not a
    /// privilege, it is a broken ledger. The two are asked together at every
    /// destructive action and answer different questions.
    /// </para>
    /// <para>
    /// Implemented once per aggregate that something else can reference. The
    /// pattern already existed nine times over as a private check behind an
    /// <c>InUseException</c>; the difference here is that a screen can ask
    /// <i>before</i> offering the action, so a delete button that cannot work is
    /// never drawn.
    /// </para>
    /// </summary>
    public interface IUsageInspector<T>
        where T : class
    {
        Task<UsageReport> InspectAsync(int id, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Raised when a destructive action is attempted on a record something else
    /// references. Carries the report so the message can name what is in the way
    /// rather than only that something is.
    /// </summary>
    public class RecordInUseException : InvalidOperationException
    {
        public RecordInUseException(UsageReport usage)
            : base($"The record is in use: {usage.Describe(arabic: false)}.")
        {
            Usage = usage;
        }

        public UsageReport Usage { get; }
    }

    /// <summary>
    /// Raised when a record is removed without saying why (BR-GLB-032: a record that goes away
    /// carries a mandatory reason).
    /// <para>
    /// It is a separate refusal from the usage guard above because the two answer different
    /// questions — "may this be removed at all" and "is it being removed accountably" — and a
    /// screen that conflated them would tell an operator to clear a reference they do not have.
    /// </para>
    /// </summary>
    public class MissingRemovalReasonException : InvalidOperationException
    {
        public MissingRemovalReasonException(string entityType)
            : base($"Removing a {entityType} requires a reason (BR-GLB-032).")
        {
            EntityType = entityType;
        }

        public string EntityType { get; }
    }
}
