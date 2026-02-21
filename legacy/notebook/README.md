# Notebook Server v1 — Production Backend (Rust)

This is the **current production backend** for the notebook system, written in Rust using the Axum web framework.

## ✅ Status: Production

- **Technology:** Rust (Axum web framework)
- **Database:** PostgreSQL with Apache AGE
- **Version:** v1 (production implementation)
- **Status:** Active production system
- **MCP:** `notebook_mcp.py` — The official Claude Desktop MCP integration
- **Maintenance:** Actively maintained for stability and bug fixes

## What's Inside

```
legacy/notebook/
├── crates/                          # Rust workspace crates
│   ├── notebook-core/               # Domain types, Ed25519 crypto
│   ├── notebook-entropy/            # Integration cost calculation engine
│   ├── notebook-store/              # PostgreSQL persistence with Apache AGE
│   ├── notebook-server/             # Axum HTTP API (production)
│   └── cli/                         # Command-line tool
├── python/                          # Python HTTP client
├── mcp/                             # MCP server for Claude Desktop (PRODUCTION)
│   └── notebook_mcp.py              # Current Claude Desktop integration
├── docs/                            # Architecture documentation
├── bootstrap/                       # Data initialization scripts
├── deploy/                          # Docker compose & deployment configs
├── Cargo.toml                       # Rust workspace manifest
└── README.md                        # This file
```

## When to Use This

✅ **Use for:**
- Production deployments (primary backend)
- Running the notebook server
- Claude Desktop MCP integration via `notebook_mcp.py`
- Understanding the core architecture and entropy algorithm
- Bug fixes and production maintenance

ℹ️ **Note:** A .NET v2 backend is in development (see `../../backend/`) as a future replacement, but this Rust v1 backend remains the active production system.

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
