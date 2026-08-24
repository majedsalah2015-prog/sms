using System;
using Sms.Domain.Discounts;
using Sms.Domain.Workflow;

namespace Sms.Application.Discounts
{
    /// <summary>
    /// The one place WF-04's wiring names its own strings (doc 05 §5, BR-DIS-003).
    /// The seeded definition, the effects that apply it, and the grant desk that
    /// starts it all read them from here — three literals in three layers is how a
    /// chain quietly stops matching the records it is supposed to route.
    /// </summary>
    public static class DiscountWorkflow
    {
        /// <summary>doc 05 §5 — "Discount / scholarship grant".</summary>
        public const string Code = "WF-04";

        /// <summary>The entity an instance of this workflow is about.</summary>
        public const string EntityTypeName = nameof(DiscountGrant);

        /// <summary>
        /// BR-DIS-003's own bands, as the seeded chain uses them: at or under this
        /// percentage the finance manager is the whole chain; above it the request
        /// goes on to the principal.
        /// </summary>
        public const decimal FinanceManagerCeilingPercent = 10m;

        /// <summary>
        /// WF-04 is seeded against DiscountGrant, but an instance carries its own
        /// entity type and a later slice may route something else through the same
        /// code. An effect that assumed otherwise would apply a discount to whatever
        /// id it was handed.
        /// </summary>
        public static bool IsGrant(WorkflowInstance instance)
            => string.Equals(instance.EntityTypeName, EntityTypeName, StringComparison.Ordinal);
    }
}
