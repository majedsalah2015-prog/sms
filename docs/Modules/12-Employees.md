# Module 12 — Employees

**Phase:** 4 — People | **Status:** Draft for review | **Rule prefix:** `BR-EMP`

---

## 1. Purpose

The HR backbone: one employee file for all staff (teaching and administrative) covering identity, contracts, qualifications, training, staff attendance, and leave — producing clean **payroll-preparation exports** (scope decision Q7: preparation, not payroll processing).

## 2. Scope

**In:** employee master file, positions & org units, contracts (effective-dated, expiry-tracked), qualifications & training records, staff attendance (check-in/out or daily mark), leave types/balances/requests (WF-10), end-of-service offboarding, payroll-preparation export, staff documents (doc 10 catalog).
**Out:** payroll calculation/payslips/WPS (Future), recruitment (Future), appraisal (Future), teacher academic assignments (Module 13), user account permissions (Module 36 — HR events trigger account lifecycle per doc 06 §2).

## 3. Business rules

| ID | Rule |
|----|------|
| BR-EMP-001 | One employee = one permanent record + Employee No. (doc 08), across contract renewals and rehires (BR-GLB-002). Identity T1-audited. Employee ≠ user account, but offboarding auto-deactivates the account (doc 06 §2). |
| BR-EMP-002 | **Org structure:** departments/units (administrative tree, distinct from academic departments in Module 07 though linkable); each employee holds one primary position (job title lookup, bilingual) + optional secondary roles; reporting-line (manager) drives WF-10 approvals. |
| BR-EMP-003 | **Contracts** are effective-dated documents: type (full/part-time, term), dates, salary structure snapshot (basic + allowances — data for payroll prep 🔒), renewal linkage; contract expiry is tracked (BR-ATT-008) with configurable lead alerts; an employee without an active contract cannot hold teaching assignments (Module 13 validates). Salary data is restricted 🔒 (BR-GLB-072 extension — HR + Principal only). |
| BR-EMP-004 | **Qualifications** (degrees, certifications, licenses) are recorded with documents; teaching-relevant ones feed the subject qualification matrix (BR-SUB-006). **Training** records (courses, PD hours) accumulate per year (ministry PD-hour reporting per pack). |
| BR-EMP-005 | **Staff attendance:** daily presence per employee per working day (staff calendar audience, BR-CAL-001) — manual mark, bulk mark, or device import (interface-ready, Future hardware); late/early rules per school policy generate exceptions; attendance closes daily (corrections after closure = P2 with reason, mirrors WF-14). |
| BR-EMP-006 | **Leave:** types configurable (annual, sick, emergency, unpaid, Hajj, maternity/paternity… per country pack) with accrual/entitlement rules per contract type; requests via WF-10 (Manager → HR), balance-validated; sick leave beyond N days requires medical document; approved leave feeds attendance (leave days ≠ absence) and payroll prep (unpaid deductions). |
| BR-EMP-007 | **Payroll preparation export** per period: per employee — worked days, absence days, late instances, unpaid-leave days, approved allowance/deduction adjustments; export is versioned, locked at generation (regeneration = new version, both audited T1), format: file + API-ready per T-8 abstraction; the SMS never computes net salary. |
| BR-EMP-008 | **Offboarding:** resignation/termination workflow — notice tracking, clearance checklist (assets, library, finance advances, handovers), final-attendance cutoff, account deactivation, document retention per pack; end-of-service benefit *calculation* is out (payroll-side), but service-period certificate is issuable (Module 18 pattern). |
| BR-EMP-009 | Employee documents per doc 10 catalog (contract 🔒, ID/Iqama ⏰, work permit ⏰, qualifications, medical fitness 🔒); expiring-document console is an HR daily driver. |
| BR-EMP-010 | Headcount and cost-sensitive fields (salary structure) never appear in general reports; payroll-prep export permission is distinct and T0-audited. |

## 4. Workflow

WF-10 leave (P3: Manager → HR; sick > N days adds document gate). Offboarding (P3: Manager → HR → Principal) with clearance checklist (parallel, finance veto — mirrors WF-03). Contract renewal: HR drafts → Principal approves (P2) 🔒. Attendance correction post-closure: P2 (HR). All else direct entry audited.

## 5. User roles

