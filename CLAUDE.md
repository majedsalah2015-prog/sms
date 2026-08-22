# SMS — bilingual School Management System

Commercial ASP.NET Core MVC + SQL Server + EF Core system: 36 modules, Arabic/English with
full RTL, one school per deployment, multi-academic-year, with an ERP accounting product
embedded in-process. `.NET 5` (`net5.0`, C# 9, `Nullable enable`, `TreatWarningsAsErrors`).
Repo root is `sms/`; `E:\school2028` itself is not a git repo and its stray `src/`, `tests/`,
`ERP2028.sln` predate this work — ignore them.

## How work runs here

Every task follows one shape — **understand → scope → build → verify → land** — carried by the
`sms-task` skill. Load it at the start of any non-trivial task, and again before calling one done.
It is where the definition of done and the staging/commit discipline live.

| The work is | Skill | Delegate to |
|---|---|---|
| an entity, engine, port, EF config, migration | `sms-engine` | `sms-implementer` |
| a controller action, view, permission, navigation | `sms-screen` | `sms-implementer` |
| seeded reference data — reports, widgets, workflows, lookups | `sms-seed` | `sms-implementer` |
| anything under `external/erp`, the bridge, or the ledger | `sms-erp` | — |
| running the product to see it work | `sms-smoke` | `sms-verifier` |
| judging a diff before it lands | `sms-review` | `sms-reviewer` |
| reading a module's spec without burning context on it | — | `sms-spec-reader` |

The rest of this file is the standing law those skills assume: read it before touching code.

## Requirements authority

`docs/` **is** the specification, approved as Analysis v1.0 and closed. `docs/Modules/01..36`
own each module's rules and its numbered "Required screens" list; `docs/03-Business-Rules.md`
owns the `BR-XXX-###` ids; `docs/Database/` owns schema conventions; `docs/UI/` owns the screen
patterns; `docs/Implementation/` owns the S0–S8 work breakdown; `docs/Status/` holds the
current gap plans (written in Arabic).

Read the module doc before writing the module's code, and cite it in the XML summary of
whatever you build (`doc/Modules/17 §8`, `BR-GRA-003`). **When you cannot implement what a doc
requires, say so in the code comment, the commit message, and to the user** — a deviation is a
change-request-worthy finding, never something to quietly substitute.

## Layout

| Path | Holds | May reference |
|---|---|---|
| `src/Sms.Domain` | entities, enums, value objects, marker interfaces, `[Audited]` | nothing |
| `src/Sms.Application` | ports (`I*Admin`, `I*Service`) + **pure static engines** | Domain |
| `src/Sms.Infrastructure` | EF Core, `AppDbContext`, configurations, migrations, service impls, seeders | Domain, Application |
| `src/Sms.Web` | MVC controllers, views, view models, navigation, security filters, **all DI wiring** (`Startup.cs`) | everything |
| `src/Sms.Erp.Bridge` | the only project that adapts this system to the embedded ERP | Sms.*, ERP `*.Contracts` only |
| `tools/Sms.Seeder` | standalone demo/reference-data seeder | — |
| `external/erp` | the ERP product as a **read-only submodule** | — |

`tests/Sms.ArchitectureTests` enforces those columns with NetArchTest. A forbidden reference
fails CI; do not work around it.

Domain and Application mirror the same module folder names (`Grading/`, `Fees/`, `Admissions/`…).
Put new code in the existing module folder.

## Engines are static classes

Pure business-rule logic lives in `Sms.Application/<Module>/` as **static classes with no DI** —
`PermissionEvaluator`, `WorkflowEngine`, `NumberFormatEngine`, `ScaleBandResolver`,
`BlueprintWeightValidator`, `MarksheetStatusTransitions`, `TermScoreCalculator`. That holds even
when a doc calls the thing a "domain service". They take entities and values in, return decisions
out, touch no database, and are unit-tested directly in `tests/Sms.Application.Tests` (the largest
test project, deliberately).

Anything needing the database is an interface in `Sms.Application` implemented in `Sms.Infrastructure`.

## Every entity declares what it is (`Sms.Domain.Common`)

- `ISchoolScoped` (`int SchoolId`) — auto tenant-filtered by `SmsDbContext`. Everything gets it
  **except** what defines or transcends the scope: `School` itself, `AuditEntry`,
  `JobDefinition`/`JobRun`. Child rows carry their own `SchoolId` too — the filter must hold at
  every level, not just the aggregate root.
- `IYearScoped` (`int AcademicYearId`).
- `IActivatable` (`bool IsActive`) — makes hard-delete throw `HardDeleteForbiddenException`. Omit
  it deliberately on entities that will need a real physical-purge path (e.g. `Attachment`).
- `ISoftActiveFiltered` — opt-in `WHERE IsActive` filter. **Skip it on versioned catalogs**
  (`WorkflowDefinition`, `NumberingSeries`): old versions must stay loadable for pinned in-flight
  references.

There is **no Delete verb in this product** (BR-GLB-005). Deactivate, void, cancel, end.

## Audit is declarative

Tag the entity `[Audited(AuditTier.T1|T2|T3)]` (T1 = field-level + reason required, T2 =
field-level, T3 = record-level). `AuditCaptor` inside `SmsDbContext.SaveChanges` diffs and writes
entries **in the same transaction** — no per-service code, ever.

`[RequiresAuditReason]` on a property + T1 on the class ⇒ `MissingAuditReasonException` unless
`IAuditContext.Reason` was set before the save. Two traps:

- It fires only on `EntityState.Modified`, never `Added`. If rows of that entity are ever
  **pre-seeded as stubs and filled in later** (as `MarkEntry` is), the first real entry is an EF
  `Modified` transition and wrongly demands a reason. Check that before tagging.
- **Do not tag high-churn entities at all** (`UserAccount.AccessFailedCount`,
  `UserSession.LastActivityAtUtc`, `Delivery`, `LoginAttempt`, `JobRun`). Security and execution
  *events* go through explicit `IAuditEventWriter.Log(AuditAction.X, …)` calls. Append-only logs
  (`PasswordHistory`, `TemplateVersion`, `AttachmentVersion`) are never `[Audited]` — auditing a
  log is circular.

## Two service shapes — do not mix them

1. **Ambient, never saves** — `IWorkflowFinalEffect.ApplyAsync`, `INumberIssuer.IssueAsync`,
   `INotificationPublisher.PublishAsync`. The caller's `SaveChangesAsync` commits everything
   together. This is what makes "a number materializes only with the receipt it stamps" true; a
   port that saves itself destroys the guarantee it exists for.
2. **Standalone, saves itself** — every `*Admin` (`SchoolAdmin`, `GradingAdmin`, `LookupAdmin`,
   `NumberingSeriesAdmin`…). Config/admin operations not riding a larger transaction; each method
   calls `SaveChangesAsync`.

**Pluggable strategies** (`IWorkflowFinalEffect`, `IChannelSender`, `ISeedContributor`,
`IJobHandler`) all share one shape: a `Code` discriminator, many `AddScoped<TIface, TImpl>()`
registrations, injected as `IEnumerable<TIface>`, matched by code at runtime. Use that instead of
a switch statement.

## Screens: deny by default, and the build proves it

Every controller action carries `[RequirePermission(ScreenCatalog.Modules.X, ScreenCatalog.X.Y,
ActionVerb.Z)]` **or** `[NoPermissionRequired("stated reason")]`. `ScreenPermissionTests` fails
the build otherwise — that test exists because the attribute was once written and used in zero
controllers, leaving every finance screen open to anyone who could sign in.

`src/Sms.Application/Security/ScreenCatalog.cs` is the single table: the seeder catalogues it into
`sec.Permission`, controllers name entries from it, the sidebar and the P-LAUNCH workspace hide
what the user cannot open (BR-SEC-010), and `WorkspaceCatalogTests` holds launcher tiles to the
same constants. Adding a screen means adding it there first.

Missing permission ⇒ `NotFound`, never AccessDenied. Unauthorized surface disappears rather than
errors.

Verb meanings are fixed in `ScreenCatalog`'s header comment — read it rather than guessing
(`Deactivate` covers delete/void/cancel; `Post` moves money; `Configure` changes the system's own
shape).

## Bilingual is a hard rule, not a nicety

Every user-visible string ships Arabic **and** English. Controllers and views both use the same
local helper:

```csharp
private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
private static string T(string en, string ar) => IsArabic ? ar : en;
```

Enum display goes through `Sms.Web/Models/Labels.cs` or a module's own `*Labels` class — never
`enum.ToString()` on screen. **Every refusal the user can trigger must be translated at the Web
boundary**; never surface an engine's English exception text raw. Money stays LTR-digit and
right-aligned in both directions; dates are Gregorian input with optional Hijri sub-display
(never auto-switch the calendar with the language).

`_Layout.cshtml` picks `bootstrap.rtl.min.css` and sets `dir` from the culture. Screens follow the
`P-*` pattern catalog in `docs/UI/02-Screen-Patterns.md`.

## Persistence rules live in one place

`SmsDbContext` (base) centrally does tenant filtering, the soft-active filter, created/modified
stamping, the cross-school write guard, the hard-delete guard, and audit capture. `AppDbContext`
(derived) holds the `DbSet`s, applies `IEntityTypeConfiguration`s from the assembly, then
downgrades **every non-ownership cascade FK to `Restrict`** model-wide. Do not add per-entity
`OnDelete` — owned types must stay `Cascade` or `Remove()` throws.

## EF Core traps — every one of these was paid for once already

- **`Sum()`/`SumAsync()` over `decimal` throws at runtime on Sqlite.** Compiles fine, fails only
  when executed. Materialize first (`.Select(x => x.Amount).ToListAsync()`) then `.Sum()` in
  memory. Assume the same for `Min`/`Max`/`Average`.
- **Two `HasIndex` calls over the same property list silently collapse into one** (EF keys by
  property list, not name). For 2+ indexes on identical columns use the named overload:
  `builder.HasIndex(x => x.SchoolId, "IX_Name")`.
- **Long per-row batch loops on one `DbContext` are quadratic** — the change tracker keeps
  everything and `DetectChanges` re-walks it each save. A 1,020-student rollover went from >10 min
  to 16 s by calling `_db.ChangeTracker.Clear()` after each committed unit. Any loop that commits
  per row (rollover, dunning, batch charge generation, test seed loops) needs it — and must
  **re-load any header row it mutates after the loop**, because the detached instance silently
  will not save.
- **A soft-active master row keeps being referenced after deactivation.** Load the list through
  the filter, then `First(x => x.Id == row.SomethingId)`, and the page dies with "Sequence
  contains no matching element" the day someone retires a row. Use `IgnoreQueryFilters()` for the
  *lookup*; keep the filtered list for the *picker* — a different list answering a different
  question. Enforced by `SoftActiveLookupTests` for `GradeLevels`, `Stages`, `Subjects`,
  `FeeCategories`.
- Filtered unique index syntax `.HasFilter("[Status] = 1")` works on both providers.
- Prefer a plain `.IsConcurrencyToken()` column over SQL Server `ROWVERSION` — portable.
- Sqlite round-trips `78m` as `"78.0"`; don't assert exact strings for whole decimals.
- **Verify a uniqueness/concurrency guarantee actually fired** with a test that bypasses the
  service layer and asserts the `DbUpdateException` — "it compiled" proves nothing.

## Naming collisions to check before naming an entity

- A Domain entity whose simple name equals a project namespace segment (`Application`, `Domain`,
  `Infrastructure`, `Web`) breaks with `CS0118` in every file under that project, and
  `using Alias = …` does **not** fix it. Alias under a different name:
  `using AdmissionApplication = Sms.Domain.Admissions.Application;`.
- Two entities in different module namespaces sharing a simple name give `CS0104` the moment one
  file `using`s both. Already hit by `Trip`, `ConsentRecord`, `Program` — each renamed
  module-prefixed (`ActivityTrip`, `ActivityProgram`).

Before naming anything generic (Trip, Program, Session, Request, Event, Record, Item):
`grep -rn "class <Name>\b" src/Sms.Domain`, plus `src/Sms.Web/Program.cs` and
`tools/Sms.Seeder/Program.cs`.

## Enums

Start at 1 (SMALLINT convention) — **unless** a DB doc gives explicit numeric values for that
status column (e.g. `AcademicYear.Status` starts at 0). Honor the doc; don't "fix" it to match
the convention.

## Migrations

Schema is migration-managed (`src/Sms.Infrastructure/Persistence/Migrations`). Any model change
needs one or SQLEXPRESS drifts.

```bash
dotnet ef migrations add <Desc> -p src/Sms.Infrastructure -s src/Sms.Web -c AppDbContext
```

Then **rename EF's timestamp id to `yyyyMMdd_Desc`** per `docs/Database/01`. Two migrations on one
day sort alphabetically by description — name accordingly. `Program.Main` applies pending
migrations at start in Development, ahead of Hangfire's own schema bootstrap.

The seven embedded ERP module contexts each keep their own `__EFMigrationsHistory` in their own
schema and are migrated first, in `Program.ApplyPendingMigrations`' fixed order, ahead of
`AppDbContext`.

## Testing

| Project | Contains |
|---|---|
| `Sms.Application.Tests` | pure engine unit tests — the bulk |
| `Sms.Infrastructure.Tests` | `*AdminTests` over a real `AppDbContext` on `DataSource=:memory:` Sqlite + `EnsureCreated()` |
| `Sms.ArchitectureTests` | layer directions, ERP boundary, screen-permission coverage, soft-active lookups |
| `Sms.Web.Tests` | navigation, permission filter, label tests |
| `Sms.TestSupport` | `[BusinessRule("BR-X-###")]` trait + `PerfGate` (P95 vs NF-P budgets) |

Test doubles are private nested classes in the test file: `FixedClock` (mutable `UtcNow`),
`FixedUser` (mutable `UserId`), `FixedTenant` (`SchoolId`/`AcademicYearId`), implementing
`IClock`/`ICurrentUser`/`ITenantContext`+`IWorkingYearContext`.

Tag every test that verifies a numbered rule with `[BusinessRule("BR-…")]` — it feeds the CI
coverage gate (NF-M5).

Concurrency tests use **deterministic two-context interleaving**, not real threads: a shared
Sqlite connection is not safe for genuine parallel async, and the services never save
mid-operation anyway.

Package versions are pinned to the .NET 5 era — EF Core 5.0.17, xunit 2.4.1 (2.9 runners drop
net5), Hangfire 1.7.34. Don't upgrade casually.

```bash
dotnet test --configuration Release
```

## Sqlite hides a whole class of SQL Server failure

Everything is tested on Sqlite; the real database is SQLEXPRESS. Multiple cascade paths (error
1785), the decimal `Sum` difference, index behaviour, and collation only show up on a real run.
**After a significant model change, run the app against SQLEXPRESS** — don't close the work on
green Sqlite tests alone.

## The embedded ERP boundary

`ERP2028` is hosted in-process: 7 of its modules (Organization, Accounting, Inventory, Purchasing,
Sales, Cash, Partners) mounted as MVC areas inside `Sms.Web`, sharing one database through separate
schemas, with the sidebar reading the ERP's own nav providers. Communication, FixedAssets and
Identity ship in the submodule but are not mounted here.

The arrangement is only worth anything while **deleting `Sms.Erp.Bridge` leaves a standalone
school system**. So:

- `Sms.Domain` / `Sms.Application` / `Sms.Infrastructure` must not name an `ERP2028` type.
- `Sms.Erp.Bridge` may see each module's `.Contracts` **and nothing else of it**.
- `Sms.Web` (the composition root) is the only project allowed the ERP's other layers, because
  registering a module and migrating its context cannot be expressed through a contract.
- `external/erp` is **read-only here**. CI fails if it has local modifications. An accounting fix
  belongs in the ERP repository, followed by moving the submodule pointer.

`ErpBoundaryTests` enforces the first three.

## Running it

```bash
dotnet run --no-launch-profile --project src/Sms.Web --urls http://localhost:5099
```

Development migrates and seeds on start. SQL Server exists only as `.\SQLEXPRESS`, while
`appsettings.json` says `Server=.` — override with the env var, never by editing the file:

```bash
ConnectionStrings__Sms="Server=.\SQLEXPRESS;Database=Sms;Trusted_Connection=True;MultipleActiveResultSets=true"
```

`tools/Sms.Seeder` has no appsettings, so it *requires* that env var. Both it and the account
seeders are idempotent on username — re-running never resets a password. Sign-in: `admin` (staff,
holds SYSADMIN) and `parent` / `student` for the portal; one-time values are constants in
`SysAdminAccountSeedContributor` / `PortalDemoAccountSeedContributor`, and there is still no
reset-password screen — recover a password by calling
`IAuthenticationService.SetTemporaryPasswordAsync` from a scratchpad tool. Force Arabic in a
browser check with cookie `.AspNetCore.Culture=c%3Dar-SA%7Cuic%3Dar-SA`.

Views are compiled at build: a `.cshtml` edit needs `dotnet build` + restart.

## Git

- **Never `git add -A`.** Other sessions and processes drop files into this working tree
  constantly; broad stages have repeatedly swept another epic's half-finished work into a commit.
  Stage explicit paths and read `git status --short` for `??`/`M` entries you did not write. Diff
  shared files (`AppDbContext.cs`, `Startup.cs`) for another epic's keywords before trusting a
  stage.
- `.gitignore` carries a VS-boilerplate `Backup*/` rule that silently swallows first-party folders
  named `Backup` — negations are appended after it. Confirm `git status --short` actually lists
  new files under any `*/Backup/*`-style path.
- Commit subjects here describe the *change in the product*, in plain sentences ("Enforce screen
  permissions — the finance screens were open to anyone"), not `feat(scope):` prefixes. Follow the
  existing log.

## Environment notes

- **No `python` on this box** — `python`/`python3` hit the Windows Store stub and exit 49
  silently. Use the Edit/Write tools, or perl/sed. Perl `s|…|…|` breaks on C# `||`; use `s#…#…#`
  or the Edit tool.
- Git Bash: set `MSYS_NO_PATHCONV=1` before `curl` on a rooted path, and then use relative `-o`
  output paths.
- Razor: `v@version` renders literally (email heuristic) — write `v@(version)`. A Razor local
  function containing tag helpers must be `async Task` and be invoked as `@{ await Fn(…); }`.
- Another Claude session may be running its own `Sms.Web` from `src/Sms.Web/bin` against the same
  local database. Build to a private directory and run on your own port rather than assuming the
  tree or the data is yours alone.
