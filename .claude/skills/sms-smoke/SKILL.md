---
name: sms-smoke
description: Run Sms.Web locally against SQLEXPRESS and drive it for a real check — sign in, load a screen in Arabic and English, post a form, read the flash message. Use when asked to run the app, verify a screen actually works, reproduce a bug in the running product, or confirm a model change survives SQL Server rather than only Sqlite.
user-invocable: true
---

# Running and driving the app

Green Sqlite tests are not proof. Multiple cascade paths, decimal aggregates, collation, missing
views, and unregistered services all pass the test suite and fail the product. Anything
user-visible gets loaded for real before it is called done.

## Start it

```bash
ConnectionStrings__Sms="Server=.\SQLEXPRESS;Database=Sms;Trusted_Connection=True;MultipleActiveResultSets=true" ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --project src/Sms.Web --urls http://localhost:5099
```

Prefer `preview_start` with the `sms-web` configuration in `.claude/launch.json` over a raw shell
run — never leave a dev server running under Bash.

- SQL Server exists here **only** as the named instance `.\SQLEXPRESS`. `appsettings.json` says
  `Server=.`; override with the env var, do not edit the file.
- Development applies pending migrations and runs the embedded ERP seeders on start, so a cold
  start on an empty database works. Allow ~45 s; Hangfire retries its schema bootstrap a few times
  before the host listens.
- **Another Claude session may already own a port and the same database.** Check
  `netstat -ano | grep :5099` before assuming a port is free, and kill only your own PID. Rows
  appearing and vanishing under you is that session, not a bug — create the data your check needs
  through your own screens rather than trusting demo rows.
- `Sms.ArchitectureTests` references `Sms.Web`, so it cannot rebuild while an instance is running
  from `src/Sms.Web/bin`. Either stop yours, or build to a private directory:
  `dotnet build src/Sms.Web -o <dir>` then
  `dotnet <dir>/Sms.Web.dll --urls http://localhost:5010 --contentRoot src/Sms.Web`.
- **Views compile at build.** A `.cshtml` edit is invisible until `dotnet build` + restart.

## Sign in

`admin` — staff, holds SYSADMIN. `parent` and `student` — portal accounts; `/` redirects them to
`/portal` and staff URLs answer 404 by design (BR-SEC-010).

One-time passwords are constants in `SysAdminAccountSeedContributor` and
`PortalDemoAccountSeedContributor`; both seeders are idempotent on username, so re-running them
never resets anything. First login forces a change (BR-SEC-005). There is still no
reset-password screen — if a password is unknown, call
`IAuthenticationService.SetTemporaryPasswordAsync(accountId, value)` from a scratchpad console
tool (mirror the DI in `tools/Sms.Seeder/Program.cs`; it needs `<RollForward>LatestMajor</RollForward>`
because no .NET 5 shared runtime is installed).

## Drive it with the browser tools

`preview_start`, then `read_page` for structure and text, `computer`/`form_input` for
interaction, `read_console_messages` and `preview_logs` for errors. Prefer these over curl —
they see the rendered result and the console.

Check both directions. The language switch is a culture cookie:

```
.AspNetCore.Culture=c%3Dar-SA%7Cuic%3Dar-SA
```

In Arabic, confirm: `dir="rtl"` on `<html>`, no raw enum names, no untranslated English error
text, money still LTR-digit and right-aligned, dates Gregorian (Arabic must not silently switch
the calendar to Hijri).

## Driving it with curl, when you must

```bash
MSYS_NO_PATHCONV=1 curl -s -c jar -b jar http://localhost:5099/grading -o page.html
```

- `MSYS_NO_PATHCONV=1` stops Git Bash rewriting `/grading` into a Windows path. With it set,
  `-o /tmp/x` writes to `C:\tmp` — use relative output paths.
- Every POST needs the antiforgery token: GET the form first, pull
  `__RequestVerificationToken" type="hidden" value="[^"]*`, post it back with the cookie jar.
- `TempData["Flash"]` is consumed on first read — fetch the redirect target exactly once.

## What a real check looks like

1. The screen renders, HTTP 200, no console errors, no exception page.
2. Both languages render, and the Arabic one has no leaked English.
3. A write actually persists — post the form, follow the redirect, read the flash, reload and see
   the row.
4. A refusal actually refuses — trigger the rule the screen enforces and confirm the translated
   message, not a 500.
5. For a permission change: sign in as a role that lacks it and confirm the screen 404s and its
   sidebar/launcher entry is gone.

Report what you observed, including anything that failed. A screenshot or the page text is the
evidence; "should work" is not.

## Other surfaces

- `/hangfire` — recurring jobs dashboard. Hangfire 1.7 enqueues every missed occurrence on
  startup, so a long-stopped host can fire a burst of runs at once; that is expected behaviour,
  guarded by the `UX_JobRun_InFlight` filtered unique index.
- The embedded ERP's screens live under MVC areas and load `erp-theme.css`; the school's own
  screens use no area at all. If a school screen suddenly looks like the ERP, an area token leaked
  into its route.
