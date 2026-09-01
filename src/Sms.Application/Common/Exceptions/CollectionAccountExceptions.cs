using System;
using Sms.Domain.Payments;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>
    /// The school keeps at least one active account of the kind this method
    /// collects into, and the capture named none of them (doc/Modules/21 §9
    /// "method details mandatory per method").
    /// <para>
    /// Conditional on purpose. A school that has not defined a single account
    /// yet must still be able to take money at the counter on its first
    /// morning, so an empty catalogue leaves the field optional; the moment the
    /// catalogue holds an account, leaving it blank is an omission rather than
    /// an absence and is refused.
    /// </para>
    /// </summary>
    public class CollectionAccountRequiredException : InvalidOperationException
    {
        public CollectionAccountRequiredException(PaymentMethod method, CollectionAccountKind kind)
            : base($"Payment method '{method}' must name the {kind} account it was collected into.")
        {
            Method = method;
            Kind = kind;
        }

        public PaymentMethod Method { get; }

        public CollectionAccountKind Kind { get; }
    }

    /// <summary>
    /// A cash payment was pointed at a bank account, or a transfer at a cash
    /// box. Both are the same mistake — money recorded into a pot it never
    /// reached — and both make the day's reconciliation false.
    /// </summary>
    public class CollectionAccountMethodMismatchException : InvalidOperationException
    {
        public CollectionAccountMethodMismatchException(PaymentMethod method, CollectionAccountKind required, CollectionAccountKind actual)
            : base($"Payment method '{method}' collects into a {required} account, but the account given is a {actual}.")
        {
            Method = method;
            Required = required;
            Actual = actual;
        }

        public PaymentMethod Method { get; }

        public CollectionAccountKind Required { get; }

        public CollectionAccountKind Actual { get; }
    }

    /// <summary>
    /// The account named has been retired. Its old receipts keep pointing at it
    /// — that is what the soft-active filter is for — but no new money is
    /// collected into a closed account.
    /// </summary>
    public class CollectionAccountInactiveException : InvalidOperationException
    {
        public CollectionAccountInactiveException(string code)
            : base($"Collection account '{code}' is no longer active.")
        {
            Code = code;
        }

        public string Code { get; }
    }

    /// <summary>
    /// The capture named an account this school does not have. Reachable only
    /// by a tampered post — the picker offers the school's own accounts — but
    /// worth refusing by name rather than treating an unknown id as "none
    /// given", which would file the money against no account at all.
    /// </summary>
    public class CollectionAccountNotFoundException : InvalidOperationException
    {
        public CollectionAccountNotFoundException(int collectionAccountId)
            : base($"Collection account {collectionAccountId} does not belong to this school.")
        {
            CollectionAccountId = collectionAccountId;
        }

        public int CollectionAccountId { get; }
    }

    /// <summary>Codes are how an operator refers to an account out loud; two of one code is two accounts nobody can tell apart.</summary>
    public class DuplicateCollectionAccountCodeException : InvalidOperationException
    {
        public DuplicateCollectionAccountCodeException(string code)
            : base($"A collection account with code '{code}' already exists.")
        {
            Code = code;
        }

        public string Code { get; }
    }

    /// <summary>
    /// A bank account with neither an account number nor an IBAN answers none
    /// of the questions it exists to answer — the parent still cannot be told
    /// where to send the money.
    /// </summary>
    public class BankCollectionAccountNeedsNumberException : InvalidOperationException
    {
        public BankCollectionAccountNeedsNumberException()
            : base("A bank collection account needs an account number or an IBAN.")
        {
        }
    }
}
