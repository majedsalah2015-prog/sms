# Database

**Phase 10 deliverables (Checkpoint 10)** — unlocked after all 36 module analyses were approved (engagement rule honored).

| Document | Content | Status |
|----------|---------|--------|
| [01-Naming-Standards.md](01-Naming-Standards.md) | Platform decisions (collation, types, keys), schemas, naming rules, standard column sets, EF Core mapping standards, integrity policy | ✅ Draft for review |
| [02-ER-Model.md](02-ER-Model.md) | Core patterns + ER diagrams per cluster (core, people, academic, finance, services, platform), dependency map | ✅ Draft for review |
| [03-Table-Specifications.md](03-Table-Specifications.md) | Column-level specs for the 12 pivotal tables + full ~190-table inventory by schema with restricted-category flags | ✅ Draft for review |
| [04-Indexes-Constraints-Performance.md](04-Indexes-Constraints-Performance.md) | Indexing strategy & prescriptions, constraint catalog, strict-numbering implementation, read models for the report catalog, volume/partitioning plan, performance gates | ✅ Draft for review |

Change control: schema changes require the module doc's Database Concept and the doc 03 inventory updated first; EF migrations then implement.
