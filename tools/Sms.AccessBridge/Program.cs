using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Sms.AccessBridge
{
    /// <summary>
    /// Reads a school's old Access register (.mdb / .accdb) in a 32-bit process and prints the
    /// answer as JSON, because the driver that can open the file is 32-bit and the web app is not.
    /// See the project file for why that is the arrangement.
    /// <para>
    /// Three verbs, matching what the import screen asks in its three steps:
    /// </para>
    /// <code>
    /// Sms.AccessBridge tables  &lt;file&gt;
    /// Sms.AccessBridge columns &lt;file&gt; &lt;table&gt;
    /// Sms.AccessBridge rows    &lt;file&gt; &lt;table&gt;
    /// </code>
    /// <para>
    /// Always exits 0 with <c>{"ok":true,…}</c> or 1 with <c>{"ok":false,"error":…}</c>: a caller
    /// parsing stdout should never have to also parse a stack trace off stderr to find out what
    /// happened. <c>missingDriver</c> distinguishes "this machine has no Access driver at all"
    /// from anything about the file, because those two need opposite things said to the operator.
    /// </para>
    /// <para>
    /// Read-only and nothing else. The register is somebody's only copy of their history.
    /// </para>
    /// </summary>
    public static class Program
    {
        /// <summary>The ACE driver, which reads both formats. Present wherever Office 2007+ has been.</summary>
        private const string AceDriver = "Microsoft Access Driver (*.mdb, *.accdb)";

        /// <summary>The old Jet driver — .mdb only, but on machines that have nothing newer it is the difference between working and not.</summary>
        private const string JetDriver = "Microsoft Access Driver (*.mdb)";

        public static int Main(string[] args)
        {
            // The register is Arabic. A default console codepage would hand the caller mojibake
            // that still parses as JSON, which is the worst way for this to fail.
            Console.OutputEncoding = new UTF8Encoding(false);

            try
            {
                if (args.Length < 2)
                {
                    return Fail("usage: Sms.AccessBridge <tables|columns|rows> <file> [table]", missingDriver: false);
                }

                var file = args[1];
                if (!File.Exists(file))
                {
                    return Fail($"No such file: {file}", missingDriver: false);
                }

                return args[0].ToLowerInvariant() switch
                {
                    "tables" => Tables(file),
                    "columns" => args.Length >= 3 ? Columns(file, args[2]) : Fail("columns needs a table name.", false),
                    "rows" => args.Length >= 3 ? Rows(file, args[2]) : Fail("rows needs a table name.", false),
                    _ => Fail($"Unknown verb: {args[0]}", missingDriver: false),
                };
            }
            catch (OdbcException ex)
            {
                return Fail(ex.Message, IsMissingDriver(ex));
            }
            catch (Exception ex)
            {
                return Fail(ex.Message, missingDriver: false);
            }
        }

        private static int Tables(string file)
        {
            using var connection = Open(file);
            var schema = connection.GetSchema("Tables");
            var tables = schema.Rows.Cast<DataRow>()
                .Where(r => string.Equals(Convert.ToString(r["TABLE_TYPE"], CultureInfo.InvariantCulture), "TABLE", StringComparison.OrdinalIgnoreCase))
                .Select(r => Convert.ToString(r["TABLE_NAME"], CultureInfo.InvariantCulture) ?? string.Empty)
                .Where(n => n.Length > 0 && !n.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(new Dictionary<string, object> { ["tables"] = tables });
        }

        private static int Columns(string file, string table)
        {
            using var connection = Open(file);
            using var command = new OdbcCommand($"SELECT * FROM [{Sanitize(table)}]", connection);
            using var reader = command.ExecuteReader(CommandBehavior.SchemaOnly);
            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();

            return Ok(new Dictionary<string, object> { ["columns"] = columns });
        }

        private static int Rows(string file, string table)
        {
            using var connection = Open(file);
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
                        ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : Convert.ToString(value, CultureInfo.InvariantCulture);
                }

                rows.Add(row);
            }

            return Ok(new Dictionary<string, object> { ["columns"] = columns, ["rows"] = rows });
        }

        /// <summary>
        /// ACE first, because it reads both formats; the old Jet driver second, because a machine
        /// that only has it can still open the .mdb registers that are the common case here.
        /// </summary>
        private static OdbcConnection Open(string filePath)
        {
            try
            {
                return Connect(AceDriver, filePath);
            }
            catch (OdbcException ex) when (IsMissingDriver(ex) && !filePath.EndsWith(".accdb", StringComparison.OrdinalIgnoreCase))
            {
                return Connect(JetDriver, filePath);
            }
        }

        private static OdbcConnection Connect(string driver, string filePath)
        {
            var connection = new OdbcConnection($"Driver={{{driver}}};Dbq={filePath};ReadOnly=1;");
            connection.Open();
            return connection;
        }

        /// <summary>IM002 is ODBC's own code for "no such driver"; the message check covers drivers that say it differently.</summary>
        private static bool IsMissingDriver(OdbcException exception)
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
        /// A table name goes into the SQL text because ODBC cannot parameterise an identifier. It
        /// comes from the list this program itself produced, but a closing bracket would still break
        /// out of the quoting, so it cannot be allowed through.
        /// </summary>
        private static string Sanitize(string table)
        {
            if (string.IsNullOrWhiteSpace(table) || table.Contains(']') || table.Contains('[') || table.Contains(';'))
            {
                throw new InvalidOperationException("That table name cannot be read.");
            }

            return table;
        }

        private static int Ok(Dictionary<string, object> payload)
        {
            payload["ok"] = true;
            Console.Out.Write(JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
            return 0;
        }

        private static int Fail(string error, bool missingDriver)
        {
            Console.Out.Write(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["ok"] = false,
                ["error"] = error,
                ["missingDriver"] = missingDriver,
            }, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
            return 1;
        }
    }
}
