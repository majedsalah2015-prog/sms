using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sms.Web.Models
{
    /// <summary>
    /// The mechanics every export in this product shares: how a cell is escaped, how a record is
    /// joined, and what bytes the browser is handed.
    /// <para>
    /// Held in one place because each of the three is wrong in a way nobody notices until a school
    /// opens the file. A family name carrying a comma shifts every column after it; an Arabic name
    /// written without a byte-order mark arrives in Excel as mojibake, which reads as the system
    /// having mangled the register rather than as three missing bytes. Two copies of these rules
    /// would eventually be two different rules.
    /// </para>
    /// </summary>
    public static class CsvFile
    {
        /// <summary>
        /// One CSV cell. Always quoted, with the quote itself doubled — quoting only the cells that
        /// look dangerous means the escaping is decided by whoever typed the name.
        /// </summary>
        public static string Cell(string? value)
            => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        /// <summary>One CSV record, comma-separated, every cell quoted.</summary>
        public static string Line(IEnumerable<string?> cells)
            => string.Join(",", (cells ?? Array.Empty<string?>()).Select(Cell));

        /// <summary>
        /// The finished file: CRLF-terminated records, UTF-8 with a byte-order mark, because the
        /// first thing anybody does with a download is open it in Excel.
        /// </summary>
        public static byte[] Bytes(IEnumerable<IEnumerable<string?>> records)
        {
            var text = new StringBuilder();
            foreach (var record in records ?? Array.Empty<IEnumerable<string?>>())
            {
                text.Append(Line(record)).Append("\r\n");
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
        }

        /// <summary>
        /// A record's own identifier made safe to put in a filename: ASCII letters, digits and
        /// dashes, everything else folded to a dash. A numbering series can be configured with an
        /// Arabic prefix, and a browser handed a non-Latin <c>Content-Disposition</c> may save the
        /// download under a name nobody can find again.
        /// </summary>
        public static string Slug(string? value, string fallback)
        {
            var text = new StringBuilder();
            foreach (var c in value ?? string.Empty)
            {
                if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9')
                {
                    text.Append(c);
                }
                else if (text.Length > 0 && text[text.Length - 1] != '-')
                {
                    text.Append('-');
                }
            }

            var slug = text.ToString().Trim('-');
            return slug.Length == 0 ? fallback : slug;
        }
    }
}
