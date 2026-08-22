---
name: sms-erp
description: Work that touches the embedded ERP — where an accounting fix belongs, how to change external/erp without breaking the read-only rule, how GL posting and ERP permissions/navigation cross the bridge, and how to move the submodule pointer. Use whenever a change involves external/erp, Sms.Erp.Bridge, the general ledger, ERP screens or ERP permissions, and before touching any file under external/erp.
user-invocable: true
---

# The embedded ERP boundary

`ERP2028` is a separate product hosted in-process here: **seven** of its modules — Organization,
Accounting, Inventory, Purchasing, Sales, Cash, Partners — registered and mounted as MVC areas
inside `Sms.Web`, sharing one database through their own schemas, each with its own
`__EFMigrationsHistory`. (`external/erp` also contains Communication, FixedAssets and Identity;
those are not mounted here. Mounting one is a `Startup.cs` + `Program.cs` change, in that order.)

The arrangement is only worth anything while **deleting `Sms.Erp.Bridge` leaves a standalone
school system**. Every rule below serves that one sentence.

## Where a change belongs

| The change is | It belongs in |
|---|---|
| an accounting rule, a posting behaviour, an ERP screen, an ERP enum | the **ERP repository** — never here |
| adapting this system to an ERP contract (posting a school batch, minting ERP claims) | `src/Sms.Erp.Bridge` |
| registering a module, migrating its context, mounting its area | `src/Sms.Web` — the composition root, the only project allowed the ERP's non-contract layers |
| a school-side rule that happens to produce money | `Sms.Application` / `Sms.Infrastructure`, with the posting handed to the bridge |

Enforced by `ErpBoundaryTests`:

- `Sms.Domain`, `Sms.Application`, `Sms.Infrastructure` must not name an `ERP2028` type at all.
- `Sms.Erp.Bridge` may see each module's `.Contracts` **and nothing else of it**.

And by CI, on every push: `git -C external/erp status --porcelain` must be empty.

## `external/erp` is read-only here

It is a submodule pinned to one commit. A fix made in this working copy would be invisible to the
ERP product and lost at the next submodule bump — and CI fails the build to make sure that is
never discovered later.

So when the right fix genuinely is inside the ERP, **do not edit it in place and leave it there**.
Move it into the ERP's own history and return this working copy to the pinned commit:

```bash
# 1. Preserve first, before anything can be lost.
git -C external/erp diff > <scratchpad>/<name>.patch

# 2. The submodule is a real clone of the ERP repository. Commit there, on a branch.
git -C external/erp checkout -b <topic-branch>
git -C external/erp add <the files you changed>          # explicit paths, as always
git -C external/erp commit -F <message-file>

# 3. Put this working copy back on the pinned commit, so the SMS tree is clean again.
git -C external/erp checkout main
git -C external/erp status --porcelain                   # empty — the CI gate passes
git submodule status                                     # no +/- prefix — the pointer matches
```

The branch now holds the work, the SMS tree is clean, and nothing is pending in a place that
forgets. Then push the branch to the ERP repository and open a PR **there** — that is an
outward-facing action, so ask the user first.

Two things that bite:

- The submodule clone has **no commit identity** of its own and inherits none, so
  `git -C external/erp commit` fails with "Author identity unknown". Copy the SMS repo's:
  `git -C external/erp config user.name "$(git config user.name)"` and the same for `user.email`.
- Writing the commit message needs a message file (`-F`), not a heredoc.

### Moving the pointer

Only after the ERP change is merged upstream:

```bash
git -C external/erp fetch origin && git -C external/erp checkout <new-sha>
git add external/erp                                     # the pointer only
```

That is its own SMS commit, whose message says which ERP change it brings and what school-side
behaviour now depends on it. Never bundle a pointer bump into a feature commit — a bisect that
lands on it needs to know the two are separable.

## What crosses the bridge

| Direction | Through |
|---|---|
| school journal batches → the general ledger | `IGlPostingPort` / `ErpGlPostingAdapter`, and `IGlAccountProvisioner` for the accounts they need |
| this system's user, clock and directory → the ERP | `ErpCurrentUserAdapter`, `ErpClockAdapter`, `ErpUserDirectoryAdapter` |
| ERP permissions → `sec.Permission` | `IExternalPermissionCatalog` / `ErpPermissionCatalog`, under the ERP's own reserved module code, carrying its own names verbatim |
| ERP screens → this system's sidebar | `Sms.Web/Navigation/ErpNavigationSource`, reading the ERP's own `INavigationProvider`s so a submodule bump brings new screens with it |

`ScreenCatalog` does **not** list ERP screens; they arrive through the external catalogue. Do not
add them by hand — a second source of truth over ~150 ERP screens goes stale silently.

## Migration order is fixed and matters

`Program.ApplyPendingMigrations` migrates the eight ERP contexts first, in a fixed order —
Organization leads (every `BranchCode` written elsewhere validates against `org.Branches`),
Accounting second (nothing can post to a ledger that does not exist), then the rest, then the
school's `AppDbContext`. A new ERP module goes into that list in dependency order, not at the end.

## GL correctness, when the task is a ledger gap

The open and closed gaps are tracked in `docs/Integration/01-Embedded-Accounting-Plan.md` as
`G-1..G-16`, with the closed ones marked. Read it before claiming a gap is unhandled.

Rules that have already been paid for once:

- **A void after its period was exported is a reversing entry in the current period**, never an
  edit to the exported one. Changing the past silently disagrees with a batch already shipped.
- **Tax is separated at the posting**, not folded into a gross amount, and never re-applied by a
  path that received a gross figure.
- **Money received that nothing has applied yet is an advance, not a settled receivable** — it
  stays visible until the ledger that owns the charge applies it.
- A balance that only balances in the test is not evidence: check the trial balance on SQLEXPRESS
  after the change (`sms-smoke`).
