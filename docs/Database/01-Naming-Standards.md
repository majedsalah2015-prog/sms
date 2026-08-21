# Database 01 — Naming & Modeling Standards

**Phase:** 10 | **Status:** Draft for review | **Owner:** Database Architect

> Binding conventions for every table, column, key, and index. EF Core migrations are the schema source of truth (T-2); these standards are enforced by code review + a migration lint check at implementation.

---

## 1. Platform decisions

| # | Decision |
|---|----------|
| DB-1 | SQL Server 2019+ (aligns .NET 8 / EF Core 8). |
| DB-2 | **Database collation: `Arabic_100_CI_AS_SC`** with all textual columns `NVARCHAR` (Unicode always — bilingual data everywhere per ADR-5). Case-insensitive, accent-sensitive, supplementary characters enabled. UTF-8 VARCHAR option rejected: NVARCHAR is EF-default, uniform, and avoids mixed-collation risk. |
| DB-3 | Dates: `DATE` for calendar dates; `DATETIME2(3)` for timestamps, **always UTC** (ADR-4); no `DATETIMEOFFSET` (TZ resolved in domain layer via school config). |
| DB-4 | Money: `DECIMAL(18,4)` storage, rounding in domain per BR-GLB-060; never FLOAT. |
| DB-5 | Primary keys: `INT IDENTITY` default; `BIGINT IDENTITY` for high-volume streams (audit, attendance-period, notifications delivery, wallet ledger, sessions). GUIDs only as **secondary** public references (certificate verification codes, payment intents) — never clustered keys. |
| DB-6 | Soft delete via `Status`/`IsActive` per ADR-7 — **no `IsDeleted` anti-pattern rows removal**; global query filters in EF enforce tenancy + active-scoping centrally (BR-GLB-010). |
| DB-7 | Concurrency: `ROWVERSION` column on all user-editable tables. |

## 2. Schemas (module clusters)

| Schema | Contents | Modules |
|--------|----------|---------|
| `core` | School, settings, lookups, academic years/calendar, grades/sections/subjects/rooms | 01–08 |
| `ppl` | Students, parents, guardians, employees, teachers, admissions | 09–13 |
| `acad` | Attendance, timetable, exams, grading, certificates | 14–18 |
| `fin` | Fees, installments, payments, discounts, payers | 19–22 |
| `svc` | Transport, health, discipline, library, cafeteria, store, activities | 23–29 |
| `msg` | Messaging, notifications (templates, deliveries) | 32–33, doc 09 |
| `sec` | Identity, roles, permissions, scopes, sessions | doc 06, 36 |
| `aud` | Audit stores, integrity checkpoints, anomaly | doc 07, 34 |
| `ops` | Jobs, imports, backup metadata, report registry/executions, dashboards | 30–31, 35–36 |
| `doc` | Attachments metadata, document types, checklists | doc 10 |

## 3. Naming rules

| Element | Rule | Example |
|---------|------|---------|
| Table | PascalCase, **singular**, schema-qualified | `ppl.Student`, `fin.Receipt` |
| Column | PascalCase; no table-name prefix | `StartDate`, not `StudentStartDate` |
| PK | `Id` | `ppl.Student.Id` |
| FK column | `<Entity>Id`; role-qualified when multiple | `SchoolId`, `ApprovedByUserId` |
| Bilingual pair | `NameAr` + `NameEn` (mandatory pair, BR-GLB-001); same for `TitleAr/En`, `DescriptionAr/En` | `core.Subject.NameAr` |
| Boolean | `Is/Has/Can` prefix | `IsGraduating`, `HasConsent` |
| Enum/status | `Status` (+ lookup or constrained smallint w/ enum in domain) | `Application.Status` |
| Dates | `<What>Date` / `<What>AtUtc` | `DueDate`, `PostedAtUtc` |
| FK constraint | `FK_<Table>_<RefTable>[_<Role>]` | `FK_Receipt_Payer` |
| Unique | `UQ_<Table>_<Cols>` | `UQ_Section_Grade_Name` |
| Check | `CK_<Table>_<Rule>` | `CK_Room_ExamCapacity` |
| Index | `IX_<Table>_<Cols>[_INC]` | `IX_Charge_StudentId_Status` |
| Default | `DF_<Table>_<Col>` | `DF_Student_Status` |

No Hungarian prefixes (`tbl`, `vw_` exception below), no abbreviations except the approved list: `Utc`, `Id`, `No` (official numbers), `Pct`, `Qty`, `Amt` (avoid where full word is short).

Views: `vw_<Purpose>` in owning schema (read models, §5 of doc 04). Stored procedures avoided (EF-first); the few allowed (strict-sequence issuance, integrity checkpoint) named `usp_<Action><Entity>`.

## 4. Standard column sets

**Every tenant table** (ADR-2): `SchoolId INT NOT NULL` → `core.School`.
**Every transactional table** (ADR-3): `AcademicYearId INT NOT NULL` → `core.AcademicYear`.
**Every table** (BR-GLB-007): `CreatedByUserId`, `CreatedAtUtc`, `ModifiedByUserId NULL`, `ModifiedAtUtc NULL`.
**User-editable**: `RowVersion ROWVERSION`.
**Master data**: `IsActive BIT DF 1` (+ `DisplayOrder` where ordered).
**Official documents** (BR-GLB-040s): `DocumentNo NVARCHAR(30) NOT NULL` + `UQ` per series scope; `Status` incl. `Void`; immutability enforced in domain + update-trigger guard on posted rows (defense in depth for BR-GLB-062).

## 5. EF Core mapping standards

- Configurations per entity class (no data annotations for schema concerns); table/schema explicit.
- Global query filters: `SchoolId` (tenant context) on all tenant entities; soft-active filter opt-in per entity.
- Enums mapped to `SMALLINT` with domain enum as truth; lookup-backed codes use FK to `core.LookupValue` when school-extensible (BR-SET-001 tier decides: fixed → enum, extensible → lookup FK).
- Owned types: `Money` (Amount + implicit school currency), bilingual name pairs as owned `LocalizedName`.
- Migrations: one per change-set, named `yyyyMMdd_<Description>`; no manual schema drift ever.

## 6. Data protection & encryption

- TDE at rest (NF-S/BR-SEC-024); backup encryption per BR-BAK-007.
- Column-level protection (Always Encrypted or app-layer) evaluated for: salary fields (`ppl.Contract`), provider credentials (`msg.Provider`) — decision at implementation with ops input; documented requirement here.
- Restricted-category tables (medical, discipline, custody, hardship) carry no special storage but are permission- and audit-bound (docs 06/07); they are **flagged in the table inventory** (doc 03) for implementation review.

## 7. Reference integrity policy

- FKs mandatory everywhere (no orphan tolerance); `ON DELETE NO ACTION` universally (soft delete philosophy — nothing cascades); the only physical deletes are retention purges via certified jobs (BR-SYS-005) using controlled procedures.
- Cross-schema FKs allowed (they express the module dependency map already approved in module docs).
- Lookup FKs always to `core.LookupValue` with `LookupCategoryId` check enforced in domain.
