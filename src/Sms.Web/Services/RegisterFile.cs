using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sms.Web.Services
{
    /// <summary>
    /// The one door an import screen knocks on, whatever the school handed over.
    /// <para>
    /// A register arrives as an Access database or as an Excel workbook, and by the time it reaches
    /// the mapping step the difference has stopped mattering: both are a list of tables, each with
    /// named columns and rows of text. Keeping the two readers behind this means the import knows
    /// about tables and columns and not about ODBC drivers and zip parts, and that adding the next
    /// format is one file rather than an edit to every step of every import screen.
    /// </para>
    /// <para>
    /// The two readers fail differently and both failures are worth telling apart — an
    /// <c>OdbcException</c> is usually a missing Access driver, an <c>InvalidDataException</c> is
    /// usually a file that was renamed to .xlsx rather than saved as one — so neither is caught
    /// here. The screen translates them, because that is where a refusal becomes a sentence
    /// somebody reads.
    /// </para>
    /// </summary>
    public static class RegisterFile
    {
        /// <summary>What an upload field accepts, and what the extension check tests against.</summary>
        public static readonly IReadOnlyList<string> Extensions = new[] { ".xlsx", ".xlsm", ".mdb", ".accdb" };

        public static bool IsSupported(string fileName)
        {
            return Extensions.Contains(Path.GetExtension(fileName).ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>True for the formats read straight out of the file, with nothing installed on the server.</summary>
        public static bool IsWorkbook(string fileName) => WorkbookRegisterReader.Handles(fileName);

        /// <summary>An Excel sheet and an Access table are the same choice to the operator: the list to import.</summary>
        public static IReadOnlyList<string> ListTables(string filePath)
        {
            return IsWorkbook(filePath)
                ? WorkbookRegisterReader.ListSheets(filePath)
                : AccessRegisterReader.ListTables(filePath);
        }

        public static IReadOnlyList<string> ListColumns(string filePath, string table)
        {
            return IsWorkbook(filePath)
                ? WorkbookRegisterReader.ListColumns(filePath, table)
                : AccessRegisterReader.ListColumns(filePath, table);
        }

        public static List<Dictionary<string, string?>> ReadRows(string filePath, string table, int? limit = null)
        {
            return IsWorkbook(filePath)
                ? WorkbookRegisterReader.ReadRows(filePath, table, limit)
                : AccessRegisterReader.ReadRows(filePath, table, limit);
        }
    }
}
