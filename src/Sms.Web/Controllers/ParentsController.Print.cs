using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// The parent file out of the screen — on paper and in a file (doc/Modules/11 §8.2, §10
    /// "Family register").
    /// <para>
    /// Both readings are built from <c>BuildFileAsync</c>, the same method the screen itself draws
    /// from, so the sheet a registrar hands a family and the row a ministry return quotes cannot
    /// come to disagree with what the clerk was just looking at. A second query written to be
    /// "just for the print" is how two numbers for one family's balance get into a school.
    /// </para>
    /// <para>
    /// Each carries its own right rather than riding on <c>PAR/File/View</c> (doc/Modules/11 §6):
    /// reading one family's record on screen and walking out with every child, every mobile number
    /// and what the family owes are different disclosures, and the school decides them separately.
    /// </para>
    /// </summary>
    public partial class ParentsController
    {
        /// <summary>
        /// The file as a sheet to sign, file or hand to the family.
        /// <para>
        /// <b>Deviation, stated:</b> doc/Modules/11 §10 asks for the family register as a document;
        /// there is no PDF engine in this build (a pending owner decision, docs/Status), so this is
        /// the browser's own print of an HTML sheet, as every other printable document here is. The
        /// layout hides the application chrome and keeps the school's name and the moment the copy
        /// was taken on the page.
        /// </para>
        /// </summary>
        [HttpGet("{id:int}/print")]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.Print)]
        public async Task<IActionResult> Print(int id)
        {
            var file = await BuildFileAsync(id, null);
            if (file == null)
            {
                return NotFound();
            }

            var school = await _db.Schools.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == _db.CurrentSchoolId)
                .Select(s => new { s.NameAr, s.NameEn })
                .SingleOrDefaultAsync(HttpContext.RequestAborted);

            return View(new ParentFileSheetViewModel
            {
                File = file,
                SchoolName = school == null ? string.Empty : (IsArabic ? school.NameAr : school.NameEn),
                PrintedAtUtc = _clock.UtcNow,
            });
        }

        /// <summary>
        /// The same family as a file: one row per child, carrying the parent's identity down the
        /// left and the child's position on the family statement along the right. The shaping is
        /// <see cref="ParentFileExport"/>'s and the quoting and byte-order mark are
        /// <see cref="CsvFile"/>'s, both pinned by tests there.
        /// </summary>
        [HttpGet("{id:int}/export.csv")]
        [RequirePermission(ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, ActionVerb.Export)]
        public async Task<IActionResult> ExportCsv(int id)
        {
            var file = await BuildFileAsync(id, null);
            if (file == null)
            {
                return NotFound();
            }

            return File(
                CsvFile.Bytes(ParentFileExport.Records(file, IsArabic)),
                "text/csv",
                ParentFileExport.FileName(file.Parent.ParentFileNo, _clock.UtcNow));
        }
    }
}
