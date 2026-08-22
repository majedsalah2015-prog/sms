---
name: sms-engine
description: Build or extend a business slice in the SMS backend — Domain entity, pure Application engine, I*Admin port, EF configuration, Infrastructure implementation, DI registration, and Sqlite integration tests. Use when adding an entity, a business rule, a new module capability, or a BR-numbered behaviour; use it before writing any new .cs file under src/Sms.Domain, src/Sms.Application or src/Sms.Infrastructure.
user-invocable: true
---

# Building a slice

Work outward: Domain → Application → Infrastructure → Web. Each step compiles and is tested
before the next one starts. `Sms.ArchitectureTests` fails the build if the direction is ever
reversed.

## 0. Read the rules first

`docs/Modules/NN-*.md` for the module, and `docs/03-Business-Rules.md` for the `BR-` ids it cites.
The doc is the specification — approved, closed, and change-controlled. Cite the section and rule
ids in the XML summary of everything you write. If the doc asks for something you cannot build
(a missing module, an unavailable technology), **build the rest and say so explicitly** in the
code comment, the commit message, and to the user. Do not substitute silently.

## 1. Domain — `src/Sms.Domain/<Module>/`

An entity is a plain class with auto-properties, no behaviour beyond invariants, no dependencies.

Decide four things, deliberately:

| Marker | Add it when | Skip it when |
|---|---|---|
| `ISchoolScoped` | almost always | the entity defines or transcends the tenant (`School`, `AuditEntry`, `JobDefinition`/`JobRun`) |
| `IYearScoped` | the row belongs to one academic year | it outlives the year |
| `IActivatable` | the row is deactivated, never deleted (nearly all master data) | it will need a real physical purge later (e.g. `Attachment`) |
| `ISoftActiveFiltered` | inactive rows should vanish from queries | old versions must stay loadable — versioned catalogs, anything pinned by an in-flight reference |

Child rows of a scoped parent carry their **own** `SchoolId`. The filter must hold at every level.

Then audit: `[Audited(AuditTier.T1|T2|T3)]` on the class. Before adding `[RequiresAuditReason]` to
a property, check whether rows of this entity are ever pre-seeded as stubs and filled in later —
the attribute fires on `Modified` only, so a stub's first real value would wrongly demand a
reason. Do not tag high-churn or append-only entities at all.

**Check the name for a collision before you commit to it.** Anything generic — Trip, Program,
Session, Request, Event, Record, Item — needs
`grep -rn "class <Name>\b" src/Sms.Domain` plus the two `Program.cs` entry points. A name equal to
a project namespace segment (`Application`, `Domain`, `Infrastructure`, `Web`) is worse: it breaks
with `CS0118` and aliasing does not fix it. Rename module-prefixed instead (`ActivityTrip`).

Enums start at 1, unless a DB doc gives that status column explicit numbers — then honor the doc.

## 2. Application — `src/Sms.Application/<Module>/`

Two kinds of file, and nothing else:

**Pure engines** — `static class` with no DI, taking entities/values in and returning decisions
out. `BlueprintWeightValidator`, `MarksheetStatusTransitions`, `ScaleBandResolver`,
`SchoolStatusTransitions` are the models to copy. Write these even when the doc calls the thing a
"domain service"; that is this codebase's convention. Status machines belong here as a
transitions table, not as `if` chains scattered across a service.

**Ports** — `I<Module>Admin` (or `I<Thing>Service`). Async, `CancellationToken` last with a
default. Every method's XML doc names the exception it throws and the `BR-` rule behind it:

```csharp
/// <summary>Throws <see cref="Common.Exceptions.BlueprintWeightMismatchException"/>
/// unless component weights sum to exactly 100.</summary>
Task LockBlueprintAsync(int blueprintId, CancellationToken cancellationToken = default);
```

Exceptions go in `Common/Exceptions/<Module>Exceptions.cs` — one file per module, deriving from
`InvalidOperationException` so the Web layer's existing catch shape works. The message is English
here; the Web layer translates.

Choose the service shape and stick to it:

- **Ambient (never saves)** for anything that must commit atomically with a caller's business row
  — number issuing, workflow final effects, notification publishing.
- **Standalone (saves itself)** for `*Admin` config/definition operations.

If a new "strategy chosen by a code" is needed, follow the fan-out shape: interface with a `Code`
property, many `AddScoped` registrations, injected `IEnumerable<T>`, matched at runtime.

## 3. Infrastructure — `src/Sms.Infrastructure/<Module>/`

The `*Admin` implementation, plus an `IEntityTypeConfiguration<T>` in
`Persistence/Configurations/`. Register the `DbSet` on `AppDbContext`.

Configuration rules:
- Table/column naming per `docs/Database/01-Naming-Standards.md`; schema prefixes match the module.
- **Two indexes over the same property list collapse into one.** Use the named overload
  `builder.HasIndex(x => x.SchoolId, "IX_Explicit_Name")` whenever a second index shares columns.
- Do **not** set `OnDelete` — `AppDbContext.OnModelCreating` downgrades every non-ownership
  cascade to `Restrict` model-wide (SQL Server error 1785). Owned types keep `Cascade`.
- Filtered unique indexes: `.HasFilter("[Status] = 1")` — portable to both providers.
- Optimistic concurrency: `.IsConcurrencyToken()` on a plain column, not `ROWVERSION`.

In the service itself:
- No `SumAsync()` / `Sum()` on a decimal column — it throws at runtime on Sqlite. Materialize the
  column, sum in memory.
- Any loop that commits per row calls `_db.ChangeTracker.Clear()` per unit, and re-loads any
  header row it mutates afterwards.

Register in `src/Sms.Web/Startup.cs` — that is the only composition root.

## 4. Migration

A model change without a migration means the SQLEXPRESS database drifts silently.

```bash
dotnet ef migrations add <Desc> -p src/Sms.Infrastructure -s src/Sms.Web -c AppDbContext
```

Rename EF's generated timestamp id to `yyyyMMdd_Desc`. Two migrations the same day sort
alphabetically by description — pick names that order correctly.

## 5. Tests — the slice is not done without them

**Engine tests** in `tests/Sms.Application.Tests/<Module>/` — direct calls, no fixtures, one fact
per rule.

**Integration tests** in `tests/Sms.Infrastructure.Tests/<Module>AdminTests.cs`, over a real
`AppDbContext`:

```csharp
_connection = new SqliteConnection("DataSource=:memory:");
_connection.Open();
using var db = CreateContext();
db.Database.EnsureCreated();
```

with private nested `FixedClock` / `FixedUser` / `FixedTenant` doubles, exactly as
`GradingAdminTests` does. Seed loops need `ChangeTracker.Clear()` too.

Tag anything verifying a numbered rule:

```csharp
[Fact]
[BusinessRule("BR-GRA-003")]
public async Task Publishing_computes_a_term_result_per_enrollment() { … }
```

Cover the refusals as well as the happy path — a rule with no test asserting it *refuses* is not
covered. For a uniqueness or concurrency guarantee, write a test that **bypasses the service and
saves directly**, asserting `DbUpdateException`. For races, interleave two contexts
deterministically; do not spawn threads against a shared Sqlite connection.

```bash
dotnet test --configuration Release
```

## 6. Then prove it on SQL Server

Sqlite cannot see multiple-cascade-path rejections, decimal aggregate behaviour, or collation.
After any significant model change, run the app against SQLEXPRESS (`sms-smoke` skill) and
exercise the new path once for real.
