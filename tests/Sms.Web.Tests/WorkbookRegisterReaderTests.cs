using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Sms.Web.Services;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The reader that lets a school's staff list arrive as the spreadsheet it already is.
    /// <para>
    /// Every case here is a real shape a school's file turns up in, and each one is a way an import
    /// fails silently rather than loudly: a number that arrives as <c>1.23456789E+08</c> is an
    /// identity number nobody can search for afterwards; a date read as 32996 is a birth date;
    /// a skipped empty cell shifts every column after it by one and maps the salary onto the job
    /// title. None of those throw — they just quietly import the wrong register.
    /// </para>
    /// <para>
    /// The workbooks are built here rather than committed as binaries, because a fixture you can
    /// read is a fixture whose failure tells you what was in the file.
    /// </para>
    /// </summary>
    public class WorkbookRegisterReaderTests : IDisposable
    {
        private readonly List<string> _files = new();

        public void Dispose()
        {
            foreach (var file in _files)
            {
                try { if (File.Exists(file)) { File.Delete(file); } }
                catch (IOException) { /* a temp file the runner still holds is swept by the OS */ }
            }
        }

        [Fact]
        public void The_sheets_are_listed_in_tab_order()
        {
            var path = Workbook(
                Sheet("الموظفون", Row(Text(0))),
                Sheet("Sheet2", Row(Text(0))));

            Assert.Equal(new[] { "الموظفون", "Sheet2" }, WorkbookRegisterReader.ListSheets(path));
        }

        [Fact]
        public void The_header_row_becomes_the_column_names()
        {
            var path = Workbook(Sheet(
                "الموظفون",
                Row(Text(0), Text(1), Text(2)),
                Row(Text(3), Text(4), Text(5))),
                "اسم الموظف", "الوظيفة", "الجوال", "محمد أحمد الخطيب", "معلم", "0599123456");

            Assert.Equal(new[] { "اسم الموظف", "الوظيفة", "الجوال" }, WorkbookRegisterReader.ListColumns(path, "الموظفون"));
        }

        [Fact]
        public void A_row_comes_back_keyed_by_its_column_name()
        {
            var path = Workbook(Sheet(
                "الموظفون",
                Row(Text(0), Text(1)),
                Row(Text(2), Text(3))),
                "اسم الموظف", "الوظيفة", "محمد أحمد الخطيب", "معلم");

            var rows = WorkbookRegisterReader.ReadRows(path, "الموظفون");

            var row = Assert.Single(rows);
            Assert.Equal("محمد أحمد الخطيب", row["اسم الموظف"]);
            Assert.Equal("معلم", row["الوظيفة"]);
        }

        /// <summary>
        /// Excel writes no cell at all for an empty one, so a row of three cells can be columns A,
        /// B and D. Reading them in order rather than by reference puts the fourth column's value
        /// in the third column's field — the mapping is then wrong for every row below the first
        /// gap, and the preview looks entirely plausible.
        /// </summary>
        [Fact]
        public void A_skipped_empty_cell_does_not_shift_the_columns_after_it()
        {
            var path = Workbook(Sheet(
                "الموظفون",
                Row(Text(0), Text(1), Text(2)),
                RowAt(("A2", Text(3)), ("C2", Text(4)))),
                "الاسم", "الوظيفة", "الجوال", "محمد الخطيب", "0599123456");

            var row = Assert.Single(WorkbookRegisterReader.ReadRows(path, "الموظفون"));

            Assert.Equal("محمد الخطيب", row["الاسم"]);
            Assert.True(string.IsNullOrEmpty(row["الوظيفة"]));
            Assert.Equal("0599123456", row["الجوال"]);
        }

        /// <summary>
        /// A nine-digit identity number arrives as a double. Printed the obvious way it becomes
        /// "1.23456789E+08" and stops being an identity number.
        /// </summary>
        [Fact]
        public void A_long_number_is_not_printed_in_scientific_notation()
        {
            var path = Workbook(Sheet(
                "الموظفون",
                Row(Text(0), Text(1)),
                Row(Text(2), Number("123456789"))),
                "الاسم", "رقم الهوية", "محمد الخطيب");

            var row = Assert.Single(WorkbookRegisterReader.ReadRows(path, "الموظفون"));

            Assert.Equal("123456789", row["رقم الهوية"]);
        }

        /// <summary>
        /// A cell formatted as a date holds a day count, not a date. 32996 is 1990-05-03, and the
        /// number is what a register's birth-date column is full of.
        /// </summary>
        [Fact]
        public void A_date_cell_comes_back_as_a_date()
        {
            var path = Workbook(Sheet(
                "الموظفون",
                Row(Text(0), Text(1)),
                Row(Text(2), Dated("32996"))),
                "الاسم", "تاريخ الميلاد", "محمد الخطيب");

            var row = Assert.Single(WorkbookRegisterReader.ReadRows(path, "الموظفون"));

            Assert.Equal("1990-05-03", row["تاريخ الميلاد"]);
        }

        /// <summary>The same number without a date format is a number, and staying a number matters:
        /// a salary column is not a date column because somebody once formatted one cell.</summary>
        [Fact]
        public void An_unformatted_number_stays_a_number()
        {
            var path = Workbook(Sheet(
                "الموظفون",
                Row(Text(0), Text(1)),
                Row(Text(2), Number("32996"))),
                "الاسم", "الراتب", "محمد الخطيب");

            var row = Assert.Single(WorkbookRegisterReader.ReadRows(path, "الموظفون"));

            Assert.Equal("32996", row["الراتب"]);
        }

        /// <summary>
        /// Half the registers in circulation open with a merged title across the top. Taken as the
        /// header it offers the operator one column to map twenty fields onto.
        /// </summary>
        [Fact]
        public void A_title_row_above_the_header_is_not_the_header()
        {
            var path = Workbook(Sheet(
                "الموظفون",
                Row(Text(0)),
                Row(Text(1), Text(2)),
                Row(Text(3), Text(4))),
                "كشف الموظفين 2025", "الاسم", "الوظيفة", "محمد الخطيب", "معلم");

            Assert.Equal(new[] { "الاسم", "الوظيفة" }, WorkbookRegisterReader.ListColumns(path, "الموظفون"));

            var row = Assert.Single(WorkbookRegisterReader.ReadRows(path, "الموظفون"));
            Assert.Equal("محمد الخطيب", row["الاسم"]);
        }

        /// <summary>Two columns with the same heading would otherwise map to one, silently.</summary>
        [Fact]
        public void A_repeated_heading_is_numbered_rather_than_merged()
        {
            var path = Workbook(Sheet(
                "الموظفون",
                Row(Text(0), Text(1)),
                Row(Text(2), Text(3))),
                "الاسم", "الاسم", "محمد", "الخطيب");

            var columns = WorkbookRegisterReader.ListColumns(path, "الموظفون");

            Assert.Equal(2, columns.Count);
            Assert.NotEqual(columns[0], columns[1]);
        }

        /// <summary>
        /// A heading nobody filled in is still a column somebody may want to map, and it needs a
        /// name to be mapped by. Its spreadsheet letter is the one name the operator can check
        /// against the file in front of them.
        /// </summary>
        [Fact]
        public void A_blank_heading_is_named_for_its_column_letter()
        {
            var path = Workbook(Sheet(
                "الموظفون",
                RowAt(("A1", Text(0)), ("B1", Blank()), ("C1", Text(1))),
                RowAt(("A2", Text(2)), ("B2", Text(3)), ("C2", Text(4)))),
                "الاسم", "الوظيفة", "محمد", "0599123456", "معلم");

            var columns = WorkbookRegisterReader.ListColumns(path, "الموظفون");

            Assert.Equal(new[] { "الاسم", "(B)", "الوظيفة" }, columns);

            var row = Assert.Single(WorkbookRegisterReader.ReadRows(path, "الموظفون"));
            Assert.Equal("0599123456", row["(B)"]);
        }

        [Fact]
        public void An_entirely_empty_row_is_not_a_row()
        {
            var path = Workbook(Sheet(
                "الموظفون",
                Row(Text(0), Text(1)),
                Row(Text(2), Text(3)),
                RowAt(),
                Row(Text(4), Text(5))),
                "الاسم", "الوظيفة", "محمد", "معلم", "أحمد", "إداري");

            Assert.Equal(2, WorkbookRegisterReader.ReadRows(path, "الموظفون").Count);
        }

        /// <summary>Some writers put the text in the cell instead of the shared table.</summary>
        [Fact]
        public void An_inline_string_reads_the_same_as_a_shared_one()
        {
            var path = Workbook(Sheet(
                "الموظفون",
                Row(Text(0), Text(1)),
                Row(Inline("محمد الخطيب"), Text(2))),
                "الاسم", "الوظيفة", "معلم");

            var row = Assert.Single(WorkbookRegisterReader.ReadRows(path, "الموظفون"));

            Assert.Equal("محمد الخطيب", row["الاسم"]);
        }

        /// <summary>
        /// The old binary .xls is a different format, not an older spelling of this one, and saying
        /// so at the extension is what lets the screen tell the operator to re-save it.
        /// </summary>
        [Theory]
        [InlineData("register.xlsx", true)]
        [InlineData("REGISTER.XLSX", true)]
        [InlineData("register.xlsm", true)]
        [InlineData("register.xls", false)]
        [InlineData("register.accdb", false)]
        [InlineData("register.csv", false)]
        public void Only_the_open_xml_workbook_formats_are_claimed(string fileName, bool handled)
        {
            Assert.Equal(handled, WorkbookRegisterReader.Handles(fileName));
        }

        /// <summary>A file renamed to .xlsx is not a workbook, and it fails as one rather than as a
        /// null reference three steps later.</summary>
        [Fact]
        public void Something_that_is_not_a_workbook_is_refused_as_one()
        {
            var path = Path.Combine(Path.GetTempPath(), "sms-test-" + Guid.NewGuid().ToString("N") + ".xlsx");
            File.WriteAllText(path, "الاسم,الوظيفة");
            _files.Add(path);

            Assert.Throws<InvalidDataException>(() => WorkbookRegisterReader.ListSheets(path));
        }

        // ------------------------------------------------------------------ building a workbook

        private static string Text(int sharedStringIndex) =>
            $"<c t=\"s\"><v>{sharedStringIndex}</v></c>";

        private static string Number(string value) => $"<c><v>{value}</v></c>";

        /// <summary>A cell that exists and holds nothing — what a heading somebody deleted looks like.</summary>
        private static string Blank() => "<c/>";

        /// <summary>Style 1 of the styles part below is the one carrying a date format.</summary>
        private static string Dated(string serial) => $"<c s=\"1\"><v>{serial}</v></c>";

        private static string Inline(string value) =>
            $"<c t=\"inlineStr\"><is><t>{value}</t></is></c>";

        /// <summary>A row of adjacent cells, which is what Excel writes when none of them is empty.</summary>
        private static string Row(params string[] cells) => "<row>" + string.Concat(cells) + "</row>";

        /// <summary>A row whose cells carry their own references — the shape with gaps in it.</summary>
        private static string RowAt(params (string Reference, string Cell)[] cells)
        {
            var written = cells.Select(c => c.Cell.Replace("<c", $"<c r=\"{c.Reference}\"", StringComparison.Ordinal));
            return "<row>" + string.Concat(written) + "</row>";
        }

        private static (string Name, string Xml) Sheet(string name, params string[] rows) =>
            (name, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                + "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                + "<sheetData>" + string.Concat(rows) + "</sheetData></worksheet>");

        /// <summary>
        /// A workbook on disk, with the parts a real one has: the package relationships, the
        /// workbook and its own relationships, the shared string table the text cells point into,
        /// and a style table whose second entry is a date format.
        /// </summary>
        private string Workbook((string Name, string Xml)[] sheets, params string[] sharedStrings)
        {
            var path = Path.Combine(Path.GetTempPath(), "sms-test-" + Guid.NewGuid().ToString("N") + ".xlsx");
            _files.Add(path);

            using (var file = File.Create(path))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                Write(zip, "_rels/.rels",
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                    + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                    + "</Relationships>");

                var sheetElements = new StringBuilder();
                var relationships = new StringBuilder();
                for (var i = 0; i < sheets.Length; i++)
                {
                    var id = "rId" + (i + 1).ToString();
                    sheetElements.Append($"<sheet name=\"{sheets[i].Name}\" sheetId=\"{i + 1}\" r:id=\"{id}\"/>");
                    relationships.Append(
                        $"<Relationship Id=\"{id}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
                    Write(zip, $"xl/worksheets/sheet{i + 1}.xml", sheets[i].Xml);
                }

                relationships.Append(
                    "<Relationship Id=\"rIdStrings\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>"
                    + "<Relationship Id=\"rIdStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");

                Write(zip, "xl/workbook.xml",
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" "
                    + "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
                    + "<sheets>" + sheetElements + "</sheets></workbook>");

                Write(zip, "xl/_rels/workbook.xml.rels",
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                    + relationships + "</Relationships>");

                Write(zip, "xl/sharedStrings.xml",
                    "<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                    + string.Concat(sharedStrings.Select(s => $"<si><t>{s}</t></si>")) + "</sst>");

                // Cell format 0 is General; cell format 1 carries numFmtId 14 — Excel's own
                // short-date format, which is what a birth-date column is stamped with.
                Write(zip, "xl/styles.xml",
                    "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
                    + "<cellXfs count=\"2\"><xf numFmtId=\"0\"/><xf numFmtId=\"14\"/></cellXfs></styleSheet>");
            }

            return path;
        }

        private static (string Name, string Xml)[] Sheets(params (string Name, string Xml)[] sheets) => sheets;

        private string Workbook((string Name, string Xml) sheet, params string[] sharedStrings) =>
            Workbook(Sheets(sheet), sharedStrings);

        private string Workbook((string Name, string Xml) first, (string Name, string Xml) second) =>
            Workbook(Sheets(first, second));

        private static void Write(ZipArchive zip, string path, string content)
        {
            var entry = zip.CreateEntry(path);
            using var stream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
