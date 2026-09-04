# `mobile/` — the parent and student portal, on a phone

A Flutter client for the half of `/api/v1` that a family reaches. It is a second
*view* over screens this product already has, never a second product: the server
owns every rule, every permission and every refusal, and this app shows what it
answers.

**Status:** built 2026-09-02. Like the API it talks to, it is **not part of
approved Analysis v1.0** — the docs put native mobile apps in `Future/` (GAP
register **G5**, roadmap **R2**, "parent first, teacher second"), so no module
doc specifies a screen here and no `BR-` rule governs one. The rules it *obeys*
are the ones the endpoints already enforce.

---

## What it covers

Everything the portal's own Razor screens show, and nothing else:

| Screen | Endpoint | Permission |
|---|---|---|
| Sign in, TOTP, forced password change | `POST /auth/login`, `/two-factor`, `/change-password` | anonymous / stated |
| The family | `GET /portal/children` | `POR/Home/View` |
| Attendance, results, timetable | `GET /portal/students/{id}/…` | `POR/Child/View` |
| Fees, family statement | `GET /portal/students/{id}/fees`, `GET /portal/statement` | `POR/Statement/View` |
| Homework | `GET /portal/students/{id}/homework` | `POR/Work/View` |
| Lessons and their material | `GET /portal/students/{id}/lessons`, `GET /portal/resources/{id}/file` | `POR/Lessons/View` |
| Announcements | `GET /portal/announcements` | `POR/Announcements/View` |

The menu is built from `GET /auth/me`'s permission list — the server's own
evaluation by the same `IPermissionService` the endpoints guard with. Hiding a
tab is therefore not the security decision; the endpoint answering **404** is
(BR-SEC-010). Hiding it only stops a family tapping into a refusal.

## What it deliberately does not do

| Not here | Why |
|---|---|
| **The teacher role** | The API has no attendance capture, no mark entry and no teacher timetable. A teacher app that only listed lesson plans would be a demo. Building it means a backend slice first — its own task. |
| **Homework submission** | There is no submission entity in the domain (`docs/Integration/03-Mobile-API.md` §6). An upload button would promise a family something the school could never receive. |
| **Push notifications** | Needs a device registry and a provider decision, both pending in `docs/Status/`. Roadmap R2. |
| **Paying online** | The payment gateway is roadmap R1 and dormant in `BR-PAY-007`. |
| **Its own arithmetic** | `IStatementService` and `IFeeAdmin.ComputeStudentPositionAsync` are the single central computation BR-FEE-008 requires. A phone that added the children up itself is how a family and the accounts office begin disagreeing. |
| **A copy of the password policy** | The school owns it and the server answers `422 password_policy` with a sentence per broken rule. A client-side copy goes stale the day a school tightens it. |

## The rules it keeps

- **Bilingual, both halves, always.** `lib/l10n/strings.dart` is the table, and
  `test/strings_test.dart` reads its source and fails on an entry whose Arabic
  half is missing, identical to the English, or not actually Arabic. Direction
  follows the locale: choosing Arabic flips the tree to RTL the way
  `_Layout.cshtml` swaps in `bootstrap.rtl.min.css`.
- **A refusal is shown, not re-worded.** The API translates every refusal at the
  web boundary; this app displays `error.message` verbatim and branches only on
  `error.code`. It supplies its own words for exactly two cases — the network
  never arrived, and the answer was not JSON.
- **Money keeps Western digits and reads left-to-right in both languages**, so a
  figure a parent reads out matches the receipt. Dates are Gregorian always;
  only the month name follows the language.
- **The session token is a credential.** It lives in the platform keystore
  (`flutter_secure_storage`), never in shared preferences, and a token past
  BR-SEC-004's absolute ceiling is dropped at launch rather than discovered
  through a 401 mid-tap. Signing out ends the session *on the server*.
- **A lesson resource is fetched with the token, not linked.** The download
  endpoint is `[Authorize]`d, a browser sends no `Authorization` header, and a
  token in a URL would leak a live credential into history. BR-LRN-006's scan
  gate is re-applied at that call, so a withdrawn file refuses properly.

## Running it

### 1. Windows Defender must be told to leave Dart alone — once

Defender flags `dart.exe` as a generic threat (a long-standing false positive).
Until it is excluded, every `flutter` command fails with *"The system cannot
execute the specified program"*. In an **administrator** PowerShell:

```powershell
Add-MpPreference -ExclusionPath "C:\src\flutter"
```

### 2. Fill in the platform folders

`lib/` and `test/` are checked in; `android/` and `ios/` are generated, because
they are per-machine glue with a binary Gradle wrapper in them.

```bash
bash tool/scaffold.sh
```

It runs `flutter create` in a scratch directory and copies only the platform
folders across, so nothing written by hand is overwritten. It then patches the
Android manifest for `INTERNET` and for cleartext HTTP **to the development
hosts only** — a school's real deployment is HTTPS, and a blanket
`usesCleartextTraffic` would quietly permit plaintext there too.

### 3. Point it at a school and run

```bash
flutter pub get
flutter run
```

The address is on the sign-in screen under **School address**, and is remembered
between launches. The defaults that work:

| Running the app on | Address |
|---|---|
| Android emulator | `http://10.0.2.2:5099` (the emulator's alias for the host) |
| A real phone on the same Wi-Fi | `http://<the laptop's LAN IP>:5099` |
| A school deployment | its own `https://…` |

Start the server the way `sms/CLAUDE.md` says, and bind it to more than
loopback so a phone can reach it:

```bash
dotnet run --no-launch-profile --project src/Sms.Web --urls http://0.0.0.0:5099
```

Sign in with the portal demo accounts (`parent`, `student`) — the one-time
values are constants in `PortalDemoAccountSeedContributor`. A fresh database has
no accounts until `tools/Sms.Seeder` has been run against it.

### Tests

```bash
flutter test
```

Pure Dart and widget-free — no device or emulator needed.

## Handing it to a family

The portal serves the build itself, at **`/portal/app`** — a page with the version,
size and date, a download button, and the install steps in both languages. Drop
the release build into the folder `MobileApp:PackagePath` names
(`src/Sms.Web/App_Data/MobileApp` by default, which `.gitignore` already
excludes) and the page picks up the newest `.apk` on the next request. No
restart, no database row:

```bash
flutter build apk --release
cp build/app/outputs/flutter-apk/app-release.apk \
   ../src/Sms.Web/App_Data/MobileApp/sms-portal-1.0.0.apk
```

The version shown comes from the file name (`sms-portal-<version>.apk`); a file
named otherwise still downloads and simply shows no version. Until a build is
dropped there the page says so outright rather than offering a button that would
refuse.

The screen carries `[NoPermissionRequired]` rather than a `POR/*` gate, and that
is deliberate: the three audiences who need it — student, guardian, **teacher** —
hold no permission in common, so any `POR` gate would hide it from the staff
half, and a new `ScreenCatalog` entry would be 404 for everyone (system
administrator included) until `tools/Sms.Seeder` had been re-run on every
deployment. What is served is the school's own client software, not a record,
and the global `FallbackPolicy` still requires a signed-in user.

**A teacher who installs it today gets the family app.** There is no teacher role
in this build, so a staff sign-in reaches the home screen and is told the account
is not linked to any student. That is honest, but it is not a teacher app — see
the table above.
