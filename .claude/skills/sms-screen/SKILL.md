---
name: sms-screen
description: Build or extend a module screen in the SMS web app — controller action, Razor view, view model, screen permission, sidebar/launcher wiring, bilingual labels. Use whenever the task is "add a screen", "the module has no UI", "build the screens for module NN", or when a controller action or .cshtml needs to be created or changed. Covers the 14 modules whose engines exist but whose screens do not.
user-invocable: true
---

# Building a screen

Most engines in this product are finished and most screens are not. A screen here is a thin
layer over an existing `I*Admin` port — if you find yourself writing business rules in a
controller, the rule belongs in `Sms.Application` instead.

## Before writing anything

1. **Read the module doc** — `docs/Modules/NN-*.md`, section §8 "Required screens". It lists the
   screens by number and names the `BR-` rules each one enforces. Build the subset you were asked
   for; note in the controller's XML summary which numbered screens you deliberately left out and
   why (this project treats silent omissions as findings).
2. **Read the `P-*` pattern** the doc names, in `docs/UI/02-Screen-Patterns.md` — P-LIST, P-DETAIL,
   P-WIZARD, P-SHEET, P-POS, P-CAL, P-BOARD, P-INBOX, P-CONFIG, P-STMT, P-LAUNCH. It fixes the
   anatomy, the keyboard behaviour, and the empty/error philosophy.
3. **Read the port** — `src/Sms.Application/<Module>/I<Module>Admin.cs`. Its XML docs name the
   exceptions each method throws; those are the messages you must translate.
4. **Copy the closest existing screen.** `GradingController` + `Views/Grading/` is the reference
   for a workspace with sub-navigation; `SchoolController` for a tabbed profile;
   `SetupController` for a wizard. Match their shape rather than inventing one.

## The seven things every screen needs

### 1. A `ScreenCatalog` entry, first

`src/Sms.Application/Security/ScreenCatalog.cs`. Add the screen constant under its module and
list the verbs it answers to. Verb meanings are fixed in that file's header comment — read it;
there is no `Delete` verb, `Deactivate` covers delete/void/cancel/end, `Post` means money moves,
`Configure` changes the system's own shape.

Nothing else can be wired until the constant exists, and `PermissionSeedContributor` picks it up
from there.

### 2. A controller action with a declared permission

```csharp
[HttpGet("")]
[RequirePermission(ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets, ActionVerb.View)]
public async Task<IActionResult> Index(int? year = null) { … }
```

Every action needs `[RequirePermission]` or `[NoPermissionRequired("why")]`.
`ScreenPermissionTests` fails the build otherwise — that is the point of it. POSTs additionally
carry `[ValidateAntiForgeryToken]`.

The controller is `[Route("<module-path>")]` at class level, actions use `[HttpGet("sub/path")]`.
Inject the `I*Admin` port, `AppDbContext` (reads only), and whichever of `IAuditContext`,
`ITenantContext`, `IWorkingYearContext`, `IClock` the screen actually needs.

### 3. Bilingual text, everywhere

```csharp
private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
private static string T(string en, string ar) => IsArabic ? ar : en;
```

Views declare the same helper locally in their `@{ }` block. Enum values render through
`Labels.cs` or the module's own `*Labels` class — never `ToString()`.

**Translate every refusal at this boundary.** The engine's exception message is English; catch it
and show a translated one, or pre-check with a translated message before calling. An Arabic user
must never see raw English error text.

### 4. Reads that survive a deactivated master row

```csharp
// The picker: what the user may choose now.
var subjects = await _db.Subjects.AsNoTracking().Where(...).ToListAsync();
// The lookup: what existing rows point at, live or retired.
var allSubjects = await _db.Subjects.IgnoreQueryFilters().AsNoTracking()
    .Where(s => s.SchoolId == _db.CurrentSchoolId).ToListAsync();
```

If you load `GradeLevels`, `Stages`, `Subjects` or `FeeCategories` and then look a row up by id
with `First(...)`, use `IgnoreQueryFilters()` on that list. `SoftActiveLookupTests` enforces it
because the same mistake has taken three screens down; the failure needs a deactivated row, which
nobody has in development.

Money: no `SumAsync()` on a decimal column — materialize, then `.Sum()` in memory.

### 5. A view model in `Sms.Web/Models`

One `*ViewModels.cs` per module area, records or simple classes, nested `Row`/`Option` types for
grid rows and picker entries. Do not put entities directly on a view where a projection is
clearer. Note: `Labels.cs` and `PeopleViewModels.cs` are shared — put a new module's label helper
in its own file.

### 6. The Razor view

`Views/<Controller>/<Action>.cshtml`, following the existing shape:

```cshtml
@{
    var isRtl = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
    string T(string en, string ar) => isRtl ? ar : en;
    ViewData["Title"] = T("…", "…");
    ViewData["Breadcrumb"] = T("…", "…");
    var help = new HelpPanelViewModel { … };   // see Views/Grading/Index.cshtml
}
<div class="sms-page-head">…</div>
<partial name="_<Module>Nav" />
```

- `sms-page-head`, `sms-card`, `sms-table` are the house classes (`wwwroot/css/site.css`).
  Bootstrap 5 with the RTL stylesheet selected by `_Layout`.
- Every module with more than one screen gets a `Views/Shared/_<Module>Nav.cshtml` partial for its
  sub-tabs.
- Add a `_HelpModal` panel — every existing screen has one, bilingual, explaining the rules the
  screen enforces.
- Tables need a `<caption class="visually-hidden">` and `scope="col"` headers.
- Empty states are instructive sentences, not blank space.
- A Razor local function containing tag helpers must be `async Task` and be called as
  `@{ await Fn(…); }`. Write `v@(version)`, never `v@version`.

### 7. Navigation

- `Sms.Web/Navigation/ModuleCatalog.cs` — set the module's `ScreenController`/`ScreenAction` so
  its sidebar entry stops routing to the "not built yet" landing page.
- `Sms.Web/Navigation/WorkspaceCatalog.cs` — add the launcher tile if the screen belongs to a
  department (P-LAUNCH). It must name the *same* `(module, screen)` constants as the
  `[RequirePermission]` attribute; `WorkspaceCatalogTests` checks that by reflection so a rename
  is a compile error rather than a dead tile.

## Writes

```csharp
try
{
    _audit.Reason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason;
    await _admin.DoTheThingAsync(...);
    TempData["Flash"] = T("Saved.", "تم الحفظ.");
    return RedirectToAction(nameof(Index), new { … });
}
catch (InvalidOperationException ex)
{
    ModelState.AddModelError(string.Empty, ex.Message);
    return View(await BuildAsync(form));
}
```

Set `IAuditContext.Reason` **before** the call for any screen editing a T1-audited field, and give
the form a reason input. Never bypass the `I*Admin` port and save through `AppDbContext` from a
controller. Never trust a posted tenant/school id — bind to `_tenant.SchoolId`.

## Verify before reporting done

```bash
dotnet build src/Sms.Web
dotnet test tests/Sms.ArchitectureTests tests/Sms.Web.Tests
```

Then load the screen for real in both languages — see the `sms-smoke` skill. Views compile at
build, so a `.cshtml` edit needs a rebuild and a restart to appear. A screen that has never been
rendered is not finished: missing views for committed actions have shipped here before and
returned 500s.
