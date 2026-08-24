using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// An Arabic screen must never show an English refusal, and this is what keeps that true.
    /// <para>
    /// Domain and Application exceptions carry English messages on purpose — a log line should read
    /// the same in every deployment. The defect is printing one at a user. It is invisible in
    /// English, invisible in a test that only asserts a save was refused, and it reaches the person
    /// least able to act on it: an Arabic-speaking administrator who is told, in a language they may
    /// not read, that something they did was rejected.
    /// </para>
    /// <para>
    /// It spreads the way every convention gap spreads — the next controller is written by copying
    /// the last one. So it is enforced the same way screen permissions and help panels are: by a red
    /// build. A controller may surface a refusal only through <c>UserMessage.For(ex, IsArabic)</c>,
    /// which translates what it knows and falls back to the original text for what it does not.
    /// </para>
    /// <para>
    /// The scan reads the controller sources rather than reflecting over the compiled types, because
    /// what is being asserted is what the author wrote in the catch block — the compiled method body
    /// has no shape to inspect.
    /// </para>
    /// </summary>
    public class TranslatedRefusalTests
    {
        /// <summary>Printing an engine exception's own sentence at a user, in whatever language it happens to be.</summary>
        private static readonly Regex RawMessage = new(@"\bex(?:ception)?\.Message\b", RegexOptions.Compiled);

        private static string ControllersDirectory([CallerFilePath] string thisFile = "")
        {
            var repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
            return Path.Combine(repo, "src", "Sms.Web", "Controllers");
        }

        public static IEnumerable<object[]> Controllers()
            => Directory.EnumerateFiles(ControllersDirectory(), "*.cs", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new object[] { Path.GetFileName(path) });

        [Theory]
        [MemberData(nameof(Controllers))]
        public void No_controller_prints_an_engine_message_at_the_user(string fileName)
        {
            var path = Path.Combine(ControllersDirectory(), fileName);
            var lines = File.ReadAllLines(path);

            var offenders = lines
                .Select((text, index) => (Text: text.Trim(), Line: index + 1))
                .Where(l => RawMessage.IsMatch(l.Text) && !l.Text.StartsWith("//", StringComparison.Ordinal))
                .ToList();

            Assert.True(
                offenders.Count == 0,
                $"{fileName} surfaces an engine exception's own message at line(s) " +
                $"{string.Join(", ", offenders.Select(o => o.Line))}. Engine messages are English by design; " +
                "route them through UserMessage.For(ex, IsArabic) so an Arabic user reads Arabic.");
        }

        /// <summary>
        /// The other half of the same rule: a controller that catches a refusal has to say something
        /// about it. A silent catch is not a translation problem, it is a worse one.
        /// </summary>
        [Theory]
        [MemberData(nameof(Controllers))]
        public void Every_controller_that_translates_a_refusal_can_reach_the_translator(string fileName)
        {
            var path = Path.Combine(ControllersDirectory(), fileName);
            var source = File.ReadAllText(path);
            if (!source.Contains("UserMessage.For", StringComparison.Ordinal))
            {
                return;
            }

            Assert.True(
                source.Contains("using Sms.Web.Models;", StringComparison.Ordinal),
                $"{fileName} calls UserMessage.For without importing Sms.Web.Models.");
            Assert.True(
                source.Contains("IsArabic", StringComparison.Ordinal),
                $"{fileName} calls UserMessage.For but defines no IsArabic — the translation would be decided by nothing.");
        }
    }
}
