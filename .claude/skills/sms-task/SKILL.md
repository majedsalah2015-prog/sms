---
name: sms-task
description: The working pattern every task in this repository follows — how a task starts (read the doc, scope it, pick the build skill), how it is verified (build, tests, SQL Server, both languages), and how it lands (explicit staging, commit style, stated deviations). Use at the START of any non-trivial task here, before choosing a build skill, and again before calling a task done or committing.
user-invocable: true
---

# How a task runs here

One shape for every task, so the twentieth screen is built the way the first was and the
reviewer, the tests and the next session all find what they expect. Five phases. None of them
is optional, and the order is not decorative: skipping phase 1 is how a screen gets built that
the module doc never asked for, and skipping phase 4 is how a committed action ships with no
view behind it.

```
1 Understand  →  2 Scope  →  3 Build  →  4 Verify  →  5 Land
```

---

## 1. Understand — the doc is the specification

`docs/` is approved as Analysis v1.0 and closed. Read, in this order:

| Read | For |
|---|---|
| `docs/Modules/NN-<Module>.md` | the module's rules and its numbered §8 "Required screens" |
| `docs/03-Business-Rules.md` | the `BR-XXX-###` ids the module cites |
| `docs/UI/02-Screen-Patterns.md` | the `P-*` pattern a screen must follow |
| `docs/Database/01-Naming-Standards.md` | table/column naming, before any migration |
| `docs/Status/*.md` | what is already known to be missing, and why it was deferred |

Then read the code that already does the nearest thing. This product repeats its own patterns
deliberately: `GradingController` for a workspace with sub-navigation, `SetupController` for a
wizard, `TransportController` for a service module built end to end, `SecurityController` for a
configuration screen over a catalogue. **Copy the closest one rather than inventing a shape.**

Delegate the reading when the module doc is long and you only need its build spec — the
`sms-spec-reader` agent returns the screens, rules and ports without spending this context on
prose you will not reread.

## 2. Scope — say what you are building, and what you are not

Before writing code, state in one short paragraph: which numbered screens or rules are in scope,
which are not, and what you are assuming. Ambiguity that changes the work is a question for the
user; ambiguity that does not is your call to make and record.

**A doc requirement you cannot meet is a finding, never a silent substitution.** If the doc asks
for something blocked (a PDF engine, a missing module, an unmade decision), build everything
around it and say so in three places: the XML summary of the code, the commit message, and your
reply to the user. The project treats a quiet omission as worse than an admitted gap — an
admitted gap gets scheduled; a quiet one gets discovered by a school.

Do not widen the scope either. A task that touches one module does not get to refactor another
because it looked wrong on the way past — note it and move on.

## 3. Build — pick the skill, do not improvise

| The task is | Use |
|---|---|
| an entity, an engine, a port, a rule, a migration | `sms-engine` |
| a controller action, a Razor view, a permission, navigation | `sms-screen` |
| seed data — report/widget/workflow definitions, lookups, a content pack | `sms-seed` |
| anything under `external/erp`, or an accounting fix | `sms-erp` |
| running the product to see it work | `sms-smoke` |
| judging a diff before it lands | `sms-review` |

Layer direction is enforced by `Sms.ArchitectureTests` and is not negotiable: Domain → Application
→ Infrastructure → Web, with all DI wiring in `Startup.cs` and every ERP reference confined to
`Sms.Erp.Bridge` and `Sms.Web`.

Work outward and keep it compiling. A slice that builds at each step localizes its own failures;
a slice written in one pass across four projects does not.

## 4. Verify — the gates, in the order they get cheaper to fix

```bash
dotnet build --configuration Release
```

`TreatWarningsAsErrors` is on: a warning is a failure. Then:

```bash
dotnet test --configuration Release
```

