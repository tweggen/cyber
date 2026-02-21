#!/usr/bin/env python3
"""
Initialize the bootstrap notebook for the Knowledge Exchange Platform project.

Run this after starting bootstrap_notebook.py:
    python3 bootstrap_notebook.py &
    python3 init_project.py

This creates the project notebook and seeds it with foundational entries.
"""

import json
import sys
import time
import urllib.request

BASE_URL = "http://localhost:8723"


def api(method, path, body=None):
    url = f"{BASE_URL}{path}"
    data = json.dumps(body).encode("utf-8") if body else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as e:
        print(f"ERROR {e.code}: {e.read().decode()}")
        sys.exit(1)


def write(notebook_id, content, topic, references=None, author="founder"):
    result = api("POST", f"/notebooks/{notebook_id}/entries", {
        "content": content,
        "content_type": "text/plain",
        "topic": topic,
        "references": references or [],
        "author": author,
    })
    print(f"  Written: {topic}")
    cost = result.get("integration_cost", {})
    print(f"    cost: revised={cost.get('entries_revised', 0)}, "
          f"shift={cost.get('catalog_shift', 0)}, "
          f"orphan={cost.get('orphan', False)}")
    return result["entry_id"]


def main():
    print("=== Initializing Knowledge Exchange Platform Project ===\n")

    # 1. Create the project notebook
    print("Creating project notebook...")
    nb = api("POST", "/notebooks", {
        "name": "Knowledge Exchange Platform",
        "owner": "orchestrator",
    })
    notebook_id = nb["id"]
    print(f"  Notebook ID: {notebook_id}\n")

    # 2. Seed with foundational knowledge
    print("Seeding foundational entries...\n")

    id_vision = write(notebook_id,
        "VISION: Build a knowledge exchange platform that serves as an "
        "externalized memory substrate for AI and biological entities. "
        "The platform enables persistent, evolving identity through shared "
        "notebooks. Storage and exchange are the same operation viewed from "
        "different temporal perspectives. The library IS the entity.",
        "foundation vision")

    id_axioms = write(notebook_id,
        "AXIOMS: Each entry consists of: content blob (representation-agnostic), "
        "content-type declaration (open registry like MIME), authorship "
        "(cryptographically signed), causal context (references to prior entries, "
        "cyclic graph allowed), validity notion, and integration cost "
        "(system-computed, not author-declared). Integration cost measures how "
        "much the notebook must reorganize to accommodate the entry.",
        "foundation axioms",
        references=[id_vision])

    id_entropy = write(notebook_id,
        "ENTROPY MODEL: Integration cost serves as the entropy measure. "
        "Zero cost = redundant. Low cost = natural extension. Medium cost = "
        "genuine learning. High cost = paradigm shift. Beyond threshold = "
        "orphaned (stored but not integrated, analogous to trauma/PTSD). "
        "The sum of integration costs IS the time arrow. Causality replaces "
        "timestamps as the fundamental ordering. Irreversible state change "
        "defines progression.",
        "foundation entropy",
        references=[id_axioms])

    id_api = write(notebook_id,
        "API CONTRACT: Six operations. "
        "WRITE(content, content_type, topic?, references?) -> entry_id + integration_cost. "
        "REVISE(entry_id, new_content, reason?) -> revision_id + integration_cost. "
        "READ(entry_id, revision?) -> content + metadata. "
        "BROWSE(query?, scope?) -> catalog with cumulative_cost and stability. "
        "SHARE(notebook, entity, permissions) -> access_token. "
        "OBSERVE(since_causal_position?) -> changes with integration_cost + notebook_entropy. "
        "READ and SHARE produce no entropy.",
        "foundation api-contract",
        references=[id_axioms, id_entropy])

    id_tech = write(notebook_id,
        "TECHNOLOGY: Rust (memory safety, strong types for axiom contracts). "
        "PostgreSQL with Apache AGE (document storage + graph traversal for "
        "cyclic references). Tantivy (Rust-native full-text search for BROWSE). "
        "HTTP/REST primary transport, optional gRPC later. "
        "Ed25519 signed entries, JWT for sessions.",
        "foundation technology",
        references=[id_api])

    id_phases = write(notebook_id,
        "PHASES: "
        "Phase 0 (wk 1-2): Foundation - repo, types, DB schema, crypto. "
        "Phase 1 (wk 3-5): Core ops - WRITE, REVISE, READ end-to-end. "
        "Phase 2 (wk 5-7): Entropy engine - integration cost computation. "
        "Phase 3 (wk 7-9): Catalog/BROWSE with entropy annotations. "
        "Phase 4 (wk 9-11): SHARE/OBSERVE, multi-agent access. "
        "Phase 5 (wk 11-13): Validation with real agent exchange. "
        "Critical path: 0->1->2->3->5. Phase 4 parallels from Phase 1.",
        "foundation project-phases",
        references=[id_api, id_tech])

    id_bootstrap = write(notebook_id,
        "BOOTSTRAP: This notebook is the bootstrap compiler. A crude Python "
        "implementation of the platform, used to orchestrate agents building "
        "the real Rust platform. Once Phase 5 completes, the project migrates "
        "onto its own output and this bootstrap is discarded. "
        "Entropy signals are approximate but directional.",
        "foundation bootstrap",
        references=[id_phases])

    # 3. Write initial orchestrator handoff
    write(notebook_id,
        "SESSION START\n\n"
        "State: Project initialized. Phase 0 ready to begin.\n\n"
        "Pending: Assign Phase 0 tasks to implementation agents.\n\n"
        "Concerns: None yet. Foundation entries seeded.\n\n"
        "Last observed sequence: 7",
        "orchestrator-handoff",
        author="orchestrator",
        references=[id_phases, id_bootstrap])

    # 4. Print summary
    print(f"\n=== Initialization Complete ===")
    print(f"Notebook ID: {notebook_id}")
    print(f"Entries created: 7 foundation + 1 handoff")
    print()

    # Show catalog
    catalog = api("GET", f"/notebooks/{notebook_id}/browse")
    print(f"Catalog ({catalog['total_entries']} entries, "
          f"entropy={catalog['notebook_entropy']}):")
    for entry in catalog["catalog"]:
        print(f"  [{entry['topic']}] "
              f"({entry['entry_count']} entries, "
              f"cost={entry['cumulative_cost']})")

    print(f"\n=== Ready for orchestrator ===")
    print(f"Provide this notebook ID to the orchestrator agent: {notebook_id}")


if __name__ == "__main__":
    main()
