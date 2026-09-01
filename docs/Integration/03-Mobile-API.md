# Mobile API — the school's app surface

**Status:** built 2026-08-31. **Not part of approved Analysis v1.0.**

The docs put native mobile apps in `Future/` — GAP register **G5**, roadmap **R2**, with the
portal PWA named as the R1 bridge — so no module doc specifies an API screen and no `BR-` rule
governs one. This layer was built on the owner's request and is documented here rather than in
`Modules/`, because inventing a numbered screen for it would put words into a closed
specification.

Everything below is a *second transport over the screens this product already has*. It is not a
second product: the same `ScreenCatalog` permissions guard it, the same `sec.UserSession`
authenticates it, the same ports do the work, and the same rule about translating a refusal
applies. Where an endpoint has no browser equivalent, its own XML summary says so.

---

## 1. Authentication

`sec.UserSession.SessionToken` has always been an opaque bearer token — the auth cookie carries
nothing else, and `IAuthenticationService.ValidateSessionAsync` is what decides on every request
whether it is still good. The app sends the same token:

```
Authorization: Bearer {sessionToken}
```

Scheme name: `Sms.Bearer` (`SessionTokenAuthenticationHandler`). This inherits, unchanged:

| Rule | Effect on the app |
|---|---|
| BR-SEC-002 | Lockout after repeated failures — `423 account_locked`, with the minutes in the message |
| BR-SEC-003 | TOTP, as a two-step sign-in (below) |
| BR-SEC-004 | Idle **and** absolute expiry, and revocation, take effect on the next call |
| BR-SEC-005 | A forced password change blocks every endpoint but two |

**The cost of this choice, stated plainly.** A session's absolute ceiling is the school's
`SessionPolicy` — 12 hours by default, never extended by activity — so a mobile user signs in
again roughly once a day. A longer-lived per-device refresh token would remove that; it is
deliberately **not built**, because it is a new credential with its own revocation surface and the
owner chose the existing session (decision, 2026-08-31).

### Sign-in

```
POST /api/v1/auth/login     { userName, password, deviceName? }
  → { token, expiresAtUtc, mustChangePassword }
  → { requiresTwoFactor: true, twoFactorToken }        // when BR-SEC-003 applies

POST /api/v1/auth/two-factor { twoFactorToken, code }
  → { token, expiresAtUtc, mustChangePassword }
```

`twoFactorToken` is a five-minute **data-protection** token carrying only the account id. It is
not a session and grants nothing. It exists because without it a caller who guessed an account id
could attack the second factor alone — which is exactly what the browser's short-lived second
cookie prevents.

`deviceName` is preferred over the HTTP `User-Agent` when the school reviews its session list: a
native client's agent string is usually a library's name and tells an administrator nothing.

### The rest

```
POST /api/v1/auth/change-password { currentPassword, newPassword }   → 204
POST /api/v1/auth/logout                                             → 204   (ends the session server-side)
GET  /api/v1/auth/me                                                 → profile + permissions + family
```

`GET /me` is the app's first call. It returns the account, the school, the working academic year,
the person behind the account (student / guardian / employee), the students this caller may read
(BR-SEC-011), and **every catalogued permission they hold** as `MODULE/Screen/Verb`. The app
builds its menu from that list instead of calling endpoints to see which ones 404 — it is
evaluated by the same `IPermissionService` the guards use, so the two cannot drift apart.

---

## 2. Language

Send `Accept-Language: ar-SA` or `en-US`. Request localization already reads it, so every
human-readable string and **every refusal** comes back in that language. Nothing else is needed.

Where a field is a stored bilingual pair the API returns **both** halves (`titleAr` / `titleEn`),
because that is data. Where a string is a *label the server chose* — a grade name, a section name,
a lookup value — it returns the one the caller asked for.

Money crosses the wire as a JSON number in invariant form and is never pre-formatted: BR-NUM-007's
separators and digit shapes are a display decision, and only the phone knows which locale it is
displaying in. `currency` travels with every amount.

Dates are Gregorian ISO-8601, always. Hijri display is the client's own decision (ADR-4).

---

## 3. Errors

One envelope, for every non-2xx answer, including the ones the framework raises on its own:

```json
{ "error": { "code": "installment_not_open", "message": "القسط غير مفتوح.", "fields": null } }
```

