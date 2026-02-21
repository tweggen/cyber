# Cyber Project Documentation

Complete documentation for the Cyber knowledge exchange platform.

## Quick Navigation

### 📖 Getting Started
- **[SETUP.md](SETUP.md)** — Installation, development setup, build & test commands
- **[ARCHITECTURE.md](ARCHITECTURE.md)** — System overview and design principles

### 📐 Architecture & Design
See `architecture/` directory for detailed design documents:
- **00-OVERVIEW.md** — System architecture, design principles, layer breakdown
- **01-CLAIM-REPRESENTATION.md** — Claim data model, fragmentation, artifact hierarchy
- **02-ENTROPY-AND-FRICTION.md** — Semantic comparison model, integration cost
- **03-SERVER-ENHANCEMENTS.md** — New server APIs: batch write, filtered browse, search, job queue
- **04-ROBOT-WORKERS.md** — Stateless cheap-LLM workers: job types, interface, scaling
- **05-INGEST-PIPELINE.md** — End-to-end flow for bulk content ingestion
- **06-MIGRATION.md** — How to evolve from v1 to v2 without breaking existing entries
- **07-NORMALIZATION.md** — Content normalization: server-side format conversion
- **08-SECURITY-MODEL.md** — Authorization, clearances, compartments, access control
- **09-PHASE-HUSH.md** — Implementation phase details
- **10-USER-FACING-FEATURES.md** — Complete feature inventory with implementation status (13/16 = 81%)
- **11-CLASSIFIED-INTERACTION-CONCEPT.md** — Classification levels and interaction models
- **12-SUBSCRIPTION-ARCHITECTURE.md** — Cross-notebook mirroring and sync mechanisms

### 🗂️ Roadmap (Kanban Board)
See `roadmap/` directory for implementation planning:
- **[README.md](roadmap/README.md)** — Kanban board index (Proposed / Planned / Done)
- **proposed/** — Future feature ideas being evaluated
- **planned/** — Approved features ready for implementation
- **done/** — Completed implementation milestones

### 📚 Key Links

**Developer Guidance:**
- Root [CLAUDE.md](../CLAUDE.md) — AI-friendly developer instructions

**Feature Status:**
- [10-USER-FACING-FEATURES.md](architecture/10-USER-FACING-FEATURES.md) — Feature matrix with implementation status

**Backend Documentation:**
- [backend/README.md](../backend/README.md) — Current (.NET) backend documentation

**Legacy Backend (Reference):**
- [legacy/notebook/README.md](../legacy/notebook/README.md) — Rust v1 documentation (reference only)

---

Last updated: February 2026
