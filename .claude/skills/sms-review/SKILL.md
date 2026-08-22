---
name: sms-review
description: Review SMS changes against the failure classes this codebase actually produces — tenant scoping, deny-by-default permissions, audit tagging, soft-active lookups, Sqlite-only decimal aggregates, change-tracker growth, bilingual leaks, migration drift, and the ERP boundary. Use for reviewing a diff, a commit, a branch, or a module before it is called done, and in place of a generic code review here.
user-invocable: true
---

# Reviewing SMS code

A generic review misses what this system actually gets wrong. Every check below exists because
the mistake was made here at least once, shipped, and cost a screen, a balance, or a security
boundary. Work through them in order — the first five are the ones that have caused real damage.

## Scope the review

```bash
git status --short
git diff --stat
git log --oneline -5
```

Review the working-tree diff by default, or the named commit/branch/path if one was given. **This
tree collects other sessions' files**: anything in the diff you did not write is context, not
your subject — say so rather than reviewing it as if it were the change.

Read the module's `docs/Modules/NN-*.md` for the rules the code claims to enforce. A change that
contradicts an approved doc is a finding even when the code is correct.

---

## 1. Tenant and scope

- Does every new entity implement `ISchoolScoped` — including **child rows**, which need their own
  `SchoolId` for the filter to hold below the aggregate root?
- Is `IActivatable` present (making hard-delete throw), or deliberately absent with a stated
  reason?
- Is `ISoftActiveFiltered` correct? On a versioned catalog it is a bug: old versions must stay
  loadable for pinned in-flight references.
- Does any controller trust a posted school/tenant id instead of binding to `_tenant.SchoolId`?
- Does any query use `IgnoreQueryFilters()` where it should not — reading past the tenant filter
  rather than past the soft-active one?

## 2. Permissions

- Every controller action carries `[RequirePermission(...)]` or `[NoPermissionRequired("reason")]`.
  `ScreenPermissionTests` catches omissions, but it cannot catch a **wrong** pairing: check the
  module/screen constants actually match the screen, and the verb matches what the action does
  (`Post` for money movement, `Approve` for decisions, `Configure` for shape changes, `Deactivate`
  for delete/void/cancel — there is no `Delete`).
- POSTs carry `[ValidateAntiForgeryToken]`.
- A new screen exists in `ScreenCatalog`, and any launcher tile names the same constants.
- Export and print actions are separately gated (BR-SEC-021) — an `Export` verb hidden behind a
  `View` permission hands data out to someone who may only look.

```bash
grep -n "public async Task<IActionResult>" src/Sms.Web/Controllers/<X>Controller.cs
```

## 3. Money and decimals

- **`Sum()` / `SumAsync()` over a decimal column throws at runtime on Sqlite** and compiles
  cleanly. Every money aggregate must materialize first, then sum in memory. Assume the same for
  `Min`/`Max`/`Average`.
- Rounding: is it done once, at a stated place, or accumulated across a loop?
- Anything posting to the ledger: does the entry balance, and does a void/cancel produce a
  reversing entry in the **current** period rather than editing an exported one?
- VAT: is it separated at the posting, not folded into the gross?

```bash
grep -rn "SumAsync\|\.Sum(" src/Sms.Infrastructure src/Sms.Web --include=*.cs
```

## 4. Soft-active lookups

The failure that has taken three screens down: load `GradeLevels`, `Stages`, `Subjects` or
`FeeCategories` through the query filter, then `First(x => x.Id == row.SomethingId)`. It reads as
correct, and dies the day someone deactivates a row. The picker list and the lookup list are two
different lists answering two different questions — the lookup one needs `IgnoreQueryFilters()`.

`SoftActiveLookupTests` covers controllers and those four sets only. **Check services, views, and
any other soft-active master data by hand.**

## 5. Change tracker and batch loops

Any loop committing per row — rollover, dunning, batch charge generation, imports, test seeds —
needs `_db.ChangeTracker.Clear()` per unit, and must **re-load any header row it mutates after the
loop**; a detached instance silently does not save. Without the clear the loop is quadratic: a
1,020-row rollover took over 10 minutes instead of 16 seconds.

---