`code` is stable and language-independent — branch on it. `message` is already in the caller's
language — show it. `fields` is present only on `validation_failed`.

| Status | When |
|---|---|
| 400 | `validation_failed` — the request body did not bind or failed a field rule |
| 401 | Not signed in, bad credentials, bad TOTP code |
| 403 | `forbidden`, `must_change_password`, `cross_school_write`, `outside_teaching_reach` |
| **404** | Not found **or no permission** — see below |
| 409 | A business rule refused a state change |
| 422 | Well-formed input a rule rejected (amounts, policies, a missing audit reason) |
| 423 | `account_locked` |
| 503 | `ledger_not_attached` — accounting endpoints on a deployment without the ERP bridge |

**404 is deliberate for a missing permission.** BR-SEC-010: unauthorized surface disappears rather
than errors. A parent guessing a student id gets exactly the same body as for a student who does
not exist, and an API that answered differently would undo the rule for the one client that reads
status codes.

**A fault is never dressed up as a rule.** Every domain exception in this product derives from
`InvalidOperationException` — and so does *"Sequence contains no matching element"*, which is the
shape a genuine bug takes here. `ApiProblem` maps known types one by one and declines everything
else, so a broken endpoint becomes a logged 500 rather than a tidy 409 nobody investigates.

**The JSON reader's own English is replaced.** It runs before model binding, so none of Startup's
`ModelBindingMessageProvider` accessors covers it, and MVC treats an `InputFormatterException`
message as safe to show a client — so a body the parser could not read used to answer
`"The JSON value could not be converted to System.String. Path: $.nameAr | LineNumber: 0 |
BytePositionInLine: 13"`, in English, inside an otherwise translated envelope. These are told apart
by their key, which is the JSON path and therefore starts with `$`; a written rule keys on a CLR
property name and never does. The field keeps its name, loses the path prefix, and gets a
translated sentence. Found by smoke-testing the API in Arabic, 2026-08-31.

---

## 4. Lists

Every collection endpoint pages. `?page=1&pageSize=25`, `pageSize` capped at 200.

```json
{ "items": [...], "page": 1, "pageSize": 25, "total": 412, "totalPages": 17, "hasMore": true }
```

---

## 5. The endpoints

74 endpoints across seven controllers. Permissions are named as `MODULE/Screen/Verb`; all of them
already existed in `ScreenCatalog`, so **no re-run of `tools/Sms.Seeder` is required** before the
API works.

### `/api/v1/auth` — sign-in *(reachable by portal accounts)*

| | Permission |
|---|---|
| `POST login`, `POST two-factor` | anonymous |
| `POST change-password`, `POST logout`, `GET me` | none required, stated |

### `/api/v1/portal` — the family *(reachable by portal accounts)*

| | Permission |
|---|---|
| `GET children` | `POR/Home/View` |
| `GET students/{id}/attendance`, `/results`, `/timetable` | `POR/Child/View` |
| `GET students/{id}/fees`, `GET statement` | `POR/Statement/View` |
| `GET students/{id}/homework` | `POR/Work/View` |
| `GET students/{id}/lessons`, `GET resources/{id}/file` | `POR/Lessons/View` |
| `GET announcements` | `POR/Announcements/View` |

`GET children` carries this year's attendance percentage and fee balance on each row, so the app's
home screen needs one call. Each figure is asked for separately and may refuse on its own — a
guardian who may see the child but not the money is a real configuration, and the row still
appears.

### `/api/v1/learning` — e-learning, teacher side

| | Permission |
|---|---|
| `GET reach/offerings`, lesson reads | `LRN/Planner/View` |
| `POST lessons` / `PUT lessons/{id}` / `publish` / `retire` | `LRN/Planner/Create · Edit · Approve · Deactivate` |
| resource attach / withdraw / file | `LRN/Resources/Create · Deactivate · View` |
| homework reads and writes | `LRN/Homework/View · Create · Edit · Approve · Deactivate` |

Reach (BR-LRN-002) is resolved by the ports, not here; `hasSchoolWideReach` is always `false`,
exactly as in `LearningController`.

### `/api/v1/students` — the register

`STU/Directory/View · Create`, `STU/File/View · Edit · Approve`, `STU/Guardians/Edit · Deactivate`,
`STU/Enrollment/Create`.

