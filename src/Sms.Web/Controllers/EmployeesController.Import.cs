using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common;
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
    /// An Excel workbook is read as readily as an Access database (owner request, 2026-08-27) and
    /// is the ordinary case: a staff list is a spreadsheet far more often than it is a database.
    /// <see cref="RegisterFile"/> hides which of the two arrived, so from the sheet-picker onwards
    /// this is one screen and not two. Excel needs nothing installed on the server, where Access
    /// needs Microsoft's ACE engine in the right bitness — see <see cref="WorkbookRegisterReader"/>.
    /// </para>
    /// <para>
    /// A workbook writes the whole name in one cell, so splitting it into the four this product
    /// stores is the main path through <see cref="InterpretEmployee"/> rather than a fallback, and
    /// the split itself is <see cref="PersonNameSplitter"/> — a tested engine, because getting the
    /// family name out of the wrong word is the kind of mistake that is only noticed a term later.
    /// </para>
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

        /// <summary>
        /// One gigabyte. Not a guess at what is reasonable — it is what an Access file grows to
        /// after twenty years of a school never compacting it, and the cost of allowing it is
        /// nothing: the upload is streamed to a temporary file, never held in memory. Kestrel's
        /// 30 MB default closes the connection before any code here runs, which shows the operator
        /// a network error and says nothing anywhere about why.
        /// <para>
        /// The student import states the same number for the same reason. Deliberately not shared
        /// between them yet: that constant is part of an in-flight change to
        /// <c>StudentsController.Import</c>, and one screen's upload limit is not worth coupling
        /// this commit to another epic's unfinished work. Fold the two together when that lands.
        /// </para>
        /// </summary>
        internal const long RegisterUploadLimit = 1_073_741_824L;

        [HttpGet("import")]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, ActionVerb.Create)]
        public async Task<IActionResult> Import()
        {
            return View(await BuildImportAsync(new EmployeeImportViewModel()));
        }

        /// <summary>
        /// Step one: take the file, keep a copy, and report what tables are in it.
        /// <para>
        /// Raised size limits, because what is being posted is a whole register rather than a form:
        /// Kestrel's 30 MB default closes the connection on a large one before any code here runs.
        /// See <see cref="RegisterUploadLimit"/>.
        /// </para>
        /// </summary>
        [HttpPost("import/upload")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(RegisterUploadLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = RegisterUploadLimit)]
        [RequirePermission(ScreenCatalog.Modules.Employees, ScreenCatalog.Employees.Directory, ActionVerb.Create)]
        public async Task<IActionResult> ImportUpload(IFormFile? register)
        {
            var m = new EmployeeImportViewModel();
            try
            {
                if (register == null || register.Length == 0)
                {
                    throw new InvalidOperationException(T("Choose a file first.", "اختر ملفاً أولاً."));
                }

                var extension = Path.GetExtension(register.FileName).ToLowerInvariant();
                if (extension == ".xls")
                {
                    // Worth its own sentence: .xls is a different format from .xlsx, not an older
                    // spelling of it, and "unsupported file" would send the operator looking for a
                    // setting rather than to File → Save As, which is the whole fix.
                    throw new InvalidOperationException(T(
                        "That is the old Excel format (.xls). Open it in Excel and save it as .xlsx, then upload it again.",
                        "هذه صيغة إكسل القديمة (xls.). افتح الملف في إكسل واحفظه بصيغة xlsx. ثم ارفعه مرة أخرى."));
                }

                if (!RegisterFile.IsSupported(register.FileName))
                {
                    throw new InvalidOperationException(T(
                        "That is not an Excel workbook (.xlsx or .xlsm) or an Access database (.mdb or .accdb).",
                        "هذا ليس مصنّف إكسل (xlsx. أو xlsm.) ولا قاعدة بيانات أكسس (mdb. أو accdb.)."));
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
                m.Tables = RegisterFile.ListTables(path);
                if (m.Tables.Count == 0)
                {
                    TempData["Error"] = T("That file has nothing to read.", "لا يوجد في هذا الملف ما يُقرأ.");
                }
                else if (m.Tables.Count == 1)
                {
                    // One sheet is the ordinary shape of a staff list, and making the operator pick
                    // it out of a list of one is a step that only ever has one right answer.
                    m.Table = m.Tables[0];
                    m.Columns = RegisterFile.ListColumns(path, m.Table);
                    GuessEmployeeMapping(m);
                }
            }
            catch (OdbcException ex) { TempData["Error"] = ExplainImport(ex); }
            catch (InvalidDataException ex) { TempData["Error"] = ExplainWorkbook(ex); }
            catch (XmlException ex) { TempData["Error"] = ExplainWorkbook(ex); }
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
                form.Tables = RegisterFile.ListTables(path);
                if (!string.IsNullOrWhiteSpace(form.Table))
                {
                    form.Columns = RegisterFile.ListColumns(path, form.Table!);
                    if (!KeepMappingsThatStillExist(form) || !form.MappingChosen)
                    {
                        GuessEmployeeMapping(form);
                    }

                    var rows = RegisterFile.ReadRows(path, form.Table!);
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
            catch (InvalidDataException ex) { TempData["Error"] = ExplainWorkbook(ex); }
            catch (XmlException ex) { TempData["Error"] = ExplainWorkbook(ex); }
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
                var rows = RegisterFile.ReadRows(path, form.Table ?? throw new InvalidOperationException(T("Choose a sheet or table.", "اختر ورقة أو جدولاً.")));

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
                            candidate.Mobile,
                            candidate.WhatsApp,
                            HttpContext.RequestAborted);
                        imported++;

                        // The three fields doc/Modules/12 §7 does not list. T1 with a required
                        // reason, and this is a Modified save, so the reason is set here — "the
                        // register it came from" is the honest answer to why the value is what it is.
                        if (candidate.MaritalStatus != null || candidate.BankName != null || candidate.BankAccountNo != null
                            || candidate.Address != null || candidate.OriginTown != null || candidate.SpouseIdNo != null
                            || candidate.PalPayWalletNo != null || candidate.JawwalPayWalletNo != null)
                        {
                            _audit.Reason = T($"Imported from {form.OriginalFileName}", $"مستورد من {form.OriginalFileName}");

                            // Everything this method carries is passed every time, because it writes
                            // the whole block: a field left out of the call is a field blanked. On a
                            // freshly registered employee that is a no-op, and stating it here is
                            // what keeps it one if the import ever runs over an existing record.
                            await _employees.UpdatePersonalDetailsAsync(
                                employee.Id,
                                candidate.MaritalStatus == null ? null : Enum.Parse<MaritalStatus>(candidate.MaritalStatus),

                                // No catalogue id: the spreadsheet carries a bank as text and nothing
                                // here matches text to a lookup — for any category, not just this one.
                                // The name lands in the free-text column the employee file falls back
                                // to, and a registrar can replace it with a catalogue value later.
                                null, candidate.BankName, candidate.BankAccountNo,
                                candidate.Address, candidate.OriginTown,
                                candidate.SpouseIdNo == null ? null : form.SpouseIdTypeLookupId,
                                candidate.SpouseIdNo,
                                candidate.PalPayWalletNo, candidate.JawwalPayWalletNo,
                                HttpContext.RequestAborted);
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
                                false, candidate.University, null, cancellationToken: HttpContext.RequestAborted);
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
            catch (InvalidDataException ex) { TempData["Error"] = ExplainWorkbook(ex); }
            catch (XmlException ex) { TempData["Error"] = ExplainWorkbook(ex); }
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
        /// Why a workbook would not open. Almost always the same cause — a file that was renamed to
        /// .xlsx instead of being saved as one, or one Excel has put a password on — so the sentence
        /// names both before it prints the technical detail, which on its own says "End of Central
        /// Directory record could not be found" and helps nobody.
        /// </summary>
        private string ExplainWorkbook(Exception ex) => T(
            "The Excel file could not be read. Check that it really is an .xlsx workbook and is not password-protected. ",
            "تعذّرت قراءة ملف إكسل. تأكّد أنه مصنّف xlsx. فعلاً وأنه غير محمي بكلمة مرور. ") + UserMessage.For(ex, IsArabic);

        /// <summary>
        /// Fills any mapping the operator has not chosen, by looking for the column names Arabic
        /// staff registers actually use. A guess, offered as a starting position and visible in the
        /// preview before it can do any harm.
        /// </summary>
        /// <summary>
        /// Drops every mapping that names a column the chosen sheet does not have, and says whether
        /// any mapping survived.
        /// <para>
        /// Two things need it. Switching sheets leaves every field pointing at a column name from
        /// the previous one, and a mapping onto a column that is gone silently imports blanks. And
        /// it is what decides whether to guess again: guessing only when <em>nothing</em> is mapped
        /// means a fresh sheet still gets its guesses, while a mapping the operator has answered is
        /// left exactly as they left it — including the fields they set back to "— none —".
        /// </para>
        /// <para>
        /// Found by reflection over the view model's own <c>*Column</c> properties rather than
        /// listed here: the list would be a second copy of the mapping, and the copy that gets
        /// forgotten when a twenty-first column is added is this one — silently, because a field
        /// missing from it simply never clears.
        /// </para>
        /// </summary>
        private static bool KeepMappingsThatStillExist(EmployeeImportViewModel form)
        {
            var columns = new HashSet<string>(form.Columns, StringComparer.OrdinalIgnoreCase);
            var mapped = false;

            foreach (var property in typeof(EmployeeImportViewModel).GetProperties())
            {
                if (property.PropertyType != typeof(string)
                    || !property.Name.EndsWith("Column", StringComparison.Ordinal)
                    || !property.CanWrite)
                {
                    continue;
                }

                var value = (string?)property.GetValue(form);
                if (value == null) { continue; }

                if (columns.Contains(value)) { mapped = true; }
                else { property.SetValue(form, null); }
            }

            return mapped;
        }

        private static void GuessEmployeeMapping(EmployeeImportViewModel form)
        {
            // A column already spoken for — by the operator or by an earlier guess — is not offered
            // again. Without that, "الاسم الأول" answers both the first-name needle and the
            // whole-name one, and the screen shows one column mapped to two different fields.
            var used = new HashSet<string>(new[]
            {
                form.FullNameColumn, form.FirstNameColumn, form.FatherNameColumn, form.GrandfatherNameColumn,
                form.FamilyNameColumn, form.IdNumberColumn, form.DateOfBirthColumn, form.GenderColumn,
                form.MobileColumn, form.ActiveColumn, form.MaritalStatusColumn, form.BankNameColumn,
                form.BankAccountColumn, form.HireDateColumn, form.ContractTypeColumn, form.SalaryColumn,
                form.PositionColumn, form.QualificationColumn, form.UniversityColumn, form.GraduationDateColumn,
                form.WhatsAppColumn, form.AddressColumn, form.OriginTownColumn, form.SpouseIdNoColumn,
                form.PalPayWalletColumn, form.JawwalPayWalletColumn,
            }.Where(c => c != null).Select(c => c!), StringComparer.OrdinalIgnoreCase);

            string? Find(params string[] needles)
            {
                var match = form.Columns.FirstOrDefault(
                    c => !used.Contains(c) && needles.Any(n => Normalize(c).Contains(Normalize(n), StringComparison.OrdinalIgnoreCase)));
                if (match != null) { used.Add(match); }
                return match;
            }

            form.FirstNameColumn ??= Find("الاسم الاول", "الأول", "first", "fname", "name1");
            form.FatherNameColumn ??= Find("الأب", "الاب", "father", "name2");
            form.GrandfatherNameColumn ??= Find("الجد", "grand", "name3");
            form.FamilyNameColumn ??= Find("العائلة", "العايلة", "family", "last", "name4");

            // The whole-name guess is deliberately in two halves. A header that says outright it
            // holds the whole name is claimed whatever else was found; the bare word "الاسم" is
            // claimed only when no first-name column turned up, because in a register that splits
            // the name it is the first of the four columns and not the name entire.
            form.FullNameColumn ??= Find("اسم الموظف", "الاسم الرباعي", "الاسم الثلاثي", "الاسم الكامل", "الاسم كامل", "fullname", "full name");
            if (form.FullNameColumn == null && form.FirstNameColumn == null)
            {
                form.FullNameColumn = Find("الاسم", "اسم", "name");
            }

            // The spouse's document is claimed before the employee's own, so that a register
            // carrying both columns cannot have "رقم هوية الزوج" answer the identity-number needle
            // and leave the employee's own number unmapped.
            form.SpouseIdNoColumn ??= Find("هوية الزوج", "هوية الزوجة", "هوية القرين", "spouse id", "spouse");
            form.IdNumberColumn ??= Find("الهوية", "رقم الهوية", "البطاقة", "identity", "idno", "id no", "nationalid", "national id");
            form.DateOfBirthColumn ??= Find("الميلاد", "تاريخ الميلاد", "المواليد", "الولادة", "birth", "dob");
            form.GenderColumn ??= Find("الجنس", "النوع", "gender", "sex");
            // WhatsApp before the mobile: "رقم الواتس اب" answers neither of the other's needles, but
            // a register that lists one number under both headings should give the mobile away first.
            form.WhatsAppColumn ??= Find("الواتس", "واتس اب", "واتساب", "whatsapp", "whats app");
            form.MobileColumn ??= Find("الجوال", "الجوّال", "الموبايل", "النقال", "الخلوي", "الهاتف", "التلفون", "mobile", "phone", "cell", "tel");
            form.AddressColumn ??= Find("العنوان", "مكان السكن", "السكن", "محل الاقامة", "address");
            form.OriginTownColumn ??= Find("البلدة الاصلية", "البلدة", "بلدة المنشأ", "الاصلية", "المنشأ", "origin", "hometown");
            form.PalPayWalletColumn ??= Find("بالي بي", "باليبي", "palpay", "pal pay");
            form.JawwalPayWalletColumn ??= Find("جوال بي", "جوالبي", "jawwalpay", "jawwal pay");
            form.ActiveColumn ??= Find("فعال", "نشط", "active", "حالة الموظف", "على رأس عمله");
            form.MaritalStatusColumn ??= Find("الحالة الاجتماعية", "الحالة", "marital", "social");
            form.BankNameColumn ??= Find("البنك", "المصرف", "bank");
            form.BankAccountColumn ??= Find("رقم الحساب", "الحساب", "الايبان", "account", "iban");
            form.HireDateColumn ??= Find("التعيين", "تاريخ التعيين", "المباشرة", "الالتحاق", "بداية العمل", "hire", "join", "start date");
            form.ContractTypeColumn ??= Find("نوع العقد", "العقد", "الدوام", "contract");
            form.SalaryColumn ??= Find("الراتب", "الاجر", "الأجر", "المرتب", "salary", "wage");
            form.PositionColumn ??= Find("الوظيفة", "المسمى", "العمل", "position", "jobtitle", "job title", "job");
            form.QualificationColumn ??= Find("المؤهل", "الشهادة", "الدرجة العلمية", "qualification", "degree");
            form.UniversityColumn ??= Find("الجامعة", "الكلية", "المعهد", "university", "college", "institution");
            form.GraduationDateColumn ??= Find("التخرج", "تاريخ التخرج", "graduation", "graduated");
        }

        /// <summary>
        /// A column heading reduced to the letters that carry its meaning, so a guess is not lost to
        /// how somebody typed it.
        /// <para>
        /// "الأسم" and "الاسم" are the same word with and without a hamza, and both are typed in
        /// every register in circulation; so are "الحالة الإجتماعية" and "الحالة الاجتماعية", and
        /// "الجوّال" with its shadda. Matching the raw text means the guess quietly fails on half
        /// the files and the operator maps twenty columns by hand believing the screen simply does
        /// not guess.
        /// </para>
        /// </summary>
        private static string Normalize(string text)
        {
            var stripped = new System.Text.StringBuilder(text.Length);
            foreach (var c in text)
            {
                // Tashkeel and tatweel are decoration over the letters, not letters.
                if (c >= 0x064B && c <= 0x0652) { continue; }
                if (c == 0x0640 || char.IsWhiteSpace(c) || c == '_' || c == '-' || c == '.') { continue; }

                stripped.Append(c switch
                {
                    (char)0x0623 or (char)0x0625 or (char)0x0622 or (char)0x0671 => (char)0x0627, // أ إ آ ٱ  ->  ا
                    (char)0x0629 => (char)0x0647,                                                 // ة        ->  ه
                    (char)0x0649 => (char)0x064A,                                                 // ى        ->  ي
                    _ => c,
                });
            }

            return stripped.ToString();
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

            static string Either(string own, string fromWholeName) => own.Length > 0 ? own : fromWholeName;

            // The register writes the name in one cell and this product stores it in four, so the
            // split is the ordinary path and not the fallback it used to be. A column of its own
            // still wins for the part it names: a file with both a whole-name column and a separate
            // family-name column knows something about the family name that no split could infer.
            var whole = PersonNameSplitter.Split(Value(form.FullNameColumn));
            var first = Either(Value(form.FirstNameColumn), whole.First);
            var father = Either(Value(form.FatherNameColumn), whole.Father);
            var grandfather = Either(Value(form.GrandfatherNameColumn), whole.Grandfather);
            var family = Either(Value(form.FamilyNameColumn), whole.Family);

            // Read first, then fall back to what the operator chose for the whole file — and only
            // where the file itself is silent. Both are noted per row further down, so an assumption
            // is something the operator sees in the preview rather than discovers later in a record.
            var readBirth = ParseImportDate(Value(form.DateOfBirthColumn));
            var readGender = ParseImportGender(Value(form.GenderColumn));
            var birth = readBirth ?? form.DefaultDateOfBirth;
            var gender = readGender ?? (form.DefaultGender == null ? null : form.DefaultGender == Gender.Female ? "F" : "M");
            var idNumber = Value(form.IdNumberColumn);
            var mobile = Value(form.MobileColumn);
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

            var notes = new List<string>();

            // A cell wider than the column it is going into is two values written into one, or a
            // whole paragraph in the address box. It is dropped and said out loud rather than
            // truncated: half a phone number reaches a stranger, and half an address reaches nobody.
            string? Within(string value, int max, string arabic, string english)
            {
                if (value.Length == 0) { return null; }
                if (value.Length <= max) { return value; }
                notes.Add($"{arabic} أطول من الحقل ولن يُحفظ / {english} too long, not stored");
                return null;
            }

            var whatsApp = Within(Value(form.WhatsAppColumn), 20, "رقم الواتس اب", "WhatsApp number");
            var address = Within(Value(form.AddressColumn), 250, "العنوان", "address");
            var originTown = Within(Value(form.OriginTownColumn), 100, "البلدة الأصلية", "town of origin");
            var spouseIdNo = Within(Value(form.SpouseIdNoColumn), 30, "رقم هوية الزوج/الزوجة", "spouse ID number");
            var palPay = Within(Value(form.PalPayWalletColumn), 20, "محفظة بالي بي", "PalPay wallet");
            var jawwalPay = Within(Value(form.JawwalPayWalletColumn), 20, "محفظة جوال بي", "JawwalPay wallet");

            // Only the employee's own identity can refuse a row. Everything else is a note.
            string? problem = null;
            if (first.Length == 0 || family.Length == 0) { problem = "الاسم ناقص — يحتاج اسماً وعائلة / name incomplete — needs a first and a family name"; }
            else if (birth == null) { problem = "تاريخ الميلاد غير مقروء / unreadable date of birth"; }
            else if (gender == null) { problem = "الجنس غير مقروء / unreadable gender"; }

            if (readBirth == null && birth != null) { notes.Add("تاريخ الميلاد مفترَض / date of birth assumed"); }
            if (readGender == null && gender != null) { notes.Add("الجنس مفترَض / gender assumed"); }
            if (form.HireDateColumn != null && hire == null) { notes.Add("لا عقد: تاريخ التعيين غير مقروء / no contract: unreadable hire date"); }
            if (form.SalaryColumn != null && salary == null) { notes.Add("الراتب غير مقروء / unreadable salary"); }
            if (form.PositionColumn != null && position.Length == 0) { notes.Add("لا تعيين: الوظيفة فارغة / no assignment: position empty"); }
            if (form.QualificationColumn != null && qualification.Length == 0) { notes.Add("لا مؤهل / no qualification"); }
            if (form.MaritalStatusColumn != null && marital == null && Value(form.MaritalStatusColumn).Length > 0) { notes.Add("الحالة الاجتماعية غير مقروءة / unreadable marital status"); }

            if (mobile.Length > 20) { notes.Add("رقم الجوال أطول من الحقل ولن يُحفظ / mobile too long, not stored"); }
            if (spouseIdNo != null && form.SpouseIdTypeLookupId == null) { notes.Add("هوية الزوج/الزوجة بلا نوع وثيقة / spouse ID has no document type"); }

            return new EmployeeImportViewModel.PreviewRow(
                number, first, father, grandfather, family,
                birth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), gender,
                idNumber.Length == 0 ? null : idNumber,
                mobile.Length == 0 || mobile.Length > 20 ? null : mobile, active,
                marital?.ToString(), bank.Length == 0 ? null : bank, account.Length == 0 ? null : account,
                hire?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), contractType?.ToString(), salary,
                position.Length == 0 ? null : position,
                qualification.Length == 0 ? null : qualification,
                university.Length == 0 ? null : university,
                graduation?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                whatsApp, address, originTown, spouseIdNo, palPay, jawwalPay,
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
