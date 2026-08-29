using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Linq;

namespace Sms.Web.Services
{
    /// <summary>
    /// Reads a school's old Access register (.mdb / .accdb) so its students can be brought across
    /// once, rather than retyped.
    /// <para>
    /// ODBC rather than OLEDB: the ACE ODBC driver is the one that turns out to be registered on a
    /// Windows machine that has ever had Office on it, while the OLEDB provider frequently is not.
    /// Both are 32/64-bit sensitive, so a driver that is installed but of the wrong bitness reports
    /// itself as missing. That is the ordinary case on a school machine — 32-bit Office, 64-bit
    /// application — so a missing driver is not the end of the road: the read goes out to
    /// <see cref="AccessBridgeClient"/>, a 32-bit console program, and only the machine with
    /// neither gets <see cref="MissingDriverMessage"/>.
    /// </para>
    /// <para>
    /// Read-only and nothing else: the file is opened, listed, read and closed. Nothing is written
    /// back to a register that is somebody's only copy of their history.
    /// </para>
    /// </summary>
    public static class AccessRegisterReader
    {
        private const string DriverName = "Microsoft Access Driver (*.mdb, *.accdb)";

        /// <summary>
        /// Whether this process can open an Access file itself. Probed once: asking the driver
        /// manager is cheap, but asking it on every cell of every step of a three-step import is
        /// not, and the answer cannot change while the application is running.
        /// </summary>
        private static readonly Lazy<bool> InProcessDriver = new(Probe);

        /// <summary>
        /// True when the read has to go out to the 32-bit helper — the ordinary case on a school
        /// machine with 32-bit Office, where the driver exists but not in this process's bitness.
        /// </summary>
        private static bool UseBridge => !InProcessDriver.Value && AccessBridgeClient.Path != null;

        /// <summary>
        /// Opens a path that is not there. A driver that exists answers "file not found"; one that
        /// does not answers IM002, which is the only thing being asked.
        /// </summary>
        private static bool Probe()
        {
            try
            {
                using var connection = new OdbcConnection(
                    $"Driver={{{DriverName}}};Dbq={Path.Combine(Path.GetTempPath(), "sms-access-driver-probe.accdb")};ReadOnly=1;");
                connection.Open();
                return true;
            }
            catch (OdbcException ex)
            {
                return !IsMissingDriver(ex);
            }
            catch (Exception)
            {
                // Anything else came from the file, not the driver manager: the driver is there.
                return true;
            }
        }