The **social profile is deliberately absent**. BR-GLB-072 makes it a restricted category with a
screen permission of its own precisely so it can be withheld from roles that hold the rest of the
file; exposing it behind `STU/File/View` would hand it to everyone the browser withholds it from.
If the app needs it, it needs its own endpoint under `STU/SocialProfile`.

### `/api/v1/employees`, `/contracts`, `/payroll` — staff

`EMP/Directory/View · Create`, `EMP/File/View · Edit · Approve`, `EMP/Contracts/View · Create ·
Edit · Approve`, `EMP/Payroll/View`.

**Pay is a restricted category** (BR-EMP-003, BR-EMP-010) and stays behind its own permissions: the
file response carries no salary, and contracts, the register and payslips each sit behind the
permission the browser uses.

### `/api/v1/finance` — fees, instalments, the counter

`FEE/Categories/View`, `FEE/Structure/View`, `FEE/StudentFinance/View`, `FEE/Position/View`,
`FEE/Charges/View · Post · Deactivate`, `INS/Schedule/View`,
`PAY/Cashier/View · Create`, `PAY/Till/Create · Post`.

No balance is computed here. `IStatementService` and `IFeeAdmin.ComputeStudentPositionAsync` are
the single central computation BR-FEE-008 requires; a second arithmetic on a second transport is
how a phone and a printed statement start disagreeing about what a family owes.

`POST charges` re-applies the counter screen's duplicate guard ("this category is already charged
for the student this year"), which lives in the controller rather than in `IFeeAdmin` — an API that
skipped it would let a phone double-charge a family where the browser refuses.

### `/api/v1/accounting` — the ledger, read-only

`FEE/GlExport/View` for the chart, trial balance, account balances and entries;
`DSH/Statistics/View` for the revenue/expense result and monthly trend.

Read-only **by design, not as a first slice**: this system bills and collects, the accounting
product keeps the books, and the one write that crosses the line is the GL export batch, which
already has its own screen. A journal entry authored from the school's side belongs in the
accounting product's own screens.

The permissions are reused rather than new. Whoever may build and export the GL batch is exactly
who may read the ledger it lands in, and the result figures are the ones the statistics screen
already shows to the same audience. Reusing them also means no new catalogue entry and therefore no
seeder re-run — a new permission is 404 for everybody, system administrator included, until the
catalogue is seeded.

Reached through `Sms.Erp.Bridge` only, over `ILedgerAnalytics` and `IChartOfAccountsDirectory` —
both sanctioned ERP query contracts. `ErpBoundaryTests` still passes; nothing outside the bridge
names an ERP type.

On a deployment without the bridge every accounting endpoint answers **503 `ledger_not_attached`**.
It must never answer zero: *"the books are empty"* and *"nobody asked the books"* are different
statements and only one of them is ever true. `GET /accounting/status` lets the app hide the
section instead of showing one that 503s.

---

## 6. What is **not** built, and why

| Gap | Why |
|---|---|
| **Student homework submission** (`§8.10`) and **timed sittings** (`§8.11`) | There is no submission entity in the domain. `PortalSetWork` says so in as many words: *"Carries no submission and no mark. Both are later slices."* This is a gap in module 37, not in the API — building it means an entity, a migration, an engine and its tests, and it cannot be faked at the transport layer. |
| **Self-service payslip** ("my own payslip") | Would need a permission `ScreenCatalog` does not define. Inventing one on a second transport is a security decision made by accident. An employee reads their payslip today through a role holding `EMP/Payroll/View`, which is the school's whole staff-pay grant; narrowing it is a `ScreenCatalog` change and its own slice. |
| **File upload** (attachments, photos) | The API links an already-uploaded `doc.Attachment` (e.g. lesson resources) but does not accept bytes. An attachment carries a document type, a size limit and a virus scan; duplicating that intake pipeline for a second transport is how the two stop agreeing about what a valid file is. |
| **Push notifications** | Needs a device registry and a provider decision, both listed as pending in `docs/Status/`. |
| **Per-device refresh tokens** | Owner decision, 2026-08-31 — see §1. |
| **Journal posting from the app** | See §5, accounting. |
| **`PUT /students/{id}/residence`**, and the `typeAr`/`typeEn` on a portal lesson resource | Built, then removed before landing: both lean on work that is still in another branch's working tree (`IStudentAdmin.SetResidenceAsync`, `PortalLessonResource.Type*`). A commit that only compiles once somebody else's lands is a broken commit, so they come back as a two-line follow-up the day that slice does. |

