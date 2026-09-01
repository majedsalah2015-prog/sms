using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Sms.Web.Services
{
    /// <summary>
    /// Reads a school's staff or student register out of an Excel workbook (.xlsx / .xlsm), so the
    /// list a secretary already keeps can be brought across once instead of retyped.
    /// <para>
    /// Written against the file format rather than against a driver, and that is the whole point of
    /// it. <see cref="AccessRegisterReader"/> has to go through Microsoft's ACE engine, which must
    /// be installed on the server, in the application's own bitness — the condition that made a
    /// 32-bit helper process necessary. An .xlsx has no such dependency: it is a zip of XML parts
    /// (ECMA-376), and <c>System.IO.Compression</c> plus <c>System.Xml</c> are already in the
    /// framework. Nothing to install, nothing to match bitness with, no new package, and it works
    /// the same if this is ever hosted on Linux.
    /// </para>
    /// <para>
    /// Every cell comes back as a string, like the Access reader, because the destination parses
    /// them anyway and a spreadsheet column's "type" is a poor promise about what is in it — an ID
    /// number stored as text in one row and as a number in the next is the normal case in a file a
    /// human has been maintaining for years.
    /// </para>
    /// <para>
    /// Read-only and nothing else: the file is opened, read and closed. Nothing is written back to
    /// a register that may be somebody's only copy.
    /// </para>
    /// </summary>
    public static class WorkbookRegisterReader
    {
        private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string PartRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        /// <summary>
        /// The number formats Excel ships that mean "this number is a date". Anything a school has
        /// defined itself lands in the workbook's own <c>numFmts</c> table and is judged by its
        /// format code instead — see <see cref="LooksLikeADate"/>.
        /// </summary>
        private static readonly HashSet<int> BuiltInDateFormats = new()
        {
            14, 15, 16, 17, 18, 19, 20, 21, 22,
            27, 28, 29, 30, 31, 32, 33, 34, 35, 36,
            45, 46, 47,
            50, 51, 52, 53, 54, 55, 56, 57, 58,
        };

        /// <summary>
        /// Whether this reader is the one for the file. The old binary <c>.xls</c> is deliberately
        /// not on the list: it is a different format entirely, and claiming it here would fail at
        /// the first byte with a message about zip archives instead of the one thing the operator
        /// needs to hear — save it as .xlsx and upload it again.
        /// </summary>
        public static bool Handles(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension is ".xlsx" or ".xlsm";
        }

        /// <summary>The sheets of the workbook, in the order the tabs appear along the bottom.</summary>
        public static IReadOnlyList<string> ListSheets(string filePath)
        {
            using var zip = ZipFile.OpenRead(filePath);
            return Sheets(zip).Select(s => s.Name).ToList();
        }

        /// <summary>
        /// The column names of one sheet: its header row, as text.
        /// <para>
        /// The header is the first row carrying two cells or more, not simply the first row with
        /// anything in it. Half the registers in circulation open with a merged title cell —
        /// "كشف الموظفين ٢٠٢٥" across the top — and taking that as the header would offer the
        /// operator one column called "كشف الموظفين ٢٠٢٥" to map twenty fields onto.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> ListColumns(string filePath, string sheet)
        {
            return ReadGrid(filePath, sheet, 0).Columns;
        }

        /// <summary>
        /// Every data row of one sheet, keyed by column name — the shape
        /// <see cref="AccessRegisterReader.ReadRows"/> returns, so the import that consumes it does
        /// not care which of the two files it was handed.
        /// </summary>
        public static List<Dictionary<string, string?>> ReadRows(string filePath, string sheet, int? limit = null)
        {
            return ReadGrid(filePath, sheet, limit ?? int.MaxValue).Rows;
        }

        // ------------------------------------------------------------------ the workbook parts

        private sealed record SheetEntry(string Name, string Path);

        /// <summary>
        /// What the whole sheet needs in order to be read: the shared string table every text cell
        /// points into, and the style table that says which numbers are really dates.
        /// </summary>
        private sealed class Workbook
        {
            public List<string> Strings { get; } = new();

            /// <summary>numFmtId per cell-format index — the <c>s</c> attribute of a cell indexes this.</summary>
            public List<int> CellFormats { get; } = new();

            /// <summary>The workbook's own format codes, by id, for the ones Excel does not ship.</summary>
            public Dictionary<int, string> CustomFormats { get; } = new();

            /// <summary>Set by workbooks authored on the old Macintosh epoch. Rare, and cheap to honour.</summary>
            public bool Epoch1904 { get; set; }

            public bool IsDate(string? styleIndex)
            {
                if (!int.TryParse(styleIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                    || index < 0 || index >= CellFormats.Count)
                {
                    return false;
                }

                var format = CellFormats[index];
                return BuiltInDateFormats.Contains(format)
                    || (CustomFormats.TryGetValue(format, out var code) && LooksLikeADate(code));
            }
        }

        private static IReadOnlyList<SheetEntry> Sheets(ZipArchive zip)
        {
            var workbookPath = WorkbookPath(zip);
            var workbook = Parse(zip, workbookPath);
            var relationships = Relationships(zip, workbookPath);

            var sheets = new List<SheetEntry>();
            foreach (var sheet in workbook.Root?.Element(XName.Get("sheets", Main))?.Elements(XName.Get("sheet", Main))
                ?? Enumerable.Empty<XElement>())
            {
                var name = (string?)sheet.Attribute("name");
                var id = (string?)sheet.Attribute(XName.Get("id", PartRelationships));
                if (name == null || id == null || !relationships.TryGetValue(id, out var target)) { continue; }

                sheets.Add(new SheetEntry(name, Resolve(workbookPath, target)));
            }

            return sheets;
        }

        /// <summary>
        /// Where the workbook part actually lives. Almost always <c>xl/workbook.xml</c>, but the
        /// package says so itself and a file produced by something other than Excel is entitled to
        /// put it elsewhere.
        /// </summary>
        private static string WorkbookPath(ZipArchive zip)
        {
            var root = Parse(zip, "_rels/.rels");
            var relationship = root.Root?.Elements(XName.Get("Relationship", PackageRelationships)).FirstOrDefault(
                r => ((string?)r.Attribute("Type"))?.EndsWith("/officeDocument", StringComparison.OrdinalIgnoreCase) == true);

            // Resolved against the package root and not against "_rels/", which is where the file
            // saying so happens to live: a relationship target is relative to the part that owns
            // the relationships, and the part that owns these is the package itself.
            var target = (string?)relationship?.Attribute("Target");
            return target == null ? "xl/workbook.xml" : Resolve(string.Empty, target);
        }

        private static Dictionary<string, string> Relationships(ZipArchive zip, string partPath)
        {
            var folder = Folder(partPath);
            var relationshipPath = folder + "_rels/" + Path.GetFileName(partPath) + ".rels";
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var entry = Find(zip, relationshipPath);
            if (entry == null) { return map; }

            foreach (var r in Parse(zip, relationshipPath).Root?.Elements(XName.Get("Relationship", PackageRelationships))
                ?? Enumerable.Empty<XElement>())
            {
                var id = (string?)r.Attribute("Id");
                var target = (string?)r.Attribute("Target");
                if (id != null && target != null) { map[id] = target; }
            }

            return map;
        }

        /// <summary>
        /// The shared strings and the style table, read once per open. Both are optional parts: a
        /// workbook whose every cell is a number has neither.
        /// </summary>
        private static Workbook ReadWorkbookData(ZipArchive zip)
        {
            var data = new Workbook();
            var workbookPath = WorkbookPath(zip);
            var folder = Folder(workbookPath);

            var properties = Parse(zip, workbookPath).Root?.Element(XName.Get("workbookPr", Main));
            data.Epoch1904 = (string?)properties?.Attribute("date1904") is "1" or "true";

            if (Find(zip, folder + "sharedStrings.xml") != null)
            {
                foreach (var item in Parse(zip, folder + "sharedStrings.xml").Root?.Elements(XName.Get("si", Main))
                    ?? Enumerable.Empty<XElement>())
                {
                    // The plain cells hold one <t>; the ones somebody coloured half a word of hold a
                    // run of them. Phonetic guides (<rPh>) also hold a <t> and are not part of the
                    // text, which is why these two are named rather than every descendant taken.
                    var text = string.Concat(item.Elements(XName.Get("t", Main)).Select(t => t.Value))
                        + string.Concat(item.Elements(XName.Get("r", Main)).Elements(XName.Get("t", Main)).Select(t => t.Value));
                    data.Strings.Add(text);
                }
            }

            if (Find(zip, folder + "styles.xml") != null)
            {
                var styles = Parse(zip, folder + "styles.xml").Root;
                foreach (var format in styles?.Element(XName.Get("numFmts", Main))?.Elements(XName.Get("numFmt", Main))
                    ?? Enumerable.Empty<XElement>())
                {
                    if (int.TryParse((string?)format.Attribute("numFmtId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                    {
                        data.CustomFormats[id] = (string?)format.Attribute("formatCode") ?? string.Empty;
                    }
                }

                foreach (var cellFormat in styles?.Element(XName.Get("cellXfs", Main))?.Elements(XName.Get("xf", Main))
                    ?? Enumerable.Empty<XElement>())
                {
                    data.CellFormats.Add(
                        int.TryParse((string?)cellFormat.Attribute("numFmtId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                            ? id
                            : 0);
                }
            }

            return data;
        }

        // ------------------------------------------------------------------ the sheet itself

        private sealed record Grid(IReadOnlyList<string> Columns, List<Dictionary<string, string?>> Rows);

        private static Grid ReadGrid(string filePath, string sheet, int rowLimit)
        {
            using var zip = ZipFile.OpenRead(filePath);
            var entry = Sheets(zip).FirstOrDefault(s => string.Equals(s.Name, sheet, StringComparison.Ordinal))
                ?? throw new InvalidDataException($"The workbook has no sheet named \"{sheet}\".");

            var data = ReadWorkbookData(zip);
            var part = Find(zip, entry.Path) ?? throw new InvalidDataException($"Sheet \"{sheet}\" is missing from the file.");

            var columns = Array.Empty<string>() as IReadOnlyList<string>;
            var rows = new List<Dictionary<string, string?>>();
            Dictionary<int, string?>? header = null;
            Dictionary<int, string?>? pending = null;

            foreach (var cells in ReadCells(part, data))
            {
                if (cells.Values.All(v => string.IsNullOrWhiteSpace(v))) { continue; }

                var row = cells;
                if (header == null)
                {
                    // The first non-empty row is judged against the one after it, because half the
                    // registers in circulation open with a merged title cell — "كشف الموظفين ٢٠٢٥"
                    // across the top — and taking that as the header would offer the operator one
                    // column to map twenty fields onto. A lone cell followed by a wide row is a
                    // title; anything else is the header.
                    if (pending == null) { pending = cells; continue; }

                    var title = Filled(pending) < 2 && Filled(cells) >= 2;
                    header = title ? cells : pending;
                    columns = NameColumns(header);
                    if (title) { continue; }
                }

                if (rows.Count >= rowLimit) { break; }
                rows.Add(Map(header, columns, row));
            }

            // A sheet of one row is all header and no data; a one-column sheet never produced the
            // wide row that would have settled the question above.
            if (header == null && pending != null) { columns = NameColumns(pending); }

            return new Grid(columns, rows);
        }

        private static int Filled(Dictionary<int, string?> cells)
        {
            return cells.Count(c => !string.IsNullOrWhiteSpace(c.Value));
        }

        /// <summary>
        /// One sheet row against the header, by position. A cell in a column the header never named
        /// is dropped: there is nothing to call it, so there is nothing the operator could map.
        /// </summary>
        private static Dictionary<string, string?> Map(
            Dictionary<int, string?> header, IReadOnlyList<string> columns, Dictionary<int, string?> cells)
        {
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var index = 0;
            foreach (var column in header.Keys.OrderBy(k => k))
            {
                if (index >= columns.Count) { break; }
                row[columns[index]] = cells.TryGetValue(column, out var value) ? value : null;
                index++;
            }

            return row;
        }

        /// <summary>
        /// Column names from the header row. A blank header is named for its spreadsheet letter and
        /// a repeated one is numbered, because these are what the mapping dropdowns are keyed by:
        /// two columns called the same thing would silently map to one.
        /// </summary>
        private static IReadOnlyList<string> NameColumns(Dictionary<int, string?> header)
        {
            var names = new List<string>();
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in header.Keys.OrderBy(k => k))
            {
                var name = (header[column] ?? string.Empty).Trim();
                if (name.Length == 0) { name = "(" + LetterOf(column) + ")"; }

                if (seen.TryGetValue(name, out var count))
                {
                    seen[name] = count + 1;
                    name = name + " (" + (count + 1).ToString(CultureInfo.InvariantCulture) + ")";
                }
                else
                {
                    seen[name] = 1;
                }

                names.Add(name);
            }

            return names;
        }

        /// <summary>
        /// Streams the sheet a row at a time, keyed by column number. Streamed rather than loaded:
        /// a register of a few thousand staff is a few megabytes of XML, and there is no reason to
        /// hold all of it to read the twenty columns that were mapped.
        /// </summary>
        private static IEnumerable<Dictionary<int, string?>> ReadCells(ZipArchiveEntry part, Workbook data)
        {
            using var stream = part.Open();
            using var xml = XmlReader.Create(stream, new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = true,
            });

            while (xml.Read())
            {
                if (xml.NodeType != XmlNodeType.Element || xml.LocalName != "row") { continue; }
                if (xml.IsEmptyElement) { continue; }

                var cells = new Dictionary<int, string?>();
                var next = 1;
                using var rowReader = xml.ReadSubtree();
                rowReader.Read();
                while (rowReader.Read())
                {
                    if (rowReader.NodeType != XmlNodeType.Element || rowReader.LocalName != "c") { continue; }

                    var column = ColumnOf(rowReader.GetAttribute("r"), next);
                    next = column + 1;
                    var type = rowReader.GetAttribute("t");
                    var style = rowReader.GetAttribute("s");

                    if (rowReader.IsEmptyElement) { cells[column] = null; continue; }

                    string? raw = null;
                    var inline = false;
                    using (var cellReader = rowReader.ReadSubtree())
                    {
                        cellReader.Read();
                        while (cellReader.Read())
                        {
                            if (cellReader.NodeType != XmlNodeType.Element) { continue; }
                            if (cellReader.LocalName == "v")
                            {
                                raw = cellReader.ReadElementContentAsString();
                            }
                            else if (cellReader.LocalName == "is")
                            {
                                var text = XNode.ReadFrom(cellReader) as XElement;
                                raw = text == null ? null : string.Concat(text.Descendants(XName.Get("t", Main)).Select(t => t.Value));
                                inline = true;
                            }
                        }
                    }

                    cells[column] = Interpret(raw, type, style, inline, data);
                }

                yield return cells;
            }
        }

        /// <summary>
        /// One cell as the text it means. A cell whose formula ended in <c>#N/A</c> comes back null
        /// rather than as the error code: an unresolved error is the absence of a value, and
        /// storing "#N/A" as somebody's job title is worse than storing nothing.
        /// </summary>
        private static string? Interpret(string? raw, string? type, string? style, bool inline, Workbook data)
        {
            if (raw == null) { return null; }
            if (inline || type == "inlineStr" || type == "str") { return raw; }

            switch (type)
            {
                case "s":
                    return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                        && index >= 0 && index < data.Strings.Count
                        ? data.Strings[index]
                        : null;
                case "b":
                    return raw == "1" ? "TRUE" : "FALSE";
                case "e":
                    return null;
                default:
                    if (data.IsDate(style)
                        && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
                    {
                        return DateFrom(serial, data.Epoch1904) ?? Number(raw);
                    }

                    return Number(raw);
            }
        }

        /// <summary>
        /// A number as a person wrote it, not as a computer would print it. Straight
        /// <c>Convert.ToString</c> turns a nine-digit ID into <c>1.23456789E+08</c> the moment it
        /// arrives as a double, which is how an identity number becomes unusable in transit.
        /// </summary>
        private static string Number(string raw)
        {
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value.ToString("0.###############", CultureInfo.InvariantCulture)
                : raw;
        }

        /// <summary>
        /// Excel keeps a date as the count of days since its epoch, so a cell showing 1990-05-03 is
        /// the number 32996 in the file. Day zero is 1899-12-30 rather than 1900-01-01 because the
        /// 1900 system deliberately reproduces Lotus's belief that 1900 was a leap year; dates in
        /// January and February 1900 are therefore a day out, which no staff register contains.
        /// </summary>
        private static string? DateFrom(double serial, bool epoch1904)
        {
            if (serial < 0 || serial > 2958465) { return null; }

            var epoch = epoch1904 ? new DateTime(1904, 1, 1) : new DateTime(1899, 12, 30);
            var moment = epoch.AddDays(serial);
            return moment.TimeOfDay == TimeSpan.Zero
                ? moment.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : moment.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Whether a format code a school wrote itself describes a date. Quoted literals, the
        /// bracketed colour and locale sections and backslash-escaped characters are removed first —
        /// <c>"يوم "dd</c> carries a "d" inside its own text that means nothing about the number.
        /// </summary>
        private static bool LooksLikeADate(string code)
        {
            var stripped = new System.Text.StringBuilder(code.Length);
            for (var i = 0; i < code.Length; i++)
            {
                switch (code[i])
                {
                    case '"':
                        while (++i < code.Length && code[i] != '"') { }
                        break;
                    case '[':
                        while (++i < code.Length && code[i] != ']') { }
                        break;
                    case '\\':
                        i++;
                        break;
                    default:
                        stripped.Append(code[i]);
                        break;
                }
            }

            var remainder = stripped.ToString();
            return remainder.IndexOf('y') >= 0 || remainder.IndexOf('Y') >= 0
                || remainder.IndexOf('d') >= 0 || remainder.IndexOf('D') >= 0
                || remainder.IndexOf('m') >= 0 || remainder.IndexOf('M') >= 0
                || remainder.IndexOf('h') >= 0 || remainder.IndexOf('H') >= 0;
        }

        // ------------------------------------------------------------------ small mechanics

        /// <summary>"B" of "B12" as 2. Falls back to the position after the last cell for the
        /// writers that omit the reference on a run of adjacent cells.</summary>
        private static int ColumnOf(string? reference, int fallback)
        {
            if (string.IsNullOrEmpty(reference)) { return fallback; }

            var index = 0;
            foreach (var c in reference)
            {
                if (c >= 'A' && c <= 'Z') { index = (index * 26) + (c - 'A' + 1); }
                else if (c >= 'a' && c <= 'z') { index = (index * 26) + (c - 'a' + 1); }
                else { break; }
            }

            return index == 0 ? fallback : index;
        }

        /// <summary>2 as "B" — for naming a column whose header cell was left blank.</summary>
        private static string LetterOf(int column)
        {
            var name = string.Empty;
            while (column > 0)
            {
                var remainder = (column - 1) % 26;
                name = (char)('A' + remainder) + name;
                column = (column - 1) / 26;
            }

            return name.Length == 0 ? "?" : name;
        }

        private static string Folder(string partPath)
        {
            var slash = partPath.LastIndexOf('/');
            return slash < 0 ? string.Empty : partPath.Substring(0, slash + 1);
        }

        /// <summary>A relationship target, which may be relative to the part that declared it or
        /// absolute within the package.</summary>
        private static string Resolve(string partPath, string target)
        {
            if (target.StartsWith("/", StringComparison.Ordinal)) { return target.Substring(1); }

            var combined = Folder(partPath) + target;
            var segments = new List<string>();
            foreach (var segment in combined.Split('/'))
            {
                if (segment == "." || segment.Length == 0) { continue; }
                if (segment == ".." && segments.Count > 0) { segments.RemoveAt(segments.Count - 1); continue; }
                segments.Add(segment);
            }

            return string.Join("/", segments);
        }

        /// <summary>Zip entries are matched without regard to case: the package spec is case
        /// sensitive, and the tools that write these files are not consistently.</summary>
        private static ZipArchiveEntry? Find(ZipArchive zip, string path)
        {
            return zip.GetEntry(path)
                ?? zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, path, StringComparison.OrdinalIgnoreCase));
        }

        private static XDocument Parse(ZipArchive zip, string path)
        {
            var entry = Find(zip, path) ?? throw new InvalidDataException($"The workbook is missing its \"{path}\" part.");
            using var stream = entry.Open();
            using var xml = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = true,
            });

            return XDocument.Load(xml);
        }
    }
}