        /// <summary>
        /// True when the failure was "there is no such driver" rather than anything about the file.
        /// IM002 is ODBC's own code for it; the message check covers drivers that report the same
        /// condition under a different state.
        /// </summary>
        public static bool IsMissingDriver(OdbcException exception)
        {
            for (var i = 0; i < exception.Errors.Count; i++)
            {
                if (string.Equals(exception.Errors[i].SQLState, "IM002", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return exception.Message.Contains("Data source name not found", StringComparison.OrdinalIgnoreCase)
                || exception.Message.Contains("no default driver", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reached only when neither road is open: no driver of this process's bitness, and no
        /// 32-bit bridge built either. Names both ways out, because on a machine with 32-bit Office
        /// the first one — installing the 64-bit engine — is the one its installer will refuse.
        /// </summary>
        public static string MissingDriverMessage(bool arabic) => arabic
            ? "لا يوجد مشغّل Access يمكن لهذا التطبيق استخدامه: المشغّل المثبَّت بمعمارية مختلفة عن معمارية التطبيق (٦٤‑بت). الحل: إمّا بناء الوسيط ٣٢‑بت بالأمر dotnet build tools/Sms.AccessBridge -c Release، أو تثبيت «Microsoft Access Database Engine 2016 Redistributable» بنسخة ٦٤‑بت — ويرفض مثبِّتها العمل عادةً إن كان Office ٣٢‑بت مثبَّتاً."
            : "No Access driver this application can use is installed: the one on this machine is of a different bitness from the application (64-bit). Either build the 32-bit bridge with \"dotnet build tools/Sms.AccessBridge -c Release\", or install the 64-bit \"Microsoft Access Database Engine 2016 Redistributable\" — whose installer normally refuses to run while 32-bit Office is present.";

        /// <summary>The user tables in the file — Access's own MSys* bookkeeping is not one of them.</summary>
        public static IReadOnlyList<string> ListTables(string filePath)
        {
            if (UseBridge) { return AccessBridgeClient.ListTables(filePath); }

            using var connection = Open(filePath);
            var schema = connection.GetSchema("Tables");
            return schema.Rows.Cast<DataRow>()
                .Where(r => string.Equals(Convert.ToString(r["TABLE_TYPE"]), "TABLE", StringComparison.OrdinalIgnoreCase))
                .Select(r => Convert.ToString(r["TABLE_NAME"]) ?? string.Empty)
                .Where(n => n.Length > 0 && !n.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Column names of one table, in the order Access holds them.</summary>
        public static IReadOnlyList<string> ListColumns(string filePath, string table)
        {
            if (UseBridge) { return AccessBridgeClient.ListColumns(filePath, table); }

            using var connection = Open(filePath);
            using var command = new OdbcCommand($"SELECT * FROM [{Sanitize(table)}]", connection);
            using var reader = command.ExecuteReader(CommandBehavior.SchemaOnly);
            return Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        }

        /// <summary>
        /// Every row of one table as strings keyed by column name. Strings because the destination
        /// parses them anyway and an Access column's declared type is a poor promise about what is
        /// actually in it — a date column full of text is the normal case, not the exception.
        /// </summary>
        public static List<Dictionary<string, string?>> ReadRows(string filePath, string table, int? limit = null)
        {
            if (UseBridge) { return AccessBridgeClient.ReadRows(filePath, table); }

            using var connection = Open(filePath);
            using var command = new OdbcCommand($"SELECT * FROM [{Sanitize(table)}]", connection);
            using var reader = command.ExecuteReader();

            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            var rows = new List<Dictionary<string, string?>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < columns.Count; i++)
                {
                    if (reader.IsDBNull(i)) { row[columns[i]] = null; continue; }

                    // A date has to survive the trip as an unambiguous string, so it is formatted
                    // rather than left to whatever the thread's culture would have printed.
                    var value = reader.GetValue(i);
                    row[columns[i]] = value is DateTime date
                        ? date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                        : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                }

                rows.Add(row);
                if (limit is int max && rows.Count >= max) { break; }
            }

            return rows;
        }

        /// <summary>
        /// One of the register's little code tables — <c>qualification_code</c>, <c>job_code</c>,
        /// <c>relation_code</c> — as code → name.
        /// <para>
        /// Which column is which is worked out rather than asked: these tables are two columns wide
        /// and have been called <c>code</c>/<c>name</c> since the nineties. Asking the operator to
        /// nominate them would be three more pickers for a question that answers itself, and a wrong
        /// answer is visible in the preview a moment later either way.
        /// </para>
        /// </summary>
        public static IReadOnlyDictionary<string, string> ReadCodeMap(string filePath, string table)
        {
            var rows = ReadRows(filePath, table);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (rows.Count == 0)
            {
                return map;
            }

            var columns = rows[0].Keys.ToList();
            var key = columns.FirstOrDefault(c => Named(c, "code", "id", "no")) ?? columns.FirstOrDefault() ?? string.Empty;
            var label = columns.FirstOrDefault(c => c != key && Named(c, "name", "title", "desc", "ar"))
                ?? columns.FirstOrDefault(c => c != key)
                ?? key;

            foreach (var row in rows)
            {
                var code = (row.TryGetValue(key, out var k) ? k : null)?.Trim();
                var name = (row.TryGetValue(label, out var v) ? v : null)?.Trim();
                if (!string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(name))
                {
                    map[code] = name;
                }
            }

            return map;
        }

        private static bool Named(string column, params string[] words) =>
            words.Any(w => column.Contains(w, StringComparison.OrdinalIgnoreCase));

        private static OdbcConnection Open(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException("The uploaded file is no longer on the server; upload it again.");
            }

            var connection = new OdbcConnection($"Driver={{{DriverName}}};Dbq={filePath};ReadOnly=1;");
            connection.Open();
            return connection;
        }

        /// <summary>
        /// A table name goes into the SQL text because ODBC cannot parameterise an identifier. The
        /// name never comes from a URL — it is chosen from the list this class itself produced — but
        /// a closing bracket would still break out of the quoting, so it cannot be allowed through.
        /// </summary>
        private static string Sanitize(string table)
        {
            if (string.IsNullOrWhiteSpace(table) || table.Contains(']') || table.Contains('[') || table.Contains(';'))
            {
                throw new InvalidOperationException("That table name cannot be read.");
            }

            return table;
        }
    }
}
