using System;

namespace Sms.Domain.Audit
{
    /// <summary>
    /// Marks a T1 field whose change requires a mandatory reason (doc 07 §3).
    /// Saving a change to this field without an ambient audit reason fails the
    /// whole transaction.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class RequiresAuditReasonAttribute : Attribute
    {
    }
}
