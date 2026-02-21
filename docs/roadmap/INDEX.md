# Implementation Roadmap — Kanban Board

This directory tracks the implementation progress of Cyber features using a kanban-style board organized into three states: **Proposed**, **Planned**, and **Done**.

## 📊 Overview

```
PROPOSED (Ideas)  →  PLANNED (Approved)  →  DONE (Completed)
```

## ✅ DONE (Completed Implementations)

Fully implemented and deployed features. These were successfully completed with both backend and frontend work.

### Core Backend Infrastructure
- **01-SCHEMA-AND-TYPES.md** — Database schema, domain types, migrations
  - Entry types, Claim model, Job queue types, Fragment support
  - Status: ✅ Complete

- **02-BATCH-WRITE-AND-CLAIMS-API.md** — Batch entry operations
  - Batch entry creation with claims and source metadata
  - Claims storage and retrieval
  - Status: ✅ Complete (backend), UI not exposed

- **03-JOB-QUEUE.md** — Async job processing
  - Job types: DISTILL_CLAIMS, COMPARE_CLAIMS, CLASSIFY_TOPIC, EMBED_CLAIMS
  - Job queue API, auto-creation triggers
  - Status: ✅ Complete with UI dashboard

- **04-FILTERED-BROWSE-AND-SEARCH.md** — Advanced search & filtering
  - 11 filter parameters (topic_prefix, claims_status, integration_status, friction, author, sequence range, needs_review, limit, offset, fragment_of, etc.)
  - Full-text search with Tantivy
  - **Frontend Status:** ✅ Complete (Browse Filters UI implemented Feb 2026)
  - **Search Status:** ✅ Complete (Server Search UI implemented)
  - Status: ✅ Fully Complete

---

## 🔄 PLANNED (Ready for Implementation)

Approved features with implementation plans, ready to start work.

### Worker Infrastructure
- **05-ROBOT-WORKERS.md** — Stateless LLM worker processes
  - Claim distillation (extract key statements)
  - Claim comparison (entropy/friction scoring)
  - Topic classification
  - Status: 📋 Plan ready, implementation pending

### MCP Integration
- **06-MCP-UPDATES.md** — Claude Desktop MCP server integration
  - Expose new batch, search, and claims operations
  - Update MCP tool definitions
  - Status: 📋 Plan ready, implementation pending

---

## 💡 PROPOSED (Ideas Being Evaluated)

Features under consideration, not yet formally planned.

- **Semantic Search UI** — Frontend interface for vector-based similarity search
  - Requires backend EmbeddingService integration
  - Ollama-based embeddings
  - Status: 💭 Proposed (backend ready, UI pending)

- **Batch Entry Creation UI** — Web interface for multi-entry uploads
  - File upload support (CSV, JSON, markdown)
  - Metadata extraction and batch classification
  - Status: 💭 Proposed (backend ready, UI pending)

- **Notebook Classification at Creation** — Add classification levels to notebook creation form
  - Update CreateNotebookRequest in backend
  - Add classification UI controls
  - Status: 💭 Proposed (backend design pending)

- **Advanced Claim Management** — UI for claim viewing, merging, contradiction resolution
  - Claim detail views
  - Claim comparison visualization
  - Status: 💭 Proposed (backend ready, UI pending)

- **Semantic Clustering Visualization** — Visual representation of entry clusters by entropy
  - Graph visualization of claim relationships
  - Friction heatmaps
  - Status: 💭 Proposed (research phase)

---

## 📈 Progress Summary

| State | Count | Status |
|-------|:-----:|--------|
| ✅ Done | 4 | Complete implementations with deployed features |
| 🔄 Planned | 2 | Approved, ready to implement |
| 💡 Proposed | 5 | Ideas under evaluation |

**Overall Feature Coverage:** 13/16 domains fully implemented (81%)

---

## 🔗 Related Documents

**See also:**
- [../README.md](../README.md) — Documentation index
- [../ARCHITECTURE.md](../ARCHITECTURE.md) — System architecture
- [../architecture/10-USER-FACING-FEATURES.md](../architecture/10-USER-FACING-FEATURES.md) — Complete feature matrix
- [../../README.md](../../README.md) — Main project README

---

## How to Use This Board

**To work on a planned item:**
1. Read the plan document in `planned/`
2. Follow the step-by-step instructions
3. Once complete, move to `done/` and update this index
4. Update [10-USER-FACING-FEATURES.md](../architecture/10-USER-FACING-FEATURES.md)

**To propose a new feature:**
1. Create a document in `proposed/`
2. Include problem statement, proposed solution, dependencies
3. Link from this index
4. Discuss with team; move to `planned/` when approved

**To track progress:**
1. Check this index for current state
2. Review the detailed plans in each directory
3. Reference [10-USER-FACING-FEATURES.md](../architecture/10-USER-FACING-FEATURES.md) for UI coverage

---

**Last Updated:** February 2026
**Current Status:** 4 done, 2 planned, 5 proposed
