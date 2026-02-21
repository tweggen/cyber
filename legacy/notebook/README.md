# Notebook Server v1 — Legacy Backend (Reference Only)

This is the **original, first-generation backend** for the notebook system, written in Rust. It is **no longer actively developed** and is maintained here for **reference and historical purposes only**.

## ⚠️ Status: Legacy / Not Maintained

- **Technology:** Rust (Axum web framework)
- **Database:** PostgreSQL
- **Version:** v1 (original implementation)
- **Status:** Reference only — not used in production
- **Maintenance:** Minimal; kept for code archaeology and reference

## Why It's Here

The Rust v1 backend served as the original proof-of-concept implementation. It includes:
- Core domain types and cryptography
- Integration cost calculation engine
- Original six REST operations (WRITE, REVISE, READ, BROWSE, OBSERVE, SHARE)
- Python HTTP client
- Claude MCP server integration

The system has since been re-implemented in .NET v2 (see `../../backend/`) with:
- Enhanced scalability
- New features (batch operations, job queue, claims management)
- Better integration with the admin UI
- Production-grade infrastructure

## What's Inside

```
legacy/notebook/
├── crates/                          # Rust workspace crates
│   ├── notebook-core/               # Domain types, Ed25519 crypto
│   ├── notebook-entropy/            # Integration cost engine
│   ├── notebook-store/              # PostgreSQL persistence
│   ├── notebook-server/             # Axum HTTP API
│   └── cli/                         # Command-line tool
├── python/                          # Python HTTP client
├── mcp/                             # MCP server (Claude Desktop)
├── docs/                            # Architecture documentation
├── bootstrap/                       # Data initialization scripts
├── Cargo.toml                       # Rust workspace manifest
└── README.md                        # This file
```

## When to Use This

✅ **Use for:**
- Understanding the original architecture
- Reference implementation of domain types
- Learning from the entropy calculation algorithm
- Historical context for design decisions

❌ **Don't use for:**
- Active development
- Production deployments
- New feature implementation
- Bug fixes or updates

## Building (If Needed)

```bash
cd legacy/notebook

# Build
cargo build

# Run HTTP server (if testing)
cargo run --bin notebook-server

# Run tests
cargo test

# Format check
cargo fmt --check
```

## Architecture Overview

The Rust v1 system consisted of:

```
┌────────────────────────┐
│   MCP Server           │
│   (Python/Rust)        │
├────────────────────────┤
│   HTTP Server (Axum)   │
│   Port 3000            │
├────────────────────────┤
│   PostgreSQL           │
│   + Apache AGE         │
└────────────────────────┘
```

**Six Core Operations:**
1. WRITE — Create entry
2. REVISE — Update entry
3. READ — Retrieve entry
4. BROWSE — List entries
5. OBSERVE — Change feed
6. SHARE — Grant access

## Key Crates

| Crate | Purpose |
|-------|---------|
| `notebook-core` | Domain types (Entry, Claim, AuthorId, etc.) and Ed25519 crypto |
| `notebook-entropy` | Integration cost engine with semantic comparison |
| `notebook-store` | PostgreSQL persistence via SQLx and EF |
| `notebook-server` | Axum web server with REST endpoints |
| `cli` | Command-line interface (Clap-based) |

## Python Client

A pure Python HTTP client is available in `python/`:

```bash
cd legacy/notebook/python

# Install
pip install -e ".[dev]"

# Use
from notebook_client import NotebookClient
client = NotebookClient("http://localhost:3000", token="...")
```

## Documentation

Original architecture documentation is in `docs/`:
- `project-plan.md` — Original v1 specification
- `discussion.md` — Philosophical foundations
- Design and implementation notes

## Migration to v2

The .NET v2 backend (`../../backend/`) preserves all v1 operations while adding:
- Batch write API
- Enhanced filtering (11+ parameters)
- Full-text search
- Job queue for async processing
- Claims management
- Better scalability

See `../../docs/architecture/06-MIGRATION.md` for migration details.

## Current Production Backend

For active development and deployment, use:
→ **[../../backend/](../../backend/README.md)** — .NET v2 (Current)

## Integration with Current System

The frontend (`../../frontend/admin/`) and current backend (`../../backend/`) form the active system. This legacy code is kept for:
- Reference and documentation
- Code archaeology
- Retracing design decisions
- Understanding the entropy algorithm

## Questions?

For questions about:
- **Current system:** See `../../backend/README.md` or `../../README.md`
- **Architecture:** See `../../docs/ARCHITECTURE.md`
- **Features:** See `../../docs/architecture/10-USER-FACING-FEATURES.md`
- **Setup:** See `../../docs/SETUP.md`

---

**Status:** Legacy / Reference Only
**Last Updated:** February 2026
**Active Development:** See `../../backend/` instead
