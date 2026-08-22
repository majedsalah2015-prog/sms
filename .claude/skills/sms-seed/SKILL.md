---
name: sms-seed
description: Add or change seeded reference data — report and widget definitions, workflow definitions, job definitions, lookups, permissions, numbering series, a country content pack, or demo data. Use when a screen renders an empty catalogue, when a platform is built but nothing is defined in it, or when adding an ISeedContributor.
user-invocable: true
---

# Seeding reference data

Several platforms in this product are finished and empty: the report centre renders a tree with
no `ReportDefinition` rows, the dashboards manage widgets nobody defined, the workflow engine
runs definitions nobody seeded. **A platform with no content is indistinguishable from a broken
platform to the person looking at it**, so seeding is product work, not fixture work.

## The contract

`ISeedContributor` in `src/Sms.Application/Seeding/`, implemented in
`src/Sms.Infrastructure/Seeding/`, registered as a fan-out `AddScoped<ISeedContributor, X>()` in
`Startup.cs`, run by `SeedRunner` in `Order`.

```csharp
public string Name => "Recurring job definitions (doc 02 T-6)";   // shown in the run log
public int Order => 15;                                            // dependencies seed first
public Task SeedAsync(CancellationToken cancellationToken = default) => …;
```

### Order is a dependency statement, not a preference

The bands in use today — put new work in the band its dependencies allow, and say why in a
comment as the existing ones do:

| Order | Seeds |
|---|---|
| 10 | product lookups — everything else names them |
| 15 | geography · job definitions · the KSA-01 content pack — system-level, depend on nothing |
| 20–22 | role templates → school permissions → the ERP's external permissions |
| 25 | the sysadmin account (needs the roles) |
| 30 | the numbering catalogue — before anything that issues a number |
| 45–55 | demo data, then the portal demo accounts |
| 70 | GL account mappings — last, they name accounts the ERP seeded |

### Idempotent, always

Re-running the seeder on a seeded tenant is a no-op, never a duplicate and never a reset. The
account seeders are idempotent **on username**, so re-running never resets a password — that
property is relied upon, do not weaken it.

Key each row on its natural business key (code, username, series key, definition key), check for
its presence, and insert only what is missing. An `Any()`-then-insert per row is correct here;
what is not correct is clearing the table first.

Existing rows are **updated only where the update is safe** — a renamed label, yes; a changed key
or a changed numeric value someone's data already points at, no. If a definition genuinely has to
change shape, version it rather than mutating it: `WorkflowDefinition` and `NumberingSeries` are
deliberately not `ISoftActiveFiltered` so old versions stay loadable for in-flight references.

## Writing the rows

- Every user-visible name is **bilingual** — a seeded English-only label leaks into an Arabic
  screen and no test catches it.
- Money, dates and enums follow the same rules as anywhere else; enum values start at 1 unless
  the DB doc fixes them otherwise.
- Multi-tenant rows carry `SchoolId`; the seeder runs inside the tenant context, so let the
  context stamp it rather than hard-coding one.
- A long insert loop is still a loop that commits: `_db.ChangeTracker.Clear()` per unit, and
  re-load any header row mutated after it.
- Cite the doc the content comes from in the `Name` and the XML summary — `docs/Reports/Report-Catalog.md`
  for reports, `docs/Reports/Dashboard-Specifications.md` for widgets, `docs/05-Workflow.md` for
  the WF catalog, the module doc for anything module-specific.

## Content that is known to be missing

Do not re-derive this list; it is the current state, and each item is a task waiting for content
rather than for engineering:

| Missing | Source doc | Note |
|---|---|---|
| `ReportDefinition` rows | `docs/Reports/Report-Catalog.md` (230 described) | E-701 explicitly ships the *platform* plus the three operating loops, not the long tail |
| `WidgetDefinition` rows | `docs/Reports/Dashboard-Specifications.md` (78 described) | `IDashboardQuery` carries three queries today; a widget with no query behind it is worse than no widget |
| `WorkflowDefinition` rows | `docs/05-Workflow.md` (WF catalog) | until these exist, every approval chain in the screens is a status change with no chain |
| Notification templates | `docs/09-Notifications.md` | and the channels behind them are `InApp` + `Stub` only |
| Residence hierarchy beyond Gaza City | `GeographySeedContributor` | 33 localities still need their quarters |

The country pack `KSA-01` is seeded with Palestinian data. That is an **owner decision**, not a
bug — do not "fix" it.

## Verify

```bash
dotnet test tests/Sms.Infrastructure.Tests --configuration Release
```

Then prove idempotency for real, because it is the property most easily lost and least likely to
be noticed:

1. Run the app (or `tools/Sms.Seeder`) against SQLEXPRESS once, count the rows.
2. Run it again. **Same count, same ids, and no password reset.**
3. Load the screen that reads the seeded rows and confirm both languages render.

`tools/Sms.Seeder` has no `appsettings.json`, so it *requires* the connection string in the
environment:

```bash
ConnectionStrings__Sms="Server=.\SQLEXPRESS;Database=Sms;Trusted_Connection=True;MultipleActiveResultSets=true"
```
