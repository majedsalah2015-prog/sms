# 00 — Project Vision

**Phase:** 1 — Foundation | **Status:** Draft for review | **Owner:** Chief Solution Architect + Senior Business Analyst

---

## 1. Vision statement

> A commercial, bilingual (Arabic/English), multi-academic-year School Management System that a private school can run its **entire administrative lifecycle** on — from admission application to graduation certificate, from fee invoice to final receipt — sold as a product to many schools, configurable without code changes, and architected from day one to grow into a multi-school platform.

## 2. Product positioning

| Dimension | Position |
|-----------|----------|
| Product type | Commercial off-the-shelf SIS/ERP for schools (not a bespoke build) |
| Primary market | Private K-12 schools, Gulf/MENA first (assumption — see README Q3) |
| School size | 100 – 5,000 students per school |
| Buyer | School owner / principal / operations director |
| Deployment | Cloud (preferred) or on-premise; multi-tenant-ready core |
| Licensing | Annual subscription per school (tiered by student count) — to be confirmed |
| Language | Full Arabic + English, full RTL, bilingual master data (every name stored in both languages) |

## 3. Problem statement

Private schools in the target market typically run on a patchwork of Excel sheets, a legacy desktop system, WhatsApp groups, and paper files. The consequences:

1. **Fee leakage** — no reliable link between enrolled students, fee schedules, discounts, and collections.
2. **No single student file** — academic, medical, behavioral, and financial history scattered or lost between years.
3. **Re-registration chaos** — each new academic year is rebuilt manually; history breaks.
4. **No auditability** — grade changes, discount grants, and refunds happen without trace.
5. **Weak parent communication** — absences, results, and dues reach parents late or never.
6. **Regulatory pressure** — ministry reporting, VAT/e-invoicing, and data-protection requirements are handled manually.

## 4. Stakeholders & personas

| Persona | Role in system | Primary goals |
|---------|----------------|---------------|
| School Owner / Board | Read-mostly | Financial health, enrollment trends, multi-school consolidation (future) |
| Principal | Approver, dashboards | Academic performance, discipline, staff oversight, exceptions/approvals |
| Vice Principal | Operations | Timetable, attendance follow-up, discipline workflow |
| Registrar | Heavy data entry | Admissions, student files, sections, certificates, transfers |
| Finance Officer / Cashier | Heavy transactional | Fee setup, invoicing, collections, discounts, refunds, reconciliation |
| HR Officer | Data entry | Employee files, contracts, leave, payroll preparation |
| Teacher | Daily user | Attendance, marks entry, behavior notes, timetable, messaging |
| Homeroom Teacher | Daily user | Section-level attendance, parent communication, report cards |
| Nurse | Specialized | Medical files, visits, vaccinations, alerts |
| Librarian / Store / Cafeteria staff | Specialized | Circulation, sales, stock |
| Transport Supervisor | Specialized | Routes, buses, stops, bus attendance |
| IT Administrator | Configuration | Users, roles, backup, audit, system setup |
| Parent | Portal user | Children's attendance, results, dues, payments, messages |
| Student | Portal user (upper grades) | Timetable, results, library, activities |
| Ministry / Auditor | External | Statutory reports, audit trail extracts |

## 5. Value proposition & differentiators

1. **True bilingual core** — not a translated UI: every entity (student, subject, grade, fee item) carries Arabic and English names; every document/report/certificate prints in either language. Many competitors bolt Arabic on.
2. **Academic-year-aware everything** — every transaction is scoped to an academic year; year-end rollover (promotion, re-registration, fee regeneration) is a first-class guided workflow, not a data migration.
3. **One student file** — a single electronic file aggregating personal, family, medical, transport, attendance, fees, documents, academic history, behavior, activities, and audit — permanent across years.
4. **Parents as first-class entities** — one parent record, many children, one login, one statement; no duplicate parent data (explicit deduplication rules).
5. **Enterprise controls in a school-sized product** — field-level audit trail, approval workflows (discounts, refunds, certificate issuance, grade changes), and permissions scoped by module/screen/action **and** by school, academic year, grade, and section.
6. **Configurable, not customized** — grading scales, fee structures, numbering formats, calendars (Gregorian + Hijri display), and certificate templates are configuration, so one codebase serves many schools.

## 6. Competitive landscape (to be deepened in Phase 12 GAP analysis)

| System | Relevance |
|--------|-----------|
| PowerSchool SIS | Global feature benchmark (attendance, gradebook, compliance) |
| Classter, Fedena/Uzity, openSIS | Mid-market SaaS benchmarks for module breadth |
| Classera, Skolera (MENA) | Regional benchmarks for Arabic/RTL and parent apps |
| Ministry systems (e.g., Noor in KSA) | Integration/reporting target, not a competitor |

Phase 12 will run a structured GAP analysis against at least three of these.

## 7. Scope boundaries

**v1 (this analysis):** the 36 listed modules, single school live, multi-school-ready data model, AR/EN, web responsive UI, parent/student web portal (recommended addition — README Q6).

**Explicitly future (`Future/`):** native mobile apps, full LMS (lessons, homework, online exams), full payroll & HR appraisal, multi-school consolidated operations, biometric/RFID attendance devices, online payment gateways (design-ready in v1), alumni management, HR recruitment.

**Out of scope:** general accounting (GL), inventory beyond store/cafeteria/library needs, hostel/boarding (candidate for Future).

## 8. Success criteria (product level)

| Metric | Target |
|--------|--------|
| Time to onboard a new school (setup → first invoice) | ≤ 10 working days |
| Year-end rollover of a 1,000-student school | ≤ 1 working day, wizard-driven |
| Fee collection visibility | Real-time aged-receivables per student/section/grade |
| Attendance capture | ≤ 2 minutes per section per period |
| Report card generation (full school) | Same day as marks approval |
| Zero unexplained data changes | 100% of sensitive changes in audit trail |

## 9. Key risks

| Risk | Mitigation |
|------|------------|
| Scope explosion (36 modules) | Phased analysis with gates; `Future/` backlog discipline |
| Regulatory variance across countries | Configuration-first design; country packs later |
| EOL technology (.NET 5) | Challenge raised — recommend .NET 8 LTS (README Q1) |
| Year-rollover complexity underestimated | Treated as a dedicated workflow in Academic Years module |
| Arabic/RTL treated as an afterthought | Bilingual data + RTL are Phase 1 architectural requirements |

## 10. Open questions

See consolidated list in [README §5](README.md) — Q1–Q10 all affect this vision.