## 6. Audit

- Is the tier right? T1 = field-level + reason, T2 = field-level, T3 = record only.
- `[RequiresAuditReason]` on a field whose rows are ever **pre-seeded as stubs and filled in
  later** is a bug — the attribute fires on `Modified` only, so the first real entry wrongly
  demands a reason (this broke every teacher's first mark entry).
- High-churn entities (`AccessFailedCount`, `LastActivityAtUtc`, `Delivery`, `LoginAttempt`,
  `JobRun`) must not be `[Audited]` at all — their events go through `IAuditEventWriter`.
  Append-only logs are never audited.
- A screen editing a T1 field sets `IAuditContext.Reason` **before** the call and offers a reason
  input.

## 7. Bilingual and RTL

- Every user-visible string has both languages. Grep the diff for a bare English literal reaching
  a view or a `TempData`/`ModelState` message.
- **Engine exception text is English and must be translated at the Web boundary** — never surfaced
  raw. This is a standing rule for every refusal the user can trigger.
- Enums render through `Labels` / `*Labels`, never `ToString()`.
- Money right-aligned with LTR digits in both directions; dates Gregorian with optional Hijri
  sub-display — Arabic must not switch the calendar.
- Tables have `scope="col"` headers and a `visually-hidden` caption; icon-only controls have
  accessible names; empty states say what to do.
- `v@version` renders literally in Razor — must be `v@(version)`.

## 8. Layering

- `Sms.Domain` references nothing; `Sms.Application` references Domain only; `Sms.Infrastructure`
  does not reference `Sms.Web`. DI registration happens only in `Startup.cs`.
- Business rules in a controller belong in an `Sms.Application` engine — static class, no DI.
- A new "strategy by code" uses the fan-out shape (`Code` discriminator + `IEnumerable<T>`), not a
  switch.
- Service shape: an ambient port (`IWorkflowFinalEffect`, `INumberIssuer`,
  `INotificationPublisher`) must **not** call `SaveChangesAsync` — that breaks the atomicity it
  exists for. An `*Admin` must.

## 9. Persistence details

- A model change has a migration, named `yyyyMMdd_Desc`, and the snapshot is regenerated. No
  migration means the SQLEXPRESS database drifts.
- Two `HasIndex` calls over the same property list collapse into one — the named overload is
  required.
- No per-entity `OnDelete`: `AppDbContext` downgrades cascades model-wide, and owned types must
  stay `Cascade`.
- Enum values start at 1 unless a DB doc fixes them otherwise.
- New generic-sounding entity names checked against `src/Sms.Domain` and both `Program.cs` files
  for `CS0104`/`CS0118` collisions.

## 10. The ERP boundary

- No `ERP2028` type named from `Sms.Domain`, `Sms.Application` or `Sms.Infrastructure`.
- `Sms.Erp.Bridge` touches only each module's `.Contracts`.
- `external/erp` unmodified — `git -C external/erp status --porcelain` must be empty. A fix made
  here would be invisible to the ERP product and lost at the next submodule bump.

## 11. Tests

- Rules verified by tests tagged `[BusinessRule("BR-…")]`, refusals covered and not only the happy
  path.
- Uniqueness/concurrency claims proven by a test that **bypasses the service** and asserts the
  `DbUpdateException` — not by the fact it compiled.
- Race tests use deterministic two-context interleaving, not threads on a shared Sqlite
  connection.
- Anything user-visible has actually been rendered (see `sms-smoke`). Committed actions with no
  view have shipped here and returned 500s.

```bash
dotnet build --configuration Release && dotnet test --configuration Release
```

---

## Reporting

Verify each candidate before reporting it: open the file, read the surrounding code, and state the
concrete failure — the input or data state that triggers it and what goes wrong. Drop anything you
cannot make concrete; a plausible-sounding finding that does not reproduce wastes more time than
it saves.

Rank by consequence: wrong money and open permissions first, then data loss and broken screens,
then rule violations against the docs, then quality. Note explicitly when the diff contradicts an
approved `docs/` requirement — that is a change-request finding, not a style note.

If `ReportFindings` is available, report through it, most severe first, and do not also print the
findings as prose.
