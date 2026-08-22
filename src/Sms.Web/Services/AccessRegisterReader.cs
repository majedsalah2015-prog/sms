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
    /// itself as missing — which is why <see cref="DriverAvailable"/> exists and why its message
    /// says which bitness to install.
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

        public static string MissingDriverMessage(bool arabic) => arabic
            ? "لا يوجد مشغّل Access على هذا الخادم. ثبّت «Microsoft Access Database Engine 2016 Redistributable» بنسخة 64‑بت (بنفس معمارية الخادم)، ثم أعد المحاولة."
            : "No Access driver is installed on this server. Install the 64-bit \"Microsoft Access Database Engine 2016 Redistributable\" (matching the server's architecture) and try again.";

        /// <summary>The user tables in the file — Access's own MSys* bookkeeping is not one of them.</summary>
        public static IReadOnlyList<string> ListTables(string filePath)
        {
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
