using System;
using System.Collections.Generic;
using Sms.Application.Security;
using Sms.Domain.Security;

namespace Sms.Web.Security
{
    /// <summary>
    /// The wire format of the role designer's permission grid: one <c>MODULE/Screen/Verb</c> string
    /// per ticked box.
    /// <para>
    /// Its own type rather than a method on the controller because it is the single place a checkbox
    /// and a <c>sec.Permission</c> row meet — get it wrong and the screen grants something other than
    /// what was ticked, which no other test in the system would catch.
    /// </para>
    /// </summary>
    public static class PermissionGrid
    {
        public static string Value(string moduleCode, string screenCode, ActionVerb action) =>
            $"{moduleCode}/{screenCode}/{action}";

        /// <summary>
        /// The ticked boxes as permission keys.
        /// <para>
        /// A missing field means "nothing is ticked", not "no change" — the grid always posts its
        /// whole state, and the difference is a role somebody believes they emptied and did not.
        /// </para>
        /// <para>
        /// A malformed entry is dropped rather than thrown on: the values come from checkboxes this
        /// application rendered, so a bad one is tampering, and throwing would let a crafted post turn
        /// the save into a 500. Nothing is lost by dropping it — the service refuses any triple the
        /// screen catalogue does not define anyway.
        /// </para>
        /// </summary>
        public static IReadOnlyCollection<PermissionKey> Parse(string[]? granted)
        {
            if (granted == null)
            {
                return Array.Empty<PermissionKey>();
            }

            var keys = new List<PermissionKey>();
            foreach (var raw in granted)
            {
                var parts = (raw ?? string.Empty).Split('/');
                if (parts.Length == 3 && Enum.TryParse<ActionVerb>(parts[2], out var verb))
                {
                    keys.Add(new PermissionKey(parts[0], parts[1], verb));
                }
            }

            return keys;
        }
    }
}
