using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Security;
using Sms.Domain.Common;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;
using Sms.Web.Services;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Bringing a school's previous Access register across (owner request, 2026-08-22).
    /// <para>
    /// Seven fields and no more: the quad name, date of birth, gender, nationality and the identity
    /// number. Everything else in an old register is somebody's abandoned column, and importing it
    /// would put data into this system that nothing here knows how to keep true.
    /// </para>
    /// <para>
    /// Three requests, not one: upload, map, commit. The mapping cannot be guessed reliably — the
    /// column called <c>Name1</c> is the first name in one school's register and the family name in
    /// the next — and the preview between mapping and committing is what makes the difference
    /// visible while it is still cheap.
    /// </para>
    /// </summary>
    public partial class StudentsController
    {
        /// <summary>Uploads live here until they are committed or the folder is next swept.</summary>
        private static string ImportFolder =>
            Path.Combine(Path.GetTempPath(), "sms-student-import");

        [HttpGet("import")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Create)]
        public async Task<IActionResult> Import()
        {
            return View(await BuildImportAsync(new StudentImportViewModel()));
        }

        /// <summary>Step one: take the file, keep a copy, and report what tables are in it.</summary>
        [HttpPost("import/upload")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Create)]
        public async Task<IActionResult> ImportUpload(IFormFile? register)
        {
            var m = new StudentImportViewModel();
            try
            {
                if (register == null || register.Length == 0)
                {
                    throw new InvalidOperationException(T("Choose an Access file first.", "اختر ملف أكسس أولاً."));
                }

                var extension = Path.GetExtension(register.FileName).ToLowerInvariant();
                if (extension is not (".mdb" or ".accdb"))
                {
                    throw new InvalidOperationException(T("That is not an Access database (.mdb or .accdb).", "هذا ليس ملف قاعدة بيانات أكسس (mdb. أو accdb.)."));
                }

                Directory.CreateDirectory(ImportFolder);
                var token = Guid.NewGuid().ToString("N") + extension;
                var path = Path.Combine(ImportFolder, token);
                await using (var file = System.IO.File.Create(path))
                {
                    await register.CopyToAsync(file, HttpContext.RequestAborted);
                }

                m.Token = token;
                m.OriginalFileName = Path.GetFileName(register.FileName);
                m.Tables = AccessRegisterReader.ListTables(path);
                if (m.Tables.Count == 0)
                {
                    TempData["Error"] = T("That file has no tables to read.", "لا توجد جداول قابلة للقراءة في هذا الملف.");
                }
            }
            catch (OdbcException ex) { TempData["Error"] = Explain(ex); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return View(nameof(Import), await BuildImportAsync(m));
        }

        /// <summary>Step two: read the chosen table's columns, and preview the mapping against real rows.</summary>
        [HttpPost("import/preview")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Create)]
        public async Task<IActionResult> ImportPreview(StudentImportViewModel form)
        {
            try
            {
                var path = PathOf(form.Token);
                form.Tables = AccessRegisterReader.ListTables(path);
                if (!string.IsNullOrWhiteSpace(form.Table))
                {
                    form.Columns = AccessRegisterReader.ListColumns(path, form.Table!);
                    GuessMapping(form);

                    var rows = AccessRegisterReader.ReadRows(path, form.Table!);
                    form.TotalRows = rows.Count;

                    var preview = new List<StudentImportViewModel.PreviewRow>();
                    var ready = 0;
                    for (var i = 0; i < rows.Count; i++)
                    {
                        var candidate = Interpret(rows[i], form, i + 1);
                        if (candidate.Problem == null) { ready++; }
                        if (preview.Count < 15) { preview.Add(candidate); }
                    }

                    form.ReadyRows = ready;
                    form.SkippedRows = rows.Count - ready;
                    form.Preview = preview;
                }
            }
            catch (OdbcException ex) { TempData["Error"] = Explain(ex); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return View(nameof(Import), await BuildImportAsync(form));
        }

        /// <summary>Step three: write the rows that were ready, and say what happened to the rest.</summary>
        [HttpPost("import/commit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Create)]
        public async Task<IActionResult> ImportCommit(StudentImportViewModel form)
        {
            try
            {
                if (form.NationalityLookupId == null)
                {
                    throw new InvalidOperationException(T("Choose the nationality every imported student gets.", "اختر الجنسية التي تُسنَد لكل طالب مستورد."));
                }

                var path = PathOf(form.Token);
                var rows = AccessRegisterReader.ReadRows(path, form.Table ?? throw new InvalidOperationException(T("Choose a table.", "اختر جدولاً.")));

                var imported = 0;
                var skipped = 0;
                var failed = new List<string>();
                for (var i = 0; i < rows.Count; i++)
                {
                    var candidate = Interpret(rows[i], form, i + 1);
                    if (candidate.Problem != null) { skipped++; continue; }

                    try
                    {
                        // Names go in twice: the Arabic register is the source, and the English half
                        // is required by the model. Transliterating here would invent four spellings
                        // per student that nobody chose — the same text is stored and left to be
                        // corrected by whoever knows how the family spells it.
                        await _students.RegisterStudentAsync(
                            candidate.FirstName, candidate.FatherName, candidate.GrandfatherName, candidate.FamilyName,
                            candidate.FirstName, candidate.FatherName, candidate.GrandfatherName, candidate.FamilyName,
                            candidate.Gender == "F" ? Gender.Female : Gender.Male,
                            DateTime.Parse(candidate.DateOfBirth!, CultureInfo.InvariantCulture),
                            form.NationalityLookupId.Value,
                            string.IsNullOrWhiteSpace(candidate.IdNumber) ? null : form.IdTypeLookupId,
                            string.IsNullOrWhiteSpace(candidate.IdNumber) ? null : candidate.IdNumber,
                            null,
                            HttpContext.RequestAborted);
                        imported++;
                    }
                    catch (Exception ex)
                    {
                        // One bad row does not stop the register: it is counted, named, and the rest
                        // go in. A half-finished import that reported success would be worse.
                        skipped++;
                        if (failed.Count < 5) { failed.Add($"#{candidate.Number} {candidate.FirstName} {candidate.FamilyName} — {UserMessage.For(ex, IsArabic)}"); }
                    }
                }

                TryDelete(path);
                TempData["Flash"] = T($"{imported} student(s) imported, {skipped} skipped.", $"استُورد {imported} طالباً، وتُخطّي {skipped}.");
                if (failed.Count > 0)
                {
                    TempData["Error"] = string.Join(" · ", failed);
                }

                return RedirectToAction(nameof(Index));
            }
            catch (OdbcException ex) { TempData["Error"] = Explain(ex); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return View(nameof(Import), await BuildImportAsync(form));
        }

        // ------------------------------------------------------------------ helpers

        private async Task<StudentImportViewModel> BuildImportAsync(StudentImportViewModel m)
        {
            m.Nationalities = await LookupAsync("Nationality");
            m.IdTypes = await LookupAsync("IdType");

            // The owner's register is Palestinian, so that is what the picker opens on when the
            // school's nationality list has such a value. It stays a picker: a fixed value would be
            // wrong the first time a non-Palestinian student was in the file.
            m.NationalityLookupId ??= m.Nationalities
                .FirstOrDefault(n => n.Ar.Contains("فلسطين") || n.En.Contains("Palestin", StringComparison.OrdinalIgnoreCase)).Id;
            m.IdTypeLookupId ??= m.IdTypes.FirstOrDefault(t => t.En.Contains("National", StringComparison.OrdinalIgnoreCase)).Id;
            if (m.NationalityLookupId == 0) { m.NationalityLookupId = null; }
            if (m.IdTypeLookupId == 0) { m.IdTypeLookupId = null; }
            return m;
        }

        private static string PathOf(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Contains('/') || token.Contains('\\') || token.Contains(".."))
            {
                throw new InvalidOperationException("Upload the file again.");
            }

            return Path.Combine(ImportFolder, token);
        }

        private string Explain(OdbcException ex) => AccessRegisterReader.IsMissingDriver(ex)
            ? AccessRegisterReader.MissingDriverMessage(IsArabic)
            : T("The Access file could not be read: ", "تعذّرت قراءة ملف أكسس: ") + UserMessage.For(ex, IsArabic);

        /// <summary>
        /// Fills any mapping the operator has not chosen, by looking for the column names Arabic
        /// registers actually use. A guess, offered as a starting position and visible in the
        /// preview before it can do any harm.
        /// </summary>
        private static void GuessMapping(StudentImportViewModel form)
        {
            string? Find(params string[] needles) => form.Columns.FirstOrDefault(
                c => needles.Any(n => c.Replace(" ", string.Empty).Contains(n, StringComparison.OrdinalIgnoreCase)));

            form.FirstNameColumn ??= Find("الاسمالاول", "الأول", "first", "fname", "name1");
            form.FatherNameColumn ??= Find("الأب", "الاب", "father", "name2");
            form.GrandfatherNameColumn ??= Find("الجد", "grand", "name3");
            form.FamilyNameColumn ??= Find("العائلة", "العايلة", "family", "last", "name4");
            form.FullNameColumn ??= Find("الاسمالرباعي", "الاسمكامل", "fullname", "الاسم");
            form.DateOfBirthColumn ??= Find("الميلاد", "تاريخالميلاد", "birth", "dob");
            form.GenderColumn ??= Find("الجنس", "النوع", "gender", "sex");
            form.IdNumberColumn ??= Find("الهوية", "رقمالهوية", "identity", "idno", "nationalid");
        }

        /// <summary>
        /// Turns one Access row into the student it would become, or into the reason it will not.
        /// Nothing is written here — this is the same function the preview and the commit both use,
        /// so what the operator saw is exactly what runs.
        /// </summary>
        private static StudentImportViewModel.PreviewRow Interpret(
            IReadOnlyDictionary<string, string?> row, StudentImportViewModel form, int number)
        {
            string Value(string? column) =>
                column != null && row.TryGetValue(column, out var v) ? (v ?? string.Empty).Trim() : string.Empty;

            var first = Value(form.FirstNameColumn);
            var father = Value(form.FatherNameColumn);
            var grandfather = Value(form.GrandfatherNameColumn);
            var family = Value(form.FamilyNameColumn);

            // A register that keeps the quad name in one column is common enough to be worth
            // splitting rather than refusing: four words is the whole name, anything shorter is
            // padded from the left because it is the family name that is most often missing.
            if (first.Length == 0 && form.FullNameColumn != null)
            {
                var parts = Value(form.FullNameColumn).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                first = parts.ElementAtOrDefault(0) ?? string.Empty;
                father = parts.ElementAtOrDefault(1) ?? string.Empty;
                grandfather = parts.ElementAtOrDefault(2) ?? string.Empty;
                family = string.Join(" ", parts.Skip(3));
            }

            var birth = ParseDate(Value(form.DateOfBirthColumn));
            var gender = ParseGender(Value(form.GenderColumn));
            var idNumber = Value(form.IdNumberColumn);

            string? problem = null;
            if (first.Length == 0 || family.Length == 0) { problem = "الاسم ناقص / name incomplete"; }
            else if (birth == null) { problem = "تاريخ الميلاد غير مقروء / unreadable date of birth"; }
            else if (gender == null) { problem = "الجنس غير مقروء / unreadable gender"; }

            return new StudentImportViewModel.PreviewRow(
                number, first, father, grandfather, family,
                birth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), gender, idNumber, problem);
        }

        /// <summary>Access hands dates back in several shapes; day-first is assumed where it is ambiguous, as an Arabic register writes it.</summary>
        private static DateTime? ParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) { return null; }

            var text = value.Replace('\\', '/').Replace('-', '/').Trim();
            string[] formats = { "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "yyyy/M/d" };
            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
            {
                return exact.Date;
            }

            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose)
                ? loose.Date
                : null;
        }

        /// <summary>"M"/"F", or null when the cell says something this cannot honestly read.</summary>
        private static string? ParseGender(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0) { return null; }
            if (text.StartsWith("ذك") || text.StartsWith("M", StringComparison.OrdinalIgnoreCase) || text == "1") { return "M"; }
            if (text.StartsWith("أن") || text.StartsWith("ان") || text.StartsWith("F", StringComparison.OrdinalIgnoreCase) || text == "2") { return "F"; }
            return null;
        }

        private static void TryDelete(string path)
        {
            try { if (System.IO.File.Exists(path)) { System.IO.File.Delete(path); } }
            catch (IOException) { /* a locked temp file is swept next time; not worth failing an import over */ }
        }
    }
}