1,611 tests today across five projects (Application 669 · Infrastructure 620 · Web 309 ·
Architecture 12 · Domain 1). The architecture tests are the ones that fail on
structural mistakes — an action with no declared permission, a catalogued permission no action
requires, a layer reference in the wrong direction, a soft-active lookup through the filter, an
ERP boundary crossing, a launcher tile naming a screen that does not exist.

Long runs belong to the `sms-verifier` agent, which reports failures rather than four minutes of
build output.

Then, and this is where green tests stop being evidence:

- **Anything user-visible gets rendered for real, in both languages** — `sms-smoke`. A committed
  action with no view has shipped here before and returned 500s to a user.
- **Any model change gets run against SQLEXPRESS.** Sqlite cannot see multiple cascade paths
  (error 1785), decimal aggregate behaviour, or collation. Green Sqlite tests are not a passing
  grade for a schema change.
- **Any refusal gets triggered once.** A rule with no test proving it *refuses* is not covered,
  and a refusal the user sees in raw English is a bilingual defect.

## 5. Land — stage explicitly, describe the product

**Never `git add -A`.** Other sessions and processes write into this tree constantly; broad
stages have repeatedly swept another epic's half-finished work into a commit. Every time:

```bash
git status --short
```

Read it. Anything you did not write is context, not your change — including files under
`.claude/worktrees/`, another session's new controllers, and modified shared files
(`Startup.cs`, `AppDbContext.cs`, `ScreenCatalog.cs`) that may carry two epics at once. Diff a
shared file before staging it and keep only what is yours.

Stage named paths, never a directory sweep:

```bash
git add src/Sms.Domain/Library/Loan.cs src/Sms.Infrastructure/Library/LibraryAdmin.cs …
git status --short          # read it again: staged is exactly what you listed
```

`external/erp` is never part of an SMS commit — see `sms-erp`. `.gitignore` carries a
VS-boilerplate `Backup*/` rule that silently swallows first-party folders named `Backup`; if your
change adds one, confirm it actually appears in `git status --short`.

### The commit message

Subjects here describe the change **in the product**, as a plain sentence — never
`feat(scope):`. Read `git log --oneline -20` and match it. The log's own examples:

```
Enforce screen permissions — the finance screens were open to anyone
G-2: a cafeteria item could not express VAT at all
/subjects/plan threw the moment a subject was retired
```

The body says what was wrong, what the change does, and what it deliberately did not do. Name
the doc section and the `BR-` ids. State any deviation from the docs here as well as in the code.

Commit and push only when the user asks. If you are on `main` and the work is substantial, ask
before committing rather than assuming.

---

## Definition of done

A task is done when **all** of these are true. Report honestly against them; a partial result
stated plainly is worth more than a confident one that is wrong.

- [ ] The module doc's requirement is met, or the gap is stated in the code, the commit and the reply
- [ ] `dotnet build --configuration Release` — no errors, no warnings
- [ ] `dotnet test --configuration Release` — green, including the architecture tests
- [ ] New rules carry tests tagged `[BusinessRule("BR-…")]`, refusals covered, not only the happy path
- [ ] Anything user-visible has been rendered in Arabic **and** English, and a write actually persisted
- [ ] A model change has a migration named `yyyyMMdd_Desc` and has been run against SQLEXPRESS
- [ ] Every new action declares `[RequirePermission]` or `[NoPermissionRequired("reason")]`
- [ ] `git status --short` shows exactly your files staged, and `git -C external/erp status --porcelain` is empty
- [ ] The reply says what was built, what was skipped, and what was verified how — not "should work"

## What ends a task early

Stop and ask, rather than guessing, when:

- two readings of the requirement produce materially different work;
- the doc conflicts with the code and both look deliberate;
- the change would need an owner decision already listed as pending in `docs/Status/`
  (the PDF engine, the email/SMS provider, the country pack's data);
- the work would modify `external/erp`, or move the submodule pointer.

Everything that does **not** depend on the answer gets built first. A blocked question is not a
reason to deliver nothing.
