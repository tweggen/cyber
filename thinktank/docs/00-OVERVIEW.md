# Notebook Server v2 — Architecture Overview

## Purpose

This document set describes the architecture for scaling the notebook server from its current state (~40 entries, LLM-only access via MCP) to handling 100,000+ entries with a mix of bulk ingest, cheap automated processing, and targeted expensive LLM work.

## The Core Problem

The current notebook routes ALL operations through LLM instances via MCP. Every read, write, comparison, and navigation decision pays full LLM inference cost. This works at 40 entries. At 14,000 (e.g., a Confluence dump) it costs >$1,000 in token overhead for work that is 90% mechanical.

## Design Principle: Separate Mechanical Work from Judgment

| Work Type | Example | Processor | Cost |
|-----------|---------|-----------|------|
| Storage I/O | Write entry, retrieve entry | Server (native) | ~$0 |
| Normalization | HTML → markdown, strip markup | Server (on write) | ~$0 |
| Text extraction | Parse source files, extract metadata | Script (no LLM) | ~$0 |
| Claim distillation | Content → N top claims | Cheap LLM (Haiku) | ~$0.001/entry |
| Claim comparison | Entropy/friction scoring | Cheap LLM (Haiku) | ~$0.001/pair |
| Contradiction resolution | Analyze conflicting claims | Strong LLM (Sonnet/Opus) | ~$0.05/case |
| Synthesis/consolidation | Merge 50 entries into 1 | Strong LLM (Opus) | ~$0.10/consolidation |
| Interactive Q&A | Answer user questions | Strong LLM (Opus) | ~$0.05/query |

## Architecture Layers

```
┌─────────────────────────────────────────────────┐
│  Agents (Opus/Sonnet via MCP)                   │
│  - Interactive Q&A with human                   │
│  - Contradiction resolution                     │
│  - Synthesis and consolidation                  │
│  - Index quality review                         │
│  Triggered: by human, by scheduler, by friction │
├─────────────────────────────────────────────────┤
│  Robots (Haiku-class, stateless workers)        │
│  - Claim distillation                           │
│  - Claim comparison (entropy/friction)          │
│  - Topic classification                         │
│  Interface: pull job from server, push result   │
├─────────────────────────────────────────────────┤
│  Notebook Server (no LLM)                       │
│  - Content normalization on write               │
│    (HTML→markdown, passthrough text/plain)       │
│  - Storage (entries, claims, originals)         │
│  - Job queue (pending work for robots)          │
│  - Entropy/friction state                       │
│  - Filtered browse, full-text search            │
│  - Batch write API                              │
│  - Index maintenance triggers                   │
├─────────────────────────────────────────────────┤
│  Ingest Scripts (no LLM)                        │
│  - Metadata extraction (titles, paths, dates)   │
│  - Fragmentation (split markdown at headings)   │
│  - Batch upload to server (raw or normalized)   │
└─────────────────────────────────────────────────┘
```

## Document Index

| Document | Contents |
|----------|----------|
| `01-CLAIM-REPRESENTATION.md` | The fixed-size claim model, fragmentation, artifact hierarchy |
| `02-ENTROPY-AND-FRICTION.md` | Semantic comparison model, entropy as novelty, friction as contradiction |
| `03-SERVER-ENHANCEMENTS.md` | New server APIs: batch write, filtered browse, search, job queue |
| `04-ROBOT-WORKERS.md` | Stateless cheap-LLM workers: job types, interface, scaling |
| `05-INGEST-PIPELINE.md` | End-to-end flow for bulk content (e.g., 14K Confluence pages) |
| `06-MIGRATION.md` | How to evolve from current v1 to v2 without breaking existing entries |
| `07-NORMALIZATION.md` | Content normalization: server-side format conversion to markdown |

## Existing Context

The notebook server repo is at `C:\Users\timow\coding\github\cyber` in the `notebook/` subdirectory. The current MCP server is `notebook_mcp.py`. The server exposes six operations: WRITE, REVISE, READ, BROWSE, OBSERVE, SHARE. All existing operations must continue to work — v2 is additive.

The notebook already has entries documenting its philosophical foundations (Whitehead, Sorkin, Varela), a scaling requirements spec, and topic indices built by a Claude Code instance. These entries are in the notebook itself and can be read for deeper context if needed.