---

## 7. OpenAPI

`Swashbuckle.AspNetCore` **6.4.0** — the last line that still ships a `net5.0` target; 7.x is
net6.0 and up, so the pin is the framework's rather than a preference.

Served at **`/api/docs`**, **Development only**. The document lists every endpoint, field and
refusal code in the product, which is a convenience on a developer's machine and a reconnaissance
aid on a school's public host. The document covers only `[ApiController]` types deriving from
`ApiControllerBase`; the MVC screens and the ERP areas are excluded.

---

## 8. Where the code lives

```
src/Sms.Web/Api/
  ApiControllerBase.cs        base: [ApiController] + bearer [Authorize] + the two filters, T(), paging
  ApiEnvelope.cs              ApiError / ApiPage<T> / ApiMoney / ApiPaging
  ApiProblem.cs               the refusal translation table + the raw JSON writer
  ApiFilters.cs               exception filter, status envelope, [PortalReachable], [PasswordChangeExempt]
  Auth/                       SessionTokenAuthenticationHandler + SessionTokenDefaults
  Controllers/                seven controllers
  Models/                     request and response DTOs
src/Sms.Web/Security/
  SessionPrincipalFactory.cs  shared by the cookie sign-in and the bearer handler
src/Sms.Application/GlExport/IGlLedgerInsight.cs
src/Sms.Erp.Bridge/GlPosting/ErpLedgerInsight.cs
```

Two existing global filters were made API-aware rather than duplicated:

- `RequirePasswordChangeFilter` answers an API caller with `403 must_change_password` instead of
  redirecting to an HTML form — a redirect is not something a phone can act on, and it reads as a
  server fault.
- `PortalAreaFilter` reads `[PortalReachable]` off the endpoint for API requests, instead of the
  controller-name list it uses for Razor screens. A name list is a security decision kept somewhere
  other than the code it governs.

`AccountController.SignInSessionAsync` was refactored onto `SessionPrincipalFactory` so the cookie
and the bearer token mint the identical principal. Two transports building a principal from two
copies of the same code is how one of them quietly loses a claim — and the ERP permission claims
are exactly the kind that would go missing silently, because an accounting screen with no claim
denies rather than errors.

## 9. Tests

- `ScreenPermissionTests` now walks `ControllerBase`, not `Controller`, and recognises
  `ActionResult<T>` (which implements `IConvertToActionResult`, not `IActionResult`). Without both
  changes **every API endpoint would have slipped past the deny-by-default gate** — the same
  failure that test exists to prevent, arriving through a second transport.
- `MobileApiSecurityTests` (22 tests) covers the portal/staff split, the password-change refusal
  shape, the error envelope, the bearer scheme, the JSON-reader translation above, and that each
  standing refusal really does say something different in Arabic and English.

## 10. What was verified, and how

Run against **SQL Server** (`.\SQLEXPRESS`, a private `SmsApi5911` database), migrated and seeded
from scratch, driven over HTTP in both languages:

| | |
|---|---|
| Sign-in, both accounts | token + `expiresAtUtc` returned; `mustChangePassword: true` on first login |
| BR-SEC-005 | `403 must_change_password`, translated, **not** a redirect to an HTML form |
| No token | `401 unauthenticated`, translated |
| Password policy | `422 password_policy` with four per-field Arabic reasons |
| `GET /auth/me` | school, guardian, child and exactly the six `POR/*` permissions the parent holds |
| `GET /portal/children` | real child, grade `الصف الثالث` / `Grade 3` by `Accept-Language`, 100% attendance, 12,000 SAR outstanding |
| BR-SEC-010 | parent calling `/api/v1/students` → `404 not_found`, translated — never 403 |
| `GET /students`, `/employees` | paged, org unit and position resolved in the caller's language |
| `GET /finance/students/1/statement` | gross / discounts / net separated, running balance, `SAR` |
| `GET /accounting/*` | bridge attached; ERP chart of accounts returned in Arabic; trial balance balanced |
| `POST /students/1/emergency-contacts` | wrote and persisted a record carrying Arabic text |
| Malformed body | translated, in both languages, with the parser's byte offset gone |
| `/api/docs` | OpenAPI document served (152 KB, 51 route templates); Swagger UI 200 |
