using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Payments
{
    /// <summary>
    /// BR-FEE-004 / BR-PAR-005: which of the children on a payer's card the school
    /// actually holds that payer responsible for. The payer model bills the
    /// financially-responsible parent, and BR-PAR-005 assigns that responsibility
    /// <em>per child</em> — divorced parents each covering specific children is the
    /// case the rule was written for — so a father can be the billed payer for one
    /// sibling and a stranger to the next one's invoice.
    /// <para>
    /// Anyone may hand money over: a grandfather settling his grandson's fees is
    /// ordinary, not an error. So this decides what the cashier screen must
    /// <em>say</em> before a receipt prints (doc/Modules/21 §8.1), never what it
    /// may refuse.
    /// </para>
    /// </summary>
    public static class PayerResponsibilityEvaluator
    {
        /// <summary>One live (not ended) guardian link, reduced to what responsibility depends on.</summary>
        public readonly struct GuardianLink
        {
            public GuardianLink(int studentId, int parentId, bool isFinanciallyResponsible)
            {
                StudentId = studentId;
                ParentId = parentId;
                IsFinanciallyResponsible = isFinanciallyResponsible;
            }

            public int StudentId { get; }

            public int ParentId { get; }

            public bool IsFinanciallyResponsible { get; }
        }

        /// <summary>
        /// True when <paramref name="parentId"/> is the guardian the school bills for this child.
        /// Two people are responsible for nobody and must not read as if they were: a payer with
        /// no parent behind it (BR-FEE-004's reserved sponsor path), and an ex-guardian whose link
        /// has been ended — the child keeps appearing on their card because the old charges still
        /// name them, which is exactly when a cashier is most likely to be misled.
        /// </summary>
        public static bool IsResponsibleFor(int? parentId, int studentId, IEnumerable<GuardianLink> liveLinks)
            => parentId != null
               && liveLinks.Any(l => l.ParentId == parentId.Value && l.StudentId == studentId && l.IsFinanciallyResponsible);

        /// <summary>
        /// The cashier's warning condition: the money is about to be receipted against somebody the
        /// school holds responsible for none of the children on the card. Allowed — it is simply not
        /// what the invoices say, and the screen has to admit that before the receipt prints rather
        /// than after. A card with no children at all states nothing either way.
        /// </summary>
        public static bool IsResponsibleForNothing(int? parentId, IReadOnlyCollection<int> childStudentIds, IEnumerable<GuardianLink> liveLinks)
        {
            if (childStudentIds.Count == 0)
            {
                return false;
            }

            var links = liveLinks as IReadOnlyCollection<GuardianLink> ?? liveLinks.ToList();
            return childStudentIds.All(id => !IsResponsibleFor(parentId, id, links));
        }
    }
}
