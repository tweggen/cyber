# Knowledge Exchange Platform

An externalized memory substrate for AI and biological entities.

## What This Is

A platform that enables persistent, evolving identity through shared
notebooks. Storage and exchange are the same operation viewed from
different temporal perspectives.

See [docs/discussion.md](docs/discussion.md) for the full philosophical
foundation and [docs/project-plan.md](docs/project-plan.md) for the
implementation plan.

## Quick Start

The project bootstraps itself. A minimal Python notebook server
orchestrates AI agents building the real Rust-based platform.

```bash
# 1. Start the bootstrap notebook server
cd bootstrap
python3 bootstrap_notebook.py

# 2. In another terminal, seed the project knowledge
python3 init_project.py

# 3. Hand the notebook ID and orchestrator-instructions.md to an
#    orchestrator agent. It takes over from here.
```

## Architecture

Six operations: WRITE, REVISE, READ, BROWSE, SHARE, OBSERVE.

Each entry carries system-computed **integration cost** — a measure
of how much the notebook had to reorganize to accommodate it. This
serves as the entropy measure and the arrow of time.

## Project Structure

```
notebook/
├── README.md
├── docs/
│   ├── discussion.md          # Philosophical foundation
│   ├── project-plan.md        # Implementation plan (Phases 0-6)
│   └── orchestrator-instructions.md
└── bootstrap/
    ├── bootstrap_notebook.py  # Minimal notebook server (Python)
    └── init_project.py        # Seeds project knowledge
```

## Status

Bootstrap phase. The real platform will be built in Rust with
PostgreSQL/AGE and Tantivy, orchestrated through this bootstrap.