HR Officer (owner), Principal (approvals, full view), Managers (team attendance/leave approvals), Employee (self-service: own file view, leave requests, payslip-prep data view excluded), Finance (payroll-prep export consumer), Sys Admin (org structure).

## 6. Permissions

| Action | Roles |
|--------|-------|
| View employee directory (basic card) | All staff |
| View full employee file | HR, Principal; Manager (team, non-salary) |
| Salary/contract data 🔒 | HR, Principal only |
| Edit employee/contracts | HR (contracts + P2) |
| Mark staff attendance | HR, delegated markers |
| Approve leave | Manager then HR (WF-10) |
| Generate payroll-prep export | HR + distinct permission (T0) |
| Offboarding | HR + chain |

## 7. Database concept

Entities: `Employee` (identity, number, status); `OrgUnit` / `Position`; `EmployeeAssignment` (position, unit, manager, effective dates); `Contract` (dates, type, salary structure 🔒, status); `Qualification` / `TrainingRecord`; `StaffAttendanceDay` (+ exception types); `LeaveType` / `LeaveEntitlement` / `LeaveRequest` (workflow-managed) / `LeaveBalance` (derived, materialized per year); `PayrollPrepExport` (+ lines, versioned); `OffboardingCase` (clearance items). Teacher-specific extensions live in Module 13 referencing Employee.

## 8. Required screens

1. Employee directory — cards/grid, org filters; basic contact card for all staff.
2. **Employee file** — tabs: Personal · Position & org · Contracts 🔒 · Qualifications · Training · Attendance · Leave (balances + history) · Documents · Audit.
3. Org chart viewer/editor.
4. Staff attendance console — daily grid (present/absent/late/leave), bulk actions, device-import placeholder, closure control.
5. Leave management — request form (self-service), approval inbox (doc 05 unified), balances dashboard, entitlement setup.
6. Contract manager 🔒 — renewals pipeline, expiry console.
7. Payroll-prep export — period picker, preview grid, generate/lock/version history 🔒.
8. Offboarding wizard — clearance board.

## 9. Validation rules

ID/Iqama mandatory + expiry per pack; contract dates non-overlapping per employee; leave request ≤ balance (type-configurable negative allowance), dates on working days, no overlap with existing leave/attendance; attendance only on staff working days; payroll-prep period must have attendance closed for all days; offboarding blocked with red clearance; manager cannot approve own leave (BR-WF-003). |

## 10. Reports

Staff register (ministry formats) · Contract expiry (30/60/90) 🔒 · Leave balances & consumption by type/unit · Staff attendance summary & exceptions (late patterns) · Qualifications matrix (feeds BR-SUB coverage) · Training/PD hours per year (ministry) · Turnover report (joins/leaves per period) · Payroll-prep register (versions, totals) 🔒 · Document expiry console report.

## 11. Dashboard widgets

HR: expiring contracts/documents, pending leave approvals, today's staff absence count, offboardings in progress. Principal: staff attendance % today, turnover trend. Manager: team leave calendar, pending approvals. Employee self-service: my balances, my requests status.

## 12. Notifications

`LeaveDecision` → employee; `LeaveRequestPending` → approver (SLA per doc 05); `ContractExpiring` → HR (+ employee per policy); `DocumentExpiring` → HR + employee; `AttendanceExceptionRecorded` (late/absent) → employee + manager (digest); `PayrollPrepGenerated` → Finance; `OffboardingStep` → stakeholders.

## 13. Future enhancements

Full payroll (GOSI/WPS, payslips) as an add-on module; recruitment pipeline; appraisal cycles (goals, observations — links Module 13 teaching load); biometric device integration (BR-EMP-005 interface); employee mobile self-service; org-budget headcount planning.

## 14. Open questions

1. Country-pack leave entitlements matrix (annual days, sick tiers at %, maternity…) — legal values needed per confirmed countries. |
2. Staff attendance granularity: daily mark (proposed v1 default) vs clock-times? Clock-times matter for hourly staff — confirm whether any target school pays hourly in v1. |
3. Do schools need substitute/cover **tracking for payroll** (extra-period allowances) in v1? Coordinates with Module 15 substitution — recommend yes as a counted export line. |
4. Payroll-prep export format: fixed product CSV + documented layout (proposed) or per-customer templates (Future)? |
