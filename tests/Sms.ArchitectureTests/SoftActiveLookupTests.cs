using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Sms.ArchitectureTests
{
    /// <summary>
    /// Catches one mistake, which has now been made three times in three
    /// different modules and taken a screen down each time.
    /// <para>
    /// Soft-active master data — a fee category, a subject, a grade level, a
    /// stage — is hidden by a global query filter once deactivated. The records
    /// that point at it are not: a posted charge keeps its category, an offering
    /// keeps its subject, an enrollment keeps its grade. Load the master list
    /// through the filter, then join it with <c>First(x =&gt; x.Id == row.SomethingId)</c>,
    /// and the day somebody retires one row the page answers
    /// "Sequence contains no matching element" and shows a stack trace.
    /// </para>
    /// <para>
    /// It read as safe every time. The filter is invisible at the call site, the
    /// join is obviously correct, and the failure needs data nobody has in
    /// development. So the rule is enforced from outside: a controller that
    /// loads one of these lists without <c>IgnoreQueryFilters</c> may not then
    /// look rows up in it by id with <c>First</c>. Use <c>IgnoreQueryFilters</c>
    /// for the lookup and keep the filtered list for the picker, which is a
    /// different list answering a different question.
    /// </para>
    /// </summary>
    public class SoftActiveLookupTests
    {
        /// <summary>The sets whose rows outlive their own deactivation.</summary>
        private static readonly string[] SoftActiveSets = { "GradeLevels", "Stages", "Subjects", "FeeCategories", "CollectionAccounts" };

        [Fact]
        public void No_controller_looks_a_row_up_by_id_in_a_soft_active_list_it_loaded_through_the_filter()
        {
            var offenders = new List<string>();

            foreach (var file in Directory.EnumerateFiles(ControllersDirectory(), "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);

                // "var subjects = await _db.Subjects.AsNoTracking()…" — the variable, and whether the
                // statement that produced it read past the filter.
                foreach (Match load in Regex.Matches(
                    source,
                    @"var\s+(?<name>\w+)\s*=\s*await\s+_db\.(?<set>" + string.Join("|", SoftActiveSets) + @")\b(?<rest>[^;]*);",
                    RegexOptions.Singleline))
                {
                    if (load.Groups["rest"].Value.Contains("IgnoreQueryFilters", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var name = load.Groups["name"].Value;

                    // …then used as name.First(x => x.Id == something). FirstOrDefault is fine: it
                    // says out loud that the row might not be there.
                    var lookup = new Regex(@"\b" + Regex.Escape(name) + @"\.First\(\s*(?<p>\w+)\s*=>\s*\k<p>\.Id\s*==");
                    if (lookup.IsMatch(source))
                    {
                        offenders.Add($"{Path.GetFileName(file)}: '{name}' from _db.{load.Groups["set"].Value} without IgnoreQueryFilters, then First(x => x.Id == …)");
                    }
                }
            }

            Assert.True(offenders.Count == 0,
                "These lookups throw the day somebody deactivates one of the rows they join to:"
                + Environment.NewLine + string.Join(Environment.NewLine, offenders.Distinct().OrderBy(x => x)));
        }

        private static string ControllersDirectory()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Sms.Web", "Controllers")))
            {
                dir = dir.Parent;
            }

            Assert.NotNull(dir);
            return Path.Combine(dir!.FullName, "src", "Sms.Web", "Controllers");
        }
    }
}
