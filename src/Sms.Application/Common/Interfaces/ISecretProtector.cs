namespace Sms.Application.Common.Interfaces
{
    /// <summary>
    /// Turns a credential into something safe to keep in a database column, and
    /// back again at the moment of use.
    /// <para>
    /// BR-NTF-003 requires a school's gateway credentials to be stored encrypted.
    /// The port is here rather than the mechanism because the mechanism belongs to
    /// the host — ASP.NET Core's data protection, keyed off the deployment's own key
    /// ring — and <c>Sms.Application</c> may not see it.
    /// </para>
    /// <para>
    /// <b>What this is not.</b> It is reversible, deliberately: a token has to be
    /// presented to the gateway in the clear to be of any use, which is what separates
    /// it from a password. Never route a password through here —
    /// <see cref="IPasswordHasher"/> exists for the values that must never come back.
    /// </para>
    /// </summary>
    public interface ISecretProtector
    {
        /// <summary>The value as it may be stored. Never the value itself.</summary>
        string Protect(string plaintext);

        /// <summary>
        /// The original value, or null when the cipher cannot be read — a key ring
        /// rotated or restored from another machine leaves ciphertext nothing can open,
        /// and a school should be told to re-enter its token rather than shown a crash.
        /// </summary>
        string? Unprotect(string? cipher);
    }
}
