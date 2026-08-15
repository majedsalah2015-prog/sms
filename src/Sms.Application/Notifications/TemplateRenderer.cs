using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// Pure BR-NOT-001 placeholder substitution (e.g. "{studentName} was
    /// absent on {date}."). A payload key missing from the event's actual
    /// data leaves the token untouched rather than throwing — a malformed
    /// template should be visible in the delivered text, not crash the
    /// business transaction it rides (BR-NOT-002/009 spirit).
    /// </summary>
    public static class TemplateRenderer
    {
        private static readonly Regex PlaceholderToken = new(@"\{(\w+)\}", RegexOptions.Compiled);

        public static string Render(string template, IReadOnlyDictionary<string, string> payload)
        {
            return PlaceholderToken.Replace(template, match =>
                payload.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
        }
    }
}
