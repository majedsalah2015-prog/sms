using System;

namespace Sms.Domain.Audit
{
    /// <summary>
    /// The field holds a credential. Audit records <em>that</em> it changed and
    /// never <em>what</em> it changed to.
    /// <para>
    /// BR-NTF-003 has a school's gateway token stored encrypted; BR-NTF-006 has the
    /// configuration that carries it audited at field level. Taken together and left
    /// alone, the second undoes the first — <c>AuditCaptor</c> writes every changed
    /// property's old and new value, so every token rotation would deposit both
    /// ciphertexts in <c>aud.AuditEntry</c>, a table far more widely readable than the
    /// row they came from. Ciphertext is not a redaction: the key lives in the same
    /// deployment.
    /// </para>
    /// <para>
    /// So the entry is still written — who rotated the token, and when, is the point of
    /// auditing it — with both values replaced by <see cref="Redaction"/>. Put this on
    /// any property whose value would be a credential if read: a token, a password
    /// hash, an API secret.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class SecretFieldAttribute : Attribute
    {
        /// <summary>What stands in for the value in the audit trail. Not a value any cipher can produce.</summary>
        public const string Redaction = "(secret)";
    }
}
