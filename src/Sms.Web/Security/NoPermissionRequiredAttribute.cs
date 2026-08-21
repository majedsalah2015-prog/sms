using System;

namespace Sms.Web.Security
{
    /// <summary>
    /// Declares that an action deliberately needs nothing beyond a signed-in
    /// user — a navigation shell, a language switch, a health ping. The reason is
    /// required and is the whole point: an action with no permission is a decision
    /// someone made, and it should read as one rather than as an omission.
    /// <para>
    /// The architecture test accepts this in place of
    /// <see cref="RequirePermissionAttribute"/> and accepts nothing else, so an
    /// action that simply forgot to declare anything fails the build instead of
    /// quietly staying open — which is how every screen in this system came to be
    /// reachable by any signed-in user in the first place.
    /// </para>
    /// <para>
    /// This is not <c>[AllowAnonymous]</c>: authentication still applies. It only
    /// says there is no screen permission to check.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class NoPermissionRequiredAttribute : Attribute
    {
        public NoPermissionRequiredAttribute(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("An action without a permission has to say why.", nameof(reason));
            }

            Reason = reason;
        }

        public string Reason { get; }
    }
}
