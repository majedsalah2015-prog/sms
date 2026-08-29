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
using Sms.Application.Lookups;
using Sms.Application.Security;
using Sms.Application.Students;
using Sms.Domain.Common;
using Sms.Domain.Parents;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;
using Sms.Web.Services;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Bringing a school's previous Access register across (owner request, 2026-08-22, extended
    /// 2026-08-24).
    /// <para>
    /// The student's own seven fields — the quad name, date of birth, gender, nationality and the
    /// identity number — plus the two guardians the register describes beside each child: for the
    /// father and the mother alike, an ID number, a name, an occupation, a mobile and an
    /// educational qualification. Everything else in an old register is somebody's abandoned
    /// column, and importing it would put data into this system that nothing here knows how to
    /// keep true.
    /// </para>
    /// <para>
    /// A guardian becomes a <c>Parent</c> row linked by <c>StudentGuardianLink</c>, not a set of
    /// columns on the student (doc/Modules/11 §7, BR-PAR-001: never duplicated per child). An old
    /// register holds one line per child, so a family of four arrives as the same father typed four
    /// times; the ID number is what folds those four back into one man, which is BR-PAR-002's
    /// strongest match and the reason that column matters more than the rest.
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

        /// <summary>
        /// ppl.Parent's own column widths. A register cell wider than the column is clamped rather
        /// than allowed to fail the row: a 40-character "mobile" is already not a phone number, and
        /// losing a whole child over it would be the wrong trade.
        /// </summary>
        private const int NameLimit = 100;

        private const int MobileLimit = 20;

        private const int IdNoLimit = 30;

        /// <summary>ppl.Student.PlaceOfBirth's own width.</summary>
        private const int PlaceOfBirthLimit = 100;

        /// <summary>
        /// One gigabyte. Not a guess at what is reasonable — it is what an Access file grows to
        /// after twenty years of a school never compacting it, and the cost of allowing it is
        /// nothing: the upload is streamed to a temporary file, never held in memory.
        /// </summary>
        internal const long AccessUploadLimit = 1_073_741_824L;

        /// <summary>
        /// An identity number this short, or made only of zeros, is the register's way of saying it
        /// does not have one. It must not be stored and above all must not be matched on: one real
        /// school register carries <c>0</c> as the mother's ID in 1,310 of 1,398 rows, and treating
        /// that as a number would fold thirteen hundred women into a single parent file.
        /// </summary>
        private static bool IsRealIdNumber(string? value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length >= 4 && text.Any(c => char.IsLetterOrDigit(c) && c != '0');
        }

        /// <summary>What the register's own code tables and this school's catalogue say, carried together through one import.</summary>
        private sealed record CodeMaps(
            IReadOnlyDictionary<string, string> Occupations,
            IReadOnlyDictionary<string, string> Educations,
            IReadOnlyCollection<(int Id, string Ar, string En)> EducationLevels);

        [HttpGet("import")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Create)]
        public async Task<IActionResult> Import()
        {
            return View(await BuildImportAsync(new StudentImportViewModel()));
        }

        /// <summary>
        /// Step one: take the file, keep a copy, and report what tables are in it.
        /// <para>
        /// The size limits are raised for this one action and nowhere else. Kestrel refuses a
        /// request body over 30 MB by default, which is smaller than any real school register — the
        /// one this was built against is 76 MB for 1,398 children — and the refusal happens in the
        /// server before any of this code runs: the connection simply closes, the browser shows a
        /// network error, and nothing anywhere says why. Every other endpoint keeps the default,
        /// because everything else that is posted here is a form.
        /// </para>
        /// </summary>
        [HttpPost("import/upload")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(AccessUploadLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = AccessUploadLimit)]
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
                LoadTables(m, path);
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
                LoadTables(form, path);
                if (string.IsNullOrWhiteSpace(form.Table))
                {
                    // Pressing "Read the table" on the empty choice returned the page unchanged and
                    // said nothing, which reads as a dead button rather than as a question not yet
                    // answered.
                    TempData["Error"] = T("Choose the table to read first.", "اختر أولاً الجدول المراد قراءته.");
                }
                else
                {
                    form.Columns = AccessRegisterReader.ListColumns(path, form.Table!);

                    // Guess only what has not been answered. Re-guessing unconditionally meant a
                    // field the operator had just set back to "— none —" came straight back mapped,
                    // so a wrong guess could be re-pointed but never cleared; and a mapping made
                    // against the previous table survived the switch naming columns the new one does
                    // not have, which shows as a mapping the screen cannot display and a preview in
                    // which every row is "name incomplete".
                    if (!KeepMappingsThatStillExist(form) || !form.MappingChosen)
                    {
                        GuessMapping(form);
                    }

                    var maps = LoadCodeMaps(path, form, await LookupAsync("EducationLevel"));
                    var rows = AccessRegisterReader.ReadRows(path, form.Table!);
                    form.TotalRows = rows.Count;

                    // An empty table is an answer, and until now it was the one answer this screen
                    // could not give: no rows meant no preview, and no preview meant the whole third
                    // step vanished, so the operator saw the mapping they had just filled in come
                    // back unchanged and concluded the button was broken. It happens for a real
                    // reason — one register of this shape carried an empty `student_table` beside a
                    // `del_st` holding 103 withdrawn children — so the fix is to say which table was
                    // read and that it was empty, not to guess at another one.
                    if (rows.Count == 0)
                    {
                        TempData["Error"] = T(
                            $"The table \"{form.Table}\" has no rows, so there is nothing to preview. Choose the table that actually holds the children — an old register often keeps the current students in one table and the withdrawn ones in another.",
                            $"الجدول «{form.Table}» لا يحتوي على أي صف، فلا شيء يُعايَن. اختر الجدول الذي يحمل الطلاب فعلاً — فالسجل القديم كثيراً ما يحفظ الطلاب الحاليين في جدول والمنسحبين في جدول آخر.");
                    }

                    var preview = new List<StudentImportViewModel.PreviewRow>();
                    var ready = 0;

                    // Counted across every row, not only the fifteen shown: the operator is deciding
                    // whether to run this over a thousand children, and "how many guardians will this
                    // actually create" is not a question the first page can answer.
                    var guardianIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var anonymousGuardians = 0;
                    var unmatched = 0;
                    var unmatchedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    for (var i = 0; i < rows.Count; i++)
                    {
                        var candidate = Interpret(rows[i], form, maps, i + 1);
                        if (candidate.Problem == null)
                        {
                            ready++;
                            foreach (var guardian in new[] { candidate.Father, candidate.Mother })
                            {
                                if (guardian == null) { continue; }
                                if (string.IsNullOrWhiteSpace(guardian.IdNumber)) { anonymousGuardians++; }
                                else { guardianIds.Add(guardian.IdNumber!); }
                                if (guardian.EducationUnmatched && guardian.EducationText is string text)
                                {
                                    unmatched++;
                                    unmatchedNames[text] = unmatchedNames.TryGetValue(text, out var seen) ? seen + 1 : 1;
                                }
                            }
                        }

                        if (preview.Count < 15) { preview.Add(candidate); }
                    }

                    form.ReadyRows = ready;
                    form.SkippedRows = rows.Count - ready;
                    form.GuardianCount = guardianIds.Count + anonymousGuardians;
                    form.GuardiansWithoutId = anonymousGuardians;
                    form.UnmatchedEducations = unmatched;
                    form.UnmatchedEducationNames = unmatchedNames
                        .OrderByDescending(p => p.Value).Take(12).Select(p => (p.Key, p.Value)).ToList();
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

                var (fatherRelationshipId, motherRelationshipId) = await GuardianRelationshipsAsync();
                if (form.MapsGuardians && (fatherRelationshipId == null || motherRelationshipId == null))
                {
                    // Linking a guardian under the wrong relationship is worse than not linking one:
                    // custody, pickup and the portal all read it. Refuse, and name the fix.
                    throw new InvalidOperationException(T(
                        "The relationship list has no \"Father\" and \"Mother\" — add them under Setup › Lookups before importing guardians.",
                        "لا تحتوي قائمة صلة القرابة على «الأب» و«الأم» — أضِفهما من الإعدادات › القوائم قبل استيراد أولياء الأمور."));
                }

                var path = PathOf(form.Token);
                var maps = LoadCodeMaps(path, form, await LookupAsync("EducationLevel"));
                var rows = AccessRegisterReader.ReadRows(path, form.Table ?? throw new InvalidOperationException(T("Choose a table.", "اختر جدولاً.")));

                var imported = 0;
                var skipped = 0;
                var guardiansCreated = 0;
                var guardiansReused = 0;
                var guardiansFailed = 0;
                var failed = new List<string>();

                // Guardians seen in this run, by ID number. Two siblings a hundred rows apart must
                // reach the same father without a query each time, and without a second file number.
                var byIdNumber = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                for (var i = 0; i < rows.Count; i++)
                {
                    var candidate = Interpret(rows[i], form, maps, i + 1);
                    if (candidate.Problem != null) { skipped++; continue; }

                    try
                    {
                        // Names go in twice: the Arabic register is the source, and the English half
                        // is required by the model. Transliterating here would invent four spellings
                        // per student that nobody chose — the same text is stored and left to be
                        // corrected by whoever knows how the family spells it.
                        var student = await _students.RegisterStudentAsync(
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

                        // A second save, and only when the register actually said something. These
                        // are not identity fields — none of them is [RequiresAuditReason] — so this
                        // needs no ambient reason, unlike the identity half above. It is deliberately
                        // outside the guardian try/catch below: a place of birth that would not save
                        // must not be reported as a guardian failure.
                        if (!candidate.Social.IsEmpty)
                        {
                            await _students.UpdateSocialProfileAsync(
                                student.Id, null, null, null, null,
                                candidate.Social.PlaceOfBirth, null, candidate.Social.BirthOrder,
                                candidate.Social.SiblingCount, candidate.Social.Mobile,
                                HttpContext.RequestAborted);
                        }

                        try
                        {
                            // The first guardian on the row carries the flags that must belong to
                            // exactly one person — primary contact and financial responsibility
                            // (BR-GLB-004) — and the father is first when the register describes one.
                            // Both may collect the child and both see the portal.
                            var first = true;
                            foreach (var (guardian, relationshipId) in new[]
                            {
                                (candidate.Father, fatherRelationshipId),
                                (candidate.Mother, motherRelationshipId),
                            })
                            {
                                if (guardian == null || relationshipId == null) { continue; }

                                var (parentId, created) = await FindOrCreateGuardianAsync(guardian, form, byIdNumber);
                                if (created) { guardiansCreated++; } else { guardiansReused++; }

                                await _students.LinkGuardianAsync(
                                    studentId: student.Id, parentId: parentId, relationshipLookupId: relationshipId.Value,
                                    isPrimaryContact: first, isFinanciallyResponsible: first,
                                    isPickupAuthorized: true, isPortalVisible: true,
                                    effectiveFromUtc: _clock.UtcNow, guardianshipDocAttachmentId: null,
                                    cancellationToken: HttpContext.RequestAborted);
                                first = false;
                            }
                        }
                        catch (Exception ex)
                        {
                            // The child is registered and stays registered. A guardian that would not
                            // save is a gap in one file to be filled in by hand, not a reason to
                            // report the student as skipped when they are visibly in the register.
                            guardiansFailed++;
                            if (failed.Count < 5) { failed.Add($"#{candidate.Number} {candidate.FirstName} {candidate.FamilyName} — {UserMessage.For(ex, IsArabic)}"); }
                        }
                    }
                    catch (Exception ex)
                    {
                        // One bad row does not stop the register: it is counted, named, and the rest
                        // go in. A half-finished import that reported success would be worse.
                        skipped++;
                        if (failed.Count < 5) { failed.Add($"#{candidate.Number} {candidate.FirstName} {candidate.FamilyName} — {UserMessage.For(ex, IsArabic)}"); }
                    }

                    // Four saves a row now, not one. Without this the tracker keeps every student,
                    // parent and link of the run and DetectChanges re-walks the lot on each save —
                    // the same quadratic that took a 1,020-student rollover past ten minutes.
                    _db.ChangeTracker.Clear();
                }

                TryDelete(path);
                TempData["Flash"] = form.MapsGuardians
                    ? T($"{imported} student(s) imported, {skipped} skipped. Guardians: {guardiansCreated} created, {guardiansReused} matched to an existing file{(guardiansFailed > 0 ? $", {guardiansFailed} could not be saved" : string.Empty)}.",
                        $"استُورد {imported} طالباً، وتُخطّي {skipped}. أولياء الأمور: {guardiansCreated} جديد، و{guardiansReused} طوبق مع ملف قائم{(guardiansFailed > 0 ? $"، و{guardiansFailed} تعذّر حفظه" : string.Empty)}.")
                    : T($"{imported} student(s) imported, {skipped} skipped.", $"استُورد {imported} طالباً، وتُخطّي {skipped}.");
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

        // ------------------------------------------------------------------ guardians

        /// <summary>
        /// The parent file this guardian belongs to: an existing one when the ID number already
        /// names somebody here, a new one otherwise. Returns whether it had to be created, which is
        /// the only honest way to report "1,020 rows produced 613 guardians".
        /// <para>
        /// An existing file is linked to and <em>not</em> overwritten. The old register is the
        /// weaker source — it is the one being retired — and a run that quietly rewrote six hundred
        /// parents' mobiles from it would be a data loss nobody asked for and nobody would notice.
        /// </para>
        /// </summary>
        private async Task<(int ParentId, bool Created)> FindOrCreateGuardianAsync(
            StudentImportViewModel.GuardianCandidate guardian, StudentImportViewModel form, Dictionary<string, int> byIdNumber)
        {
            var idNumber = Clamp(guardian.IdNumber, IdNoLimit);
            if (idNumber != null)
            {
                if (byIdNumber.TryGetValue(idNumber, out var known))
                {
                    return (known, false);
                }

                // IgnoreQueryFilters for the lookup: a deactivated parent file still owns its ID
                // number, and finding nothing would mint a second file for the same person.
                var existing = await _db.Parents.IgnoreQueryFilters().AsNoTracking()
                    .Where(p => p.SchoolId == _db.CurrentSchoolId && p.PrimaryIdNo == idNumber)
                    .Select(p => p.Id)
                    .FirstOrDefaultAsync(HttpContext.RequestAborted);
                if (existing != 0)
                {
                    byIdNumber[idNumber] = existing;
                    return (existing, false);
                }
            }

            var name = Clamp(guardian.Name, NameLimit) ?? guardian.Name;
            var parent = await _parents.RegisterParentAsync(
                name, name,
                Clamp(guardian.Mobile, MobileLimit) ?? string.Empty,
                null, null, guardian.Occupation, "ar",
                idNumber == null ? null : form.IdTypeLookupId, idNumber,
                ParentLifeStatus.Alive, null, guardian.EducationLookupId,
                HttpContext.RequestAborted);

            if (idNumber != null) { byIdNumber[idNumber] = parent.Id; }
            return (parent.Id, true);
        }

        /// <summary>
        /// The "Father" and "Mother" entries of the RelationshipType catalogue, by code first and by
        /// name second — a school may have renamed the value it was seeded with, but the code is
        /// never re-purposed (BR-SET-001).
        /// </summary>
        private async Task<(int? Father, int? Mother)> GuardianRelationshipsAsync()
        {
            var category = await _db.LookupCategories.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Code == "RelationshipType", HttpContext.RequestAborted);
            if (category == null)
            {
                return (null, null);
            }

            var values = await _db.LookupValues.AsNoTracking()
                .Where(v => v.LookupCategoryId == category.Id)
                .Select(v => new { v.Id, v.Code, v.Name.NameAr, v.Name.NameEn })
                .ToListAsync(HttpContext.RequestAborted);

            int? Find(string code, string ar, string en)
            {
                var byCode = values.FirstOrDefault(v => string.Equals(v.Code, code, StringComparison.OrdinalIgnoreCase));
                if (byCode != null) { return byCode.Id; }

                var named = LookupTextMatcher.Match(ar, values.Select(v => (v.Id, v.NameAr, v.NameEn)).ToList());
                return named ?? LookupTextMatcher.Match(en, values.Select(v => (v.Id, v.NameAr, v.NameEn)).ToList());
            }

            return (Find("Father", "الأب", "Father"), Find("Mother", "الأم", "Mother"));
        }

        // ------------------------------------------------------------------ helpers

        private async Task<StudentImportViewModel> BuildImportAsync(StudentImportViewModel m)
        {
            m.Nationalities = await LookupAsync("Nationality");
            m.IdTypes = await LookupAsync("IdType");
            m.EducationLevels = await LookupAsync("EducationLevel");

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

        /// <summary>
        /// Reads whichever of the register's little code tables the mapping names, once per request
        /// rather than once per row: they are eight or a hundred rows and the alternative is a file
        /// read per cell across fourteen hundred children.
        /// </summary>
        private static CodeMaps LoadCodeMaps(
            string path, StudentImportViewModel form, IReadOnlyCollection<(int Id, string Ar, string En)> educationLevels)
        {
            static IReadOnlyDictionary<string, string> Read(string path, string? table) =>
                string.IsNullOrWhiteSpace(table)
                    ? new Dictionary<string, string>()
                    : AccessRegisterReader.ReadCodeMap(path, table!);

            return new CodeMaps(Read(path, form.OccupationCodeTable), Read(path, form.EducationCodeTable), educationLevels);
        }

        /// <summary>
        /// Fills the table pickers: the names, and the row count to show beside each one. A register
        /// of this kind holds 160 tables of which four matter, and a name alone cannot tell an
        /// operator that <c>student_table</c> is the empty one — so the count is read here rather
        /// than left to be discovered by choosing wrong. It costs about a second for the whole file,
        /// because <c>COUNT(*)</c> transfers nothing.
        /// </summary>
        private static void LoadTables(StudentImportViewModel m, string path)
        {
            var sizes = AccessRegisterReader.ListTableSizes(path);
            m.Tables = sizes.Select(s => s.Name).ToList();

            // Indexer rather than ToDictionary: Access will not hold two tables whose names differ
            // only by case, but this reads whatever file was handed to it, and a duplicate key must
            // not be a 500 on somebody's register.
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, rows) in sizes)
            {
                if (rows is int count) { counts[name] = count; }
            }

            m.TableRowCounts = counts;
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

        /// <summary>Trimmed to the column's width, or null when there was nothing there.</summary>
        private static string? Clamp(string? value, int limit)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0) { return null; }
            return text.Length <= limit ? text : text.Substring(0, limit);
        }

        /// <summary>
        /// Clears every mapping naming a column the chosen table does not have, and says whether any
        /// mapping survived. A mapping is only meaningful against the table it was made for, and the
        /// table can be changed after the mapping is filled in — a stale name then binds to nothing,
        /// which is invisible on screen (the picker simply shows "— none —" while the model still
        /// holds the old column) and shows up only as a preview where every row is "name incomplete".
        /// <para>
        /// Found by reflection over the view model's own <c>*Column</c> properties rather than listed
        /// here: the list would be a second copy of the mapping, and the copy that gets forgotten
        /// when a twenty-third column is added is this one — silently, because a field missing from
        /// it simply never clears. The two <c>*CodeTable</c> properties are deliberately not matched:
        /// they name tables, and a table is not invalidated by a change of table.
        /// </para>
        /// </summary>
        private static bool KeepMappingsThatStillExist(StudentImportViewModel form)
        {
            var columns = new HashSet<string>(form.Columns, StringComparer.OrdinalIgnoreCase);
            var mapped = false;

            foreach (var property in typeof(StudentImportViewModel).GetProperties())
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

        /// <summary>
        /// Fills any mapping the operator has not chosen. The guessing itself is
        /// <see cref="RegisterMappingGuesser"/> — a pure engine, so it can be tested against a real
        /// register's sixty-seven column names rather than only against a running screen. This is
        /// the translation between it and the form.
        /// </summary>
        private static void GuessMapping(StudentImportViewModel form)
        {
            var guessed = RegisterMappingGuesser.Guess(form.Columns, form.Tables, new RegisterMappingGuesser.RegisterMapping
            {
                FirstName = form.FirstNameColumn,
                FatherName = form.FatherNameColumn,
                GrandfatherName = form.GrandfatherNameColumn,
                FamilyName = form.FamilyNameColumn,
                FullName = form.FullNameColumn,
                DateOfBirth = form.DateOfBirthColumn,
                Gender = form.GenderColumn,
                IdNumber = form.IdNumberColumn,
                PlaceOfBirth = form.PlaceOfBirthColumn,
                SiblingCount = form.SiblingCountColumn,
                BirthOrder = form.BirthOrderColumn,
                Mobile = form.MobileColumn,
                FatherFullName = form.FatherFullNameColumn,
                FatherIdNumber = form.FatherIdNumberColumn,
                FatherOccupation = form.FatherOccupationColumn,
                FatherMobile = form.FatherMobileColumn,
                FatherEducation = form.FatherEducationColumn,
                MotherFullName = form.MotherFullNameColumn,
                MotherIdNumber = form.MotherIdNumberColumn,
                MotherOccupation = form.MotherOccupationColumn,
                MotherMobile = form.MotherMobileColumn,
                MotherEducation = form.MotherEducationColumn,
                OccupationCodeTable = form.OccupationCodeTable,
                EducationCodeTable = form.EducationCodeTable,
            });

            form.FirstNameColumn = guessed.FirstName;
            form.FatherNameColumn = guessed.FatherName;
            form.GrandfatherNameColumn = guessed.GrandfatherName;
            form.FamilyNameColumn = guessed.FamilyName;
            form.FullNameColumn = guessed.FullName;
            form.DateOfBirthColumn = guessed.DateOfBirth;
            form.GenderColumn = guessed.Gender;
            form.IdNumberColumn = guessed.IdNumber;
            form.PlaceOfBirthColumn = guessed.PlaceOfBirth;
            form.SiblingCountColumn = guessed.SiblingCount;
            form.BirthOrderColumn = guessed.BirthOrder;
            form.MobileColumn = guessed.Mobile;
            form.FatherFullNameColumn = guessed.FatherFullName;
            form.FatherIdNumberColumn = guessed.FatherIdNumber;
            form.FatherOccupationColumn = guessed.FatherOccupation;
            form.FatherMobileColumn = guessed.FatherMobile;
            form.FatherEducationColumn = guessed.FatherEducation;
            form.MotherFullNameColumn = guessed.MotherFullName;
            form.MotherIdNumberColumn = guessed.MotherIdNumber;
            form.MotherOccupationColumn = guessed.MotherOccupation;
            form.MotherMobileColumn = guessed.MotherMobile;
            form.MotherEducationColumn = guessed.MotherEducation;
            form.OccupationCodeTable = guessed.OccupationCodeTable;
            form.EducationCodeTable = guessed.EducationCodeTable;
        }

        /// <summary>
        /// Turns one Access row into the student it would become, or into the reason it will not.
        /// Nothing is written here — this is the same function the preview and the commit both use,
        /// so what the operator saw is exactly what runs.
        /// </summary>
        private static StudentImportViewModel.PreviewRow Interpret(
            IReadOnlyDictionary<string, string?> row, StudentImportViewModel form, CodeMaps maps, int number)
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

            // The father's name is in the student's own: in this convention the child is
            // <given> <father> <grandfather> <family>, so the man is <father> <grandfather> <family>.
            // Used only when the register has no column of its own for it, and only when the operator
            // mapped some father field — deriving a guardian nobody asked for would be an invention.
            var derivedFatherName = string.Join(" ", new[] { father, grandfather, family }.Where(p => p.Length > 0));

            var fatherCandidate = Guardian(
                Value, maps, derivedFatherName,
                form.FatherFullNameColumn, form.FatherIdNumberColumn, form.FatherOccupationColumn,
                form.FatherMobileColumn, form.FatherEducationColumn);

            var motherCandidate = Guardian(
                Value, maps, null,
                form.MotherFullNameColumn, form.MotherIdNumberColumn, form.MotherOccupationColumn,
                form.MotherMobileColumn, form.MotherEducationColumn);

            // The student's own particulars beyond identity. None of them can refuse a row: a
            // register with an unreadable sibling count is still a register full of children, and a
            // child kept out of the system over the number of brothers they have would be absurd.
            var social = new StudentImportViewModel.SocialCandidate(
                Clamp(Value(form.PlaceOfBirthColumn), PlaceOfBirthLimit),
                ParseCount(Value(form.SiblingCountColumn), zeroMeans: 0),
                ParseCount(Value(form.BirthOrderColumn), zeroMeans: null),
                Clamp(Value(form.MobileColumn), MobileLimit));

            return new StudentImportViewModel.PreviewRow(
                number, first, father, grandfather, family,
                birth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), gender, idNumber, problem,
                fatherCandidate, motherCandidate, social);
        }

        /// <summary>
        /// One guardian as the mapped columns describe them, or null when this row says nothing about
        /// them. Silence is the common case — half a register's mothers have no cell filled in — and
        /// it must produce no parent file at all rather than an empty one.
        /// </summary>
        private static StudentImportViewModel.GuardianCandidate? Guardian(
            Func<string?, string> value, CodeMaps maps, string? fallbackName,
            string? nameColumn, string? idColumn, string? occupationColumn, string? mobileColumn, string? educationColumn)
        {
            // A code table turns 12 into مهندس. A code the table does not list — 0, which is how a
            // register says "not recorded" — resolves to nothing rather than to the digit it happens
            // to be. But a cell that was never a number was never a code either: the operator named
            // the wrong table, or this row was typed by hand, and its words are kept.
            static string Decode(string cell, IReadOnlyDictionary<string, string> map)
            {
                if (cell.Length == 0 || map.Count == 0) { return cell; }
                if (map.TryGetValue(cell, out var name)) { return name; }
                return cell.All(c => char.IsDigit(c) || c is '.' or '-' or '+') ? string.Empty : cell;
            }

            var name = value(nameColumn);
            var rawId = value(idColumn);
            var idNumber = IsRealIdNumber(rawId) ? rawId : string.Empty;
            var occupation = Decode(value(occupationColumn), maps.Occupations);
            var mobile = value(mobileColumn);
            var education = Decode(value(educationColumn), maps.Educations);

            var anythingSaid = name.Length > 0 || idNumber.Length > 0 || occupation.Length > 0
                || mobile.Length > 0 || education.Length > 0;
            if (!anythingSaid)
            {
                return null;
            }

            if (name.Length == 0) { name = (fallbackName ?? string.Empty).Trim(); }
            if (name.Length == 0)
            {
                // Fields with nobody to attach them to. Recorded as no guardian rather than as a
                // nameless parent file, which is the one thing a register of people cannot hold.
                return null;
            }

            var matched = education.Length == 0 ? null : LookupTextMatcher.Match(education, maps.EducationLevels);
            return new StudentImportViewModel.GuardianCandidate(
                name,
                idNumber.Length == 0 ? null : idNumber,
                occupation.Length == 0 ? null : occupation,
                mobile.Length == 0 ? null : mobile,
                matched,
                education.Length == 0 ? null : education,
                education.Length > 0 && matched == null);
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

        /// <summary>
        /// A small whole number out of a register cell, or null when the cell does not hold one.
        /// <para>
        /// <paramref name="zeroMeans"/> is the whole point. Both columns this reads spell "not
        /// recorded" as 0, but only one of them can also mean it: a child with no brothers or sisters
        /// genuinely has 0 siblings, so 0 is stored, while "0th eldest" is not a position anybody
        /// holds and the 221 rows of the real register that say it are saying nothing. Reading both
        /// the same way would either lose every only child or invent a birth order for a sixth of
        /// the school.
        /// </para>
        /// <para>
        /// Bounded at 40 because past that the cell is a phone number, a year, or a code from a
        /// column nobody meant to map — and a student with 900 siblings on their file is worse than
        /// one with none recorded.
        /// </para>
        /// </summary>
        private static int? ParseCount(string value, int? zeroMeans)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0 || !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return null;
            }

            if (number == 0) { return zeroMeans; }
            return number > 0 && number <= 40 ? number : null;
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
