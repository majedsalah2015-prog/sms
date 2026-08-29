using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Sms.Web.Services
{
    /// <summary>
    /// Runs <c>tools/Sms.AccessBridge</c> — a 32-bit console program — and reads its JSON answer,
    /// for the machines where the in-process ODBC read cannot work.
    /// <para>
    /// The Access driver is registered per bitness. A school PC that has ever had Office on it has
    /// the 32-bit one and nothing else, while this application is AnyCPU and therefore runs 64-bit,
    /// so the driver is present, works in Access itself, and is invisible here. Installing the
    /// 64-bit Access Database Engine would fix it, and its installer refuses to run while 32-bit
    /// Office is present — which is exactly the machine that has this problem.
    /// </para>
    /// <para>
    /// One process per question rather than one long-lived one: the import asks three times across
    /// three page loads with a person thinking in between, so there is nothing to keep alive, and a
    /// process that ends cannot hold a lock on somebody's only register.
    /// </para>
    /// </summary>
    public static class AccessBridgeClient
    {
        /// <summary>Longer than any register anyone will import, short enough that a hung driver does not hold a request forever.</summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

        /// <summary>Set this to point at the executable when it does not sit anywhere <see cref="Locate"/> looks.</summary>
        private const string PathVariable = "SMS_ACCESS_BRIDGE";

        private static readonly Lazy<string?> Executable = new(Locate);

        /// <summary>Null when this machine has no bridge built or deployed — the caller then has nothing left to try.</summary>
        public static string? Path => Executable.Value;

        public static IReadOnlyList<string> ListTables(string filePath)
        {
            var answer = Run("tables", filePath, null);
            return Strings(answer, "tables");
        }

        /// <summary>
        /// Table names with row counts, degrading to names alone against a bridge that predates the
        /// verb. The executable is built and deployed separately from the application — it is 32-bit
        /// where the application is not, which is the whole reason it exists — so the two versions
        /// can and will drift on a school's server. A stale one must cost the counts, never the
        /// import: without this the screen would answer "Unknown verb: sizes" and list no tables at
        /// all. Anything else the bridge says is a real failure and still propagates.
        /// </summary>
        public static IReadOnlyList<(string Name, int? Rows)> ListTableSizes(string filePath)
        {
            JsonElement answer;
            try
            {
                answer = Run("sizes", filePath, null);
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("Unknown verb", StringComparison.OrdinalIgnoreCase))
            {
                return ListTables(filePath).Select(name => (name, (int?)null)).ToList();
            }

            var sizes = new List<(string, int?)>();
            if (!answer.TryGetProperty("sizes", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return sizes;
            }

            foreach (var element in array.EnumerateArray())
            {
                var name = element.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrEmpty(name)) { continue; }

                // A count the bridge could not take arrives as null and stays null here: the table
                // is still offered, just without a size beside it.
                int? rows = element.TryGetProperty("rows", out var r) && r.ValueKind == JsonValueKind.Number
                    ? r.GetInt32()
                    : null;
                sizes.Add((name!, rows));
            }

            return sizes;
        }

        public static IReadOnlyList<string> ListColumns(string filePath, string table)
        {
            var answer = Run("columns", filePath, table);
            return Strings(answer, "columns");
        }

        public static List<Dictionary<string, string?>> ReadRows(string filePath, string table)
        {
            var answer = Run("rows", filePath, table);
            var rows = new List<Dictionary<string, string?>>();
            if (!answer.TryGetProperty("rows", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return rows;
            }

            foreach (var element in array.EnumerateArray())
            {
                // Case-insensitive, like the in-process reader: a mapping chosen against one
                // spelling of a column name must not miss the cell because Access reported another.
                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    row[property.Name] = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString();
                }

                rows.Add(row);
            }

            return rows;
        }

        private static IReadOnlyList<string> Strings(JsonElement answer, string property) =>
            answer.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
                ? array.EnumerateArray().Select(e => e.GetString() ?? string.Empty).Where(s => s.Length > 0).ToList()
                : Array.Empty<string>();

        private static JsonElement Run(string verb, string filePath, string? table)
        {
            var exe = Path ?? throw new InvalidOperationException(
                "The 32-bit Access bridge is not built on this server.");

            var start = new ProcessStartInfo(exe)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add(verb);
            start.ArgumentList.Add(filePath);
            if (table != null) { start.ArgumentList.Add(table); }

            using var process = Process.Start(start) ?? throw new InvalidOperationException("The Access bridge would not start.");

            // Read before waiting. A register of a thousand rows fills the pipe buffer, and a child
            // blocked writing into a pipe nobody is draining is a deadlock, not a slow import.
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { /* already gone */ }
                throw new InvalidOperationException("Reading the Access file took too long and was stopped.");
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error) ? "The Access bridge returned nothing." : error.Trim());
            }

            var answer = JsonDocument.Parse(output).RootElement;
            if (!answer.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
            {
                throw new InvalidOperationException(
                    answer.TryGetProperty("error", out var message) ? message.GetString() ?? "The Access file could not be read." : "The Access file could not be read.");
            }

            return answer.Clone();
        }

        /// <summary>
        /// Where the executable is, in the order worth looking: an explicit setting, then beside the
        /// deployed application, then the build output a developer running from the repository would
        /// have. Null if none of them exists, which is a fact the caller must be able to state
        /// plainly rather than discover as a crash.
        /// </summary>
        private static string? Locate()
        {
            const string exeName = "Sms.AccessBridge.exe";

            var configured = Environment.GetEnvironmentVariable(PathVariable);
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            {
                return configured;
            }

            var beside = System.IO.Path.Combine(AppContext.BaseDirectory, "AccessBridge", exeName);
            if (File.Exists(beside))
            {
                return beside;
            }

            // Running from the repository: bin/<config>/net5.0 is four levels under src/Sms.Web, so
            // the repository root is somewhere above. Walk up rather than hard-coding the depth,
            // which changes with the configuration and the RID.
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 8 && directory != null; i++, directory = directory.Parent)
            {
                foreach (var configuration in new[] { "Release", "Debug" })
                {
                    var candidate = System.IO.Path.Combine(
                        directory.FullName, "tools", "Sms.AccessBridge", "bin", configuration, "net5.0", "win-x86", exeName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }
    }
}
