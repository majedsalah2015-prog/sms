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
using Sms.Domain.Employees;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;
using Sms.Web.Services;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Bringing a school's existing staff register across (owner request, 2026-08-23). Same three
    /// requests as the student import — upload, map, commit — because the mapping cannot be
    /// guessed reliably and the preview between the guess and the write is what makes a wrong
    /// column visible while it is still free to fix.
    /// <para>
    /// Fifteen fields landing in four records. Only the employee is mandatory: a row with a name,
    /// a birth date and a gender is imported even when its salary cell is unreadable, and the
    /// preview says which of the other three records that row will and will not produce. The
    /// alternative — refusing the whole row over a bad salary — loses the employee to save a
    /// number the school can type in later.
    /// </para>
    /// </summary>
    public partial class EmployeesController
    {
        /// <summary>Uploads live here until they are committed or the folder is next swept.</summary>
        private static string ImportFolder => Path.Combine(Path.GetTempPath(), "sms-employee-import");

        [HttpGet("import")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, ActionVerb.Create)]
        public async Task<IActionResult> Import()
        {
            return View(await BuildImportAsync(new EmployeeImportViewModel()));
        }

        /// <summary>Step one: take the file, keep a copy, and report what tables are in it.</summary>
        [HttpPost("import/upload")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, ActionVerb.Create)]
        public async Task<IActionResult> ImportUpload(IFormFile? register)
        {
            var m = new EmployeeImportViewModel();
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
            catch (OdbcException ex) { TempData["Error"] = ExplainImport(ex); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return View(nameof(Import), await BuildImportAsync(m));
        }

        /// <summary>Step two: read the chosen table's columns, and preview the mapping against real rows.</summary>
        [HttpPost("import/preview")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, ActionVerb.Create)]
        public async Task<IActionResult> ImportPreview(EmployeeImportViewModel form)
        {
            try
            {
                var path = PathOfImport(form.Token);
                form.Tables = AccessRegisterReader.ListTables(path);
                if (!string.IsNullOrWhiteSpace(form.Table))
                {
                    form.Columns = AccessRegisterReader.ListColumns(path, form.Table!);
                    GuessEmployeeMapping(form);

                    var rows = AccessRegisterReader.ReadRows(path, form.Table!);
                    form.TotalRows = rows.Count;

                    var preview = new List<EmployeeImportViewModel.PreviewRow>();
                    var ready = 0;
                    for (var i = 0; i < rows.Count; i++)
                    {
                        var candidate = InterpretEmployee(rows[i], form, i + 1);
                        if (candidate.Problem == null) { ready++; }
                        if (preview.Count < 15) { preview.Add(candidate); }
                    }

                    form.ReadyRows = ready;
                    form.SkippedRows = rows.Count - ready;
                    form.Preview = preview;
                }
            }
            catch (OdbcException ex) { TempData["Error"] = ExplainImport(ex); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return View(nameof(Import), await BuildImportAsync(form));
        }

        /// <summary>Step three: write the rows that were ready, and say what happened to the rest.</summary>
        [HttpPost("import/commit")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, ActionVerb.Create)]
        public async Task<IActionResult> ImportCommit(EmployeeImportViewModel form)
        {
            try
            {
                if (form.NationalityLookupId == null)
                {
                    throw new InvalidOperationException(T("Choose the nationality every imported employee gets.", "اختر الجنسية التي تُسنَد لكل موظف مستورد."));
                }

                if (form.PositionColumn != null && form.OrgUnitId == null)
                {
                    throw new InvalidOperationException(T("Choose the organisation unit the imported employees are assigned to.", "اختر الوحدة التنظيمية التي يُسنَد إليها الموظفون المستوردون."));
                }

                if (form.HireDateColumn != null && form.ContractEndDate == null)
                {
                    throw new InvalidOperationException(T("Choose the contract end date. A contract cannot be stored without one, and a made-up date is worse than being asked.", "اختر تاريخ نهاية العقد. لا يمكن حفظ عقد بدونه، وتاريخ يخترعه النظام أسوأ من أن يُسأل عنه."));
                }

                var path = PathOfImport(form.Token);
                var rows = AccessRegisterReader.ReadRows(path, form.Table ?? throw new InvalidOperationException(T("Choose a table.", "اختر جدولاً.")));

                var imported = 0;
                var skipped = 0;
                var contracts = 0;
                var assignments = 0;
                var qualifications = 0;
                var failed = new List<string>();

                for (var i = 0; i < rows.Count; i++)
                {
                    var candidate = InterpretEmployee(rows[i], form, i + 1);
                    if (candidate.Problem != null) { skipped++; continue; }

                    try
                    {
                        // The register is Arabic and the model wants both halves. The same text goes
                        // in twice rather than being transliterated: four invented spellings per
                        // employee is not data, and whoever knows how the family spells it can fix
                        // the English half in the file screen.
                        var employee = await _employees.RegisterEmployeeAsync(
                            candidate.FirstName, candidate.FatherName, candidate.GrandfatherName, candidate.FamilyName,
                            candidate.FirstName, candidate.FatherName, candidate.GrandfatherName, candidate.FamilyName,
                            candidate.Gender == "F" ? Gender.Female : Gender.Male,
                            DateTime.Parse(candidate.DateOfBirth!, CultureInfo.InvariantCulture),
                            form.NationalityLookupId.Value,
                            null,
                            string.IsNullOrWhiteSpace(candidate.IdNumber) ? null : form.IdTypeLookupId,
                            string.IsNullOrWhiteSpace(candidate.IdNumber) ? null : candidate.IdNumber,
                            null,
                            HttpContext.RequestAborted);
                        imported++;

                        // The three fields doc/Modules/12 §7 does not list. T1 with a required
                        // reason, and this is a Modified save, so the reason is set here — "the
                        // register it came from" is the honest answer to why the value is what it is.
                        if (candidate.MaritalStatus != null || candidate.BankName != null || candidate.BankAccountNo != null)
                        {
                            _audit.Reason = T($"Imported from {form.OriginalFileName}", $"مستورد من {form.OriginalFileName}");
                            await _employees.UpdatePersonalDetailsAsync(
                                employee.Id,
                                candidate.MaritalStatus == null ? null : Enum.Parse<MaritalStatus>(candidate.MaritalStatus),
                                candidate.BankName, candidate.BankAccountNo, HttpContext.RequestAborted);
                        }

                        if (!candidate.IsActive)
                        {
                            await _employees.ChangeStatusAsync(employee.Id, EmployeeStatus.Terminated, HttpContext.RequestAborted);
                        }

                        // Each of the three below is skipped, not failed, when its own columns were
                        // not readable — the note on the preview row already said so.
                        if (candidate.HireDate != null && form.ContractEndDate != null)
                        {
                            await _employees.DefineContractAsync(
                                employee.Id,
                                candidate.ContractType == null ? form.DefaultContractType : Enum.Parse<ContractType>(candidate.ContractType),
                                DateTime.Parse(candidate.HireDate, CultureInfo.InvariantCulture),
                                form.ContractEndDate.Value,
                                candidate.Salary ?? 0m,
                                null,
                                HttpContext.RequestAborted);
                            contracts++;
                        }

                        if (candidate.Position != null && form.OrgUnitId != null)
                        {
                            var positionId = await ResolvePositionAsync(candidate.Position);
                            if (positionId != null)
                            {
                                await _employees.AssignPositionAsync(
                                    employee.Id, form.OrgUnitId.Value, positionId.Value, null,
                                    candidate.HireDate == null ? _clock.UtcNow.Date : DateTime.Parse(candidate.HireDate, CultureInfo.InvariantCulture),
                                    HttpContext.RequestAborted);
                                assignments++;
                            }
                        }

                        if (candidate.Qualification != null)
                        {
                            await _employees.AddQualificationAsync(
                                employee.Id, candidate.Qualification, candidate.Qualification,
                                candidate.GraduationDate == null ? DateTime.Parse(candidate.DateOfBirth!, CultureInfo.InvariantCulture).AddYears(22) : DateTime.Parse(candidate.GraduationDate, CultureInfo.InvariantCulture),
                                false, candidate.University, null, HttpContext.RequestAborted);
                            qualifications++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // One bad row does not stop the register: it is counted, named, and the rest
                        // go in. A half-finished import reporting success would be worse.
                        skipped++;
                        if (failed.Count < 5) { failed.Add($"#{candidate.Number} {candidate.FirstName} {candidate.FamilyName} — {UserMessage.For(ex, IsArabic)}"); }
                    }
                }

                TryDeleteImport(path);
                TempData["Flash"] = T(
                    $"{imported} employee(s) imported ({contracts} contracts, {assignments} assignments, {qualifications} qualifications), {skipped} skipped.",
                    $"استُورد {imported} موظفاً ({contracts} عقداً، {assignments} تعييناً، {qualifications} مؤهلاً)، وتُخطّي {skipped}.");
                if (failed.Count > 0)
                {
                    TempData["Error"] = string.Join(" · ", failed);
                }

                return RedirectToAction(nameof(Index));
            }
            catch (OdbcException ex) { TempData["Error"] = ExplainImport(ex); }
            catch (InvalidOperationException ex) { TempData["Error"] = UserMessage.For(ex, IsArabic); }

            return View(nameof(Import), await BuildImportAsync(form));
        }

        // ------------------------------------------------------------------ helpers

        private async Task<EmployeeImportViewModel> BuildImportAsync(EmployeeImportViewModel m)
        {
            m.Nationalities = await LookupAsync("Nationality");
            m.IdTypes = await LookupAsync("IdType");
            m.Positions = await LookupAsync("JobTitle");
            m.OrgUnits = await _db.OrgUnits.AsNoTracking().OrderBy(u => u.NameAr)
                .Select(u => new ValueTuple<int, string, string>(u.Id, u.NameAr, u.NameEn)).ToListAsync();

            m.NationalityLookupId ??= m.Nationalities
                .FirstOrDefault(n => n.Ar.Contains("فلسطين") || n.En.Contains("Palestin", StringComparison.OrdinalIgnoreCase)).Id;
            m.IdTypeLookupId ??= m.IdTypes.FirstOrDefault(t => t.En.Contains("National", StringComparison.OrdinalIgnoreCase)).Id;
            if (m.NationalityLookupId == 0) { m.NationalityLookupId = null; }
            if (m.IdTypeLookupId == 0) { m.IdTypeLookupId = null; }
            if (m.OrgUnitId == null && m.OrgUnits.Count == 1) { m.OrgUnitId = m.OrgUnits[0].Id; }
            return m;
        }

        /// <summary>
        /// Matches a job title from the register against the school's own JobTitle list, in either
        /// language. Returns null when nothing matches, which drops the assignment and keeps the
        /// employee — inventing a lookup value from a spreadsheet cell would fill the list with
        /// every spelling the old register used.
        /// </summary>
        private async Task<int?> ResolvePositionAsync(string title)
        {
            var wanted = title.Replace(" ", string.Empty);
            var positions = await LookupAsync("JobTitle");
            foreach (var p in positions)
            {
                if (p.Ar.Replace(" ", string.Empty).Equals(wanted, StringComparison.OrdinalIgnoreCase)
                    || p.En.Replace(" ", string.Empty).Equals(wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return p.Id;
                }
            }

            return null;
        }

        private static string PathOfImport(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Contains('/') || token.Contains('\\') || token.Contains(".."))
            {
                throw new InvalidOperationException("Upload the file again.");
            }

            return Path.Combine(ImportFolder, token);
        }

        private string ExplainImport(OdbcException ex) => AccessRegisterReader.IsMissingDriver(ex)
            ? AccessRegisterReader.MissingDriverMessage(IsArabic)
            : T("The Access file could not be read: ", "تعذّرت قراءة ملف أكسس: ") + UserMessage.For(ex, IsArabic);

        /// <summary>
        /// Fills any mapping the operator has not chosen, by looking for the column names Arabic
        /// staff registers actually use. A guess, offered as a starting position and visible in the
        /// preview before it can do any harm.
        /// </summary>
        private static void GuessEmployeeMapping(EmployeeImportViewModel form)
        {
            string? Find(params string[] needles) => form.Columns.FirstOrDefault(
                c => needles.Any(n => c.Replace(" ", string.Empty).Contains(n, StringComparison.OrdinalIgnoreCase)));

            form.FirstNameColumn ??= Find("الاسمالاول", "الأول", "first", "fname", "name1");
            form.FatherNameColumn ??= Find("الأب", "الاب", "father", "name2");
            form.GrandfatherNameColumn ??= Find("الجد", "grand", "name3");
            form.FamilyNameColumn ??= Find("العائلة", "العايلة", "family", "last", "name4");
            form.FullNameColumn ??= Find("اسمالموظف", "الاسمالرباعي", "الاسمكامل", "fullname", "الاسم");
            form.IdNumberColumn ??= Find("الهوية", "رقمالهوية", "identity", "idno", "nationalid");
            form.DateOfBirthColumn ??= Find("الميلاد", "تاريخالميلاد", "birth", "dob");
            form.GenderColumn ??= Find("الجنس", "النوع", "gender", "sex");
            form.ActiveColumn ??= Find("فعال", "نشط", "active", "حالةالموظف");
            form.MaritalStatusColumn ??= Find("الحالةالاجتماعية", "الحالة", "marital", "social");
            form.BankNameColumn ??= Find("البنك", "المصرف", "bank");
            form.BankAccountColumn ??= Find("رقمالحساب", "الحساب", "account", "iban");
            form.HireDateColumn ??= Find("التعيين", "تاريخالتعيين", "hire", "join", "المباشرة");
            form.ContractTypeColumn ??= Find("نوعالعقد", "العقد", "contract");
            form.SalaryColumn ??= Find("الراتب", "الاجر", "الأجر", "salary", "wage");
            form.PositionColumn ??= Find("الوظيفة", "المسمى", "position", "jobtitle", "job");
            form.QualificationColumn ??= Find("المؤهل", "الشهادة", "qualification", "degree");
            form.UniversityColumn ??= Find("الجامعة", "university", "college", "institution");
            form.GraduationDateColumn ??= Find("التخرج", "تاريخالتخرج", "graduation", "graduated");
        }

        /// <summary>
        /// Turns one Access row into the records it would become, or into the reason it will not.
        /// Nothing is written here — the preview and the commit both call this, so what the operator
        /// read is exactly what runs.
        /// </summary>
        private static EmployeeImportViewModel.PreviewRow InterpretEmployee(
            IReadOnlyDictionary<string, string?> row, EmployeeImportViewModel form, int number)
        {
            string Value(string? column) =>
                column != null && row.TryGetValue(column, out var v) ? (v ?? string.Empty).Trim() : string.Empty;

            var first = Value(form.FirstNameColumn);
            var father = Value(form.FatherNameColumn);
            var grandfather = Value(form.GrandfatherNameColumn);
            var family = Value(form.FamilyNameColumn);

            if (first.Length == 0 && form.FullNameColumn != null)
            {
                var parts = Value(form.FullNameColumn).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                first = parts.ElementAtOrDefault(0) ?? string.Empty;
                father = parts.ElementAtOrDefault(1) ?? string.Empty;
                grandfather = parts.ElementAtOrDefault(2) ?? string.Empty;
                family = string.Join(" ", parts.Skip(3));
            }

            var birth = ParseImportDate(Value(form.DateOfBirthColumn));
            var gender = ParseImportGender(Value(form.GenderColumn));
            var idNumber = Value(form.IdNumberColumn);
            var hire = ParseImportDate(Value(form.HireDateColumn));
            var graduation = ParseImportDate(Value(form.GraduationDateColumn));
            var salary = ParseImportMoney(Value(form.SalaryColumn));
            var marital = ParseMaritalStatus(Value(form.MaritalStatusColumn));
            var contractType = ParseContractType(Value(form.ContractTypeColumn));
            var active = ParseActive(Value(form.ActiveColumn));
            var bank = Value(form.BankNameColumn);
            var account = Value(form.BankAccountColumn);
            var position = Value(form.PositionColumn);
            var qualification = Value(form.QualificationColumn);
            var university = Value(form.UniversityColumn);

            // Only the employee's own identity can refuse a row. Everything else is a note.
            string? problem = null;
            if (first.Length == 0 || family.Length == 0) { problem = "الاسم ناقص / name incomplete"; }
            else if (birth == null) { problem = "تاريخ الميلاد غير مقروء / unreadable date of birth"; }
            else if (gender == null) { problem = "الجنس غير مقروء / unreadable gender"; }

            var notes = new List<string>();
            if (form.HireDateColumn != null && hire == null) { notes.Add("لا عقد: تاريخ التعيين غير مقروء / no contract: unreadable hire date"); }
            if (form.SalaryColumn != null && salary == null) { notes.Add("الراتب غير مقروء / unreadable salary"); }
            if (form.PositionColumn != null && position.Length == 0) { notes.Add("لا تعيين: الوظيفة فارغة / no assignment: position empty"); }
            if (form.QualificationColumn != null && qualification.Length == 0) { notes.Add("لا مؤهل / no qualification"); }
            if (form.MaritalStatusColumn != null && marital == null && Value(form.MaritalStatusColumn).Length > 0) { notes.Add("الحالة الاجتماعية غير مقروءة / unreadable marital status"); }

            return new EmployeeImportViewModel.PreviewRow(
                number, first, father, grandfather, family,
                birth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), gender,
                idNumber.Length == 0 ? null : idNumber, active,
                marital?.ToString(), bank.Length == 0 ? null : bank, account.Length == 0 ? null : account,
                hire?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), contractType?.ToString(), salary,
                position.Length == 0 ? null : position,
                qualification.Length == 0 ? null : qualification,
                university.Length == 0 ? null : university,
                graduation?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                problem, notes);
        }

        /// <summary>Access hands dates back in several shapes; day-first is assumed where it is ambiguous, as an Arabic register writes it.</summary>
        private static DateTime? ParseImportDate(string value)
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
        private static string? ParseImportGender(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0) { return null; }
            if (text.StartsWith("ذك") || text.StartsWith("M", StringComparison.OrdinalIgnoreCase) || text == "1") { return "M"; }
            if (text.StartsWith("أن") || text.StartsWith("ان") || text.StartsWith("F", StringComparison.OrdinalIgnoreCase) || text == "2") { return "F"; }
            return null;
        }

        /// <summary>
        /// A salary cell carrying a currency symbol, thousands separators or Arabic-Indic digits is
        /// still a salary. One that carries a word is not, and returns null rather than zero — a
        /// contract silently stored at zero pay is the kind of error nobody finds until payday.
        /// </summary>
        private static decimal? ParseImportMoney(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) { return null; }

            var cleaned = new string(value
                .Select(c => c >= '٠' && c <= '٩' ? (char)(c - '٠' + '0') : c)
                .Where(c => char.IsDigit(c) || c == '.' || c == '-')
                .ToArray());

            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount >= 0
                ? amount
                : null;
        }

        /// <summary>An empty column means every row is active — a register that does not track it has nobody switched off.</summary>
        private static bool ParseActive(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0) { return true; }
            if (text.StartsWith("لا") || text.StartsWith("غير") || text.StartsWith("منتهي") || text == "0") { return false; }
            if (text.Equals("false", StringComparison.OrdinalIgnoreCase) || text.Equals("no", StringComparison.OrdinalIgnoreCase)) { return false; }
            return true;
        }

        private static MaritalStatus? ParseMaritalStatus(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0) { return null; }
            if (text.StartsWith("أعزب") || text.StartsWith("اعزب") || text.StartsWith("عزب") || text.StartsWith("single", StringComparison.OrdinalIgnoreCase)) { return MaritalStatus.Single; }
            if (text.StartsWith("متزوج") || text.StartsWith("married", StringComparison.OrdinalIgnoreCase)) { return MaritalStatus.Married; }
            if (text.StartsWith("مطلق") || text.StartsWith("divorc", StringComparison.OrdinalIgnoreCase)) { return MaritalStatus.Divorced; }
            if (text.StartsWith("أرمل") || text.StartsWith("ارمل") || text.StartsWith("widow", StringComparison.OrdinalIgnoreCase)) { return MaritalStatus.Widowed; }
            return null;
        }

        private static ContractType? ParseContractType(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0) { return null; }
            if (text.Contains("جزئي") || text.Contains("part", StringComparison.OrdinalIgnoreCase)) { return ContractType.PartTime; }
            if (text.Contains("فصل") || text.Contains("مؤقت") || text.Contains("term", StringComparison.OrdinalIgnoreCase)) { return ContractType.Term; }
            if (text.Contains("كلي") || text.Contains("دائم") || text.Contains("full", StringComparison.OrdinalIgnoreCase)) { return ContractType.FullTime; }
            return null;
        }

        private static void TryDeleteImport(string path)
        {
            try { if (System.IO.File.Exists(path)) { System.IO.File.Delete(path); } }
            catch (IOException) { /* a locked temp file is swept next time; not worth failing an import over */ }
        }
    }
}
