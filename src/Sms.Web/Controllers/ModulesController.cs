using Microsoft.AspNetCore.Mvc;
using Sms.Web.Navigation;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Landing page per module until its own screens exist: shows what the
    /// engine covers (epic, doc reference) so a sidebar entry is never a dead
    /// link. Real module controllers replace these routes as screens land.
    /// </summary>
    [Route("m")]
    public class ModulesController : Controller
    {
        [HttpGet("{code}")]
        public IActionResult Index(string code)
        {
            var module = ModuleCatalog.Find(code);
            if (module == null)
            {
                return NotFound();
            }

            if (module.ScreenController != null)
            {
                return RedirectToAction(module.ScreenAction, module.ScreenController);
            }

            return View(module);
        }
    }
}
