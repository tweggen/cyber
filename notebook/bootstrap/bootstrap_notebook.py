#!/usr/bin/env python3
"""
Bootstrap Notebook - Minimal Knowledge Exchange Platform

This is the "assembly language" version of the notebook platform.
Its sole purpose is to be good enough to orchestrate AI agents
building the real Rust-based platform described in
knowledge-platform-project-plan.md

Once the real platform is running, this bootstrap is discarded.

Design principle: the minimum viable notebook that gives an
orchestrator agent shared memory with integration cost signals
across multiple implementation agent sessions.

Storage: flat files in a directory structure.
Entropy: computed by diffing file states.
API: HTTP server, six endpoints, no authentication (local only).
Catalog: auto-generated from file summaries.

Usage:
    python3 bootstrap_notebook.py [--port 8723] [--data ./notebook-data]

Then point the orchestrator and implementation agents at
    http://localhost:8723
"""

import os
import sys
import json
import time
import uuid
import hashlib
import difflib
import argparse
import threading
from pathlib import Path
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs
from datetime import datetime, timezone
from dataclasses import dataclass, field, asdict
from typing import Optional


# ---------------------------------------------------------------------------
# Core data types - direct mapping from the axioms
# ---------------------------------------------------------------------------

def _now_iso():
    return datetime.now(timezone.utc).isoformat()


def _uuid():
    return str(uuid.uuid4())


class NotebookStore:
    """
    Flat-file notebook storage.

    Structure:
        {data_dir}/
            notebooks/
                {notebook_id}/
                    meta.json           # notebook metadata
                    sequence.txt        # monotonic counter
                    entries/
                        {entry_id}.json # individual entries
                    catalog.json        # auto-generated catalog cache
                    coherence.json      # coherence state for entropy calc
    """

    def __init__(self, data_dir: str):
        self.data_dir = Path(data_dir)
        self.data_dir.mkdir(parents=True, exist_ok=True)
        self._locks = {}  # per-notebook locks
        self._global_lock = threading.Lock()

    def _notebook_dir(self, notebook_id: str) -> Path:
        return self.data_dir / "notebooks" / notebook_id

    def _entries_dir(self, notebook_id: str) -> Path:
        return self._notebook_dir(notebook_id) / "entries"

    def _get_lock(self, notebook_id: str) -> threading.Lock:
        with self._global_lock:
            if notebook_id not in self._locks:
                self._locks[notebook_id] = threading.Lock()
            return self._locks[notebook_id]

    # -- Notebook management --

    def create_notebook(self, name: str, owner: str = "system") -> dict:
        notebook_id = _uuid()
        nb_dir = self._notebook_dir(notebook_id)
        nb_dir.mkdir(parents=True)
        (nb_dir / "entries").mkdir()

        meta = {
            "id": notebook_id,
            "name": name,
            "owner": owner,
            "participants": [{"entity": owner, "read": True, "write": True}],
            "created": _now_iso(),
        }
        (nb_dir / "meta.json").write_text(json.dumps(meta, indent=2))
        (nb_dir / "sequence.txt").write_text("0")
        (nb_dir / "coherence.json").write_text(json.dumps({
            "clusters": {},
            "total_entries": 0,
            "total_entropy": 0.0,
        }))
        self._rebuild_catalog(notebook_id)
        return meta

    def list_notebooks(self) -> list:
        nb_base = self.data_dir / "notebooks"
        if not nb_base.exists():
            return []
        result = []
        for d in nb_base.iterdir():
            if d.is_dir() and (d / "meta.json").exists():
                meta = json.loads((d / "meta.json").read_text())
                result.append(meta)
        return result

    def get_notebook(self, notebook_id: str) -> Optional[dict]:
        meta_path = self._notebook_dir(notebook_id) / "meta.json"
        if not meta_path.exists():
            return None
        return json.loads(meta_path.read_text())

    # -- Sequence (causal position) --

    def _next_sequence(self, notebook_id: str) -> int:
        seq_path = self._notebook_dir(notebook_id) / "sequence.txt"
        seq = int(seq_path.read_text().strip()) + 1
        seq_path.write_text(str(seq))
        return seq

    # -- Entry operations --

    def _load_all_entries(self, notebook_id: str) -> list:
        entries_dir = self._entries_dir(notebook_id)
        entries = []
        if entries_dir.exists():
            for f in entries_dir.glob("*.json"):
                entries.append(json.loads(f.read_text()))
        return entries

    def _load_entry(self, notebook_id: str, entry_id: str) -> Optional[dict]:
        path = self._entries_dir(notebook_id) / f"{entry_id}.json"
        if not path.exists():
            return None
        return json.loads(path.read_text())

    def _save_entry(self, notebook_id: str, entry: dict):
        path = self._entries_dir(notebook_id) / f"{entry['id']}.json"
        path.write_text(json.dumps(entry, indent=2))

    # -- Integration cost computation (bootstrap version) --

    def _compute_integration_cost(self, notebook_id: str, entry: dict) -> dict:
        """
        Bootstrap entropy computation.

        This is deliberately crude. The real platform will use proper
        clustering and coherence modeling. This version uses:

        - Topic keyword overlap to estimate cluster disruption
        - Reference validity checking
        - Simple catalog diff for catalog_shift

        Good enough to give the orchestrator meaningful signals.
        Not good enough for production.
        """
        coherence_path = self._notebook_dir(notebook_id) / "coherence.json"
        coherence = json.loads(coherence_path.read_text())

        existing_entries = self._load_all_entries(notebook_id)
        new_content = entry.get("content", "")
        new_topic = entry.get("topic", "")
        new_references = set(entry.get("references", []))

        # -- entries_revised --
        entries_revised = 0
        new_words = set((new_topic + " " + new_content[:200]).lower().split())

        for existing in existing_entries:
            if existing["id"] == entry.get("revision_of"):
                entries_revised += 1
                continue
            existing_words = set(
                (existing.get("topic", "") + " " + existing.get("content", "")[:200])
                .lower().split()
            )
            overlap = len(new_words & existing_words)
            total = max(len(new_words | existing_words), 1)
            if overlap / total > 0.3:
                entries_revised += 1

        # -- references_broken --
        existing_ids = {e["id"] for e in existing_entries}
        references_broken = len(new_references - existing_ids)

        # -- catalog_shift --
        existing_topics = set()
        for e in existing_entries:
            existing_topics.update(e.get("topic", "").lower().split())
        new_topic_words = set(new_topic.lower().split()) if new_topic else set()
        novel_words = new_topic_words - existing_topics
        catalog_shift = len(novel_words) / max(len(new_topic_words), 1) if new_topic_words else 0.0

        # -- orphan --
        has_references = len(new_references & existing_ids) > 0
        has_topic_overlap = any(
            len(new_words & set(
                (e.get("topic", "") + " " + e.get("content", "")[:200]).lower().split()
            )) > 2
            for e in existing_entries
        ) if existing_entries else False

        orphan = not has_references and not has_topic_overlap and len(existing_entries) > 0

        cost = {
            "entries_revised": entries_revised,
            "references_broken": references_broken,
            "catalog_shift": round(catalog_shift, 4),
            "orphan": orphan,
        }

        # Update coherence state
        total_cost = entries_revised * 0.3 + references_broken * 0.5 + catalog_shift
        coherence["total_entries"] = len(existing_entries) + 1
        coherence["total_entropy"] = round(
            coherence.get("total_entropy", 0.0) + total_cost, 4
        )
        coherence_path.write_text(json.dumps(coherence, indent=2))

        return cost

    # -- Activity context --

    def _compute_activity_context(self, notebook_id: str, author: str) -> dict:
        entries = self._load_all_entries(notebook_id)
        recent_by_author = sum(1 for e in entries if e.get("author") == author)
        coherence = json.loads(
            (self._notebook_dir(notebook_id) / "coherence.json").read_text()
        )
        return {
            "entries_since_last_by_author": recent_by_author,
            "total_notebook_entries": len(entries),
            "recent_entropy": coherence.get("total_entropy", 0.0),
        }

    # -- WRITE --

    def write_entry(
        self,
        notebook_id: str,
        content: str,
        content_type: str = "text/plain",
        topic: str = "",
        references: list = None,
        author: str = "anonymous",
    ) -> dict:
        lock = self._get_lock(notebook_id)
        with lock:
            entry_id = _uuid()
            sequence = self._next_sequence(notebook_id)

            entry = {
                "id": entry_id,
                "content": content,
                "content_type": content_type,
                "topic": topic,
                "references": references or [],
                "revision_of": None,
                "author": author,
                "causal_position": {
                    "sequence": sequence,
                    "activity_context": self._compute_activity_context(
                        notebook_id, author
                    ),
                },
                "created": _now_iso(),
            }

            integration_cost = self._compute_integration_cost(notebook_id, entry)
            entry["integration_cost"] = integration_cost

            self._save_entry(notebook_id, entry)
            self._rebuild_catalog(notebook_id)

            return {
                "entry_id": entry_id,
                "causal_position": entry["causal_position"],
                "integration_cost": integration_cost,
            }

    # -- REVISE --

    def revise_entry(
        self,
        notebook_id: str,
        entry_id: str,
        new_content: str,
        reason: str = "",
        author: str = "anonymous",
    ) -> Optional[dict]:
        lock = self._get_lock(notebook_id)
        with lock:
            original = self._load_entry(notebook_id, entry_id)
            if original is None:
                return None

            revision_id = _uuid()
            sequence = self._next_sequence(notebook_id)

            revision = {
                "id": revision_id,
                "content": new_content,
                "content_type": original["content_type"],
                "topic": original["topic"],
                "references": original["references"],
                "revision_of": entry_id,
                "author": author,
                "revision_reason": reason,
                "causal_position": {
                    "sequence": sequence,
                    "activity_context": self._compute_activity_context(
                        notebook_id, author
                    ),
                },
                "created": _now_iso(),
            }

            integration_cost = self._compute_integration_cost(notebook_id, revision)
            revision["integration_cost"] = integration_cost

            self._save_entry(notebook_id, revision)
            self._rebuild_catalog(notebook_id)

            return {
                "revision_id": revision_id,
                "original_id": entry_id,
                "causal_position": revision["causal_position"],
                "integration_cost": integration_cost,
            }

    # -- READ --

    def read_entry(self, notebook_id: str, entry_id: str) -> Optional[dict]:
        entry = self._load_entry(notebook_id, entry_id)
        if entry is None:
            return None

        all_entries = self._load_all_entries(notebook_id)
        revisions = [
            e for e in all_entries if e.get("revision_of") == entry_id
        ]
        revisions.sort(key=lambda e: e["causal_position"]["sequence"])

        return {
            "entry": entry,
            "revisions": [{"id": r["id"], "sequence": r["causal_position"]["sequence"]} for r in revisions],
        }

    # -- BROWSE --

    def _rebuild_catalog(self, notebook_id: str):
        """Regenerate the catalog from current entries."""
        entries = self._load_all_entries(notebook_id)

        clusters = {}
        for entry in entries:
            topic = entry.get("topic", "(no topic)") or "(no topic)"
            if topic not in clusters:
                clusters[topic] = {
                    "topic": topic,
                    "entries": [],
                    "cumulative_cost": 0.0,
                    "latest_sequence": 0,
                }
            cluster = clusters[topic]
            cluster["entries"].append(entry["id"])
            cost = entry.get("integration_cost", {})
            cluster["cumulative_cost"] += (
                cost.get("entries_revised", 0) * 0.3
                + cost.get("references_broken", 0) * 0.5
                + cost.get("catalog_shift", 0.0)
            )
            seq = entry.get("causal_position", {}).get("sequence", 0)
            cluster["latest_sequence"] = max(cluster["latest_sequence"], seq)

        catalog_entries = []
        for topic, cluster in sorted(
            clusters.items(), key=lambda kv: -kv[1]["cumulative_cost"]
        ):
            latest_entry = None
            latest_seq = -1
            for eid in cluster["entries"]:
                e = self._load_entry(notebook_id, eid)
                if e:
                    seq = e.get("causal_position", {}).get("sequence", 0)
                    if seq > latest_seq:
                        latest_seq = seq
                        latest_entry = e

            summary = ""
            if latest_entry:
                content = latest_entry.get("content", "")
                summary = content[:150] + ("..." if len(content) > 150 else "")

            catalog_entries.append({
                "topic": topic,
                "summary": summary,
                "entry_count": len(cluster["entries"]),
                "cumulative_cost": round(cluster["cumulative_cost"], 4),
                "latest_sequence": cluster["latest_sequence"],
                "entry_ids": cluster["entries"],
            })

        coherence = json.loads(
            (self._notebook_dir(notebook_id) / "coherence.json").read_text()
        )

        catalog = {
            "catalog": catalog_entries,
            "notebook_entropy": coherence.get("total_entropy", 0.0),
            "total_entries": len(entries),
            "generated": _now_iso(),
        }

        (self._notebook_dir(notebook_id) / "catalog.json").write_text(
            json.dumps(catalog, indent=2)
        )

    def browse(
        self, notebook_id: str, query: str = "", max_entries: int = 50
    ) -> Optional[dict]:
        catalog_path = self._notebook_dir(notebook_id) / "catalog.json"
        if not catalog_path.exists():
            return None

        catalog = json.loads(catalog_path.read_text())

        if query:
            query_words = set(query.lower().split())
            filtered = []
            for entry in catalog["catalog"]:
                topic_words = set(entry["topic"].lower().split())
                summary_words = set(entry["summary"].lower().split())
                if query_words & (topic_words | summary_words):
                    filtered.append(entry)
            catalog["catalog"] = filtered

        catalog["catalog"] = catalog["catalog"][:max_entries]
        return catalog

    # -- SHARE --

    def share_notebook(
        self, notebook_id: str, entity: str, read: bool = True, write: bool = False
    ) -> Optional[dict]:
        meta_path = self._notebook_dir(notebook_id) / "meta.json"
        if not meta_path.exists():
            return None

        meta = json.loads(meta_path.read_text())

        for p in meta["participants"]:
            if p["entity"] == entity:
                p["read"] = read
                p["write"] = write
                meta_path.write_text(json.dumps(meta, indent=2))
                return {"status": "updated", "entity": entity}

        meta["participants"].append({"entity": entity, "read": read, "write": write})
        meta_path.write_text(json.dumps(meta, indent=2))
        return {"status": "shared", "entity": entity}

    # -- OBSERVE --

    def observe(
        self, notebook_id: str, since_sequence: int = 0
    ) -> Optional[dict]:
        entries = self._load_all_entries(notebook_id)
        changes = [
            {
                "entry_id": e["id"],
                "operation": "revise" if e.get("revision_of") else "write",
                "author": e.get("author", "unknown"),
                "topic": e.get("topic", ""),
                "integration_cost": e.get("integration_cost", {}),
                "causal_position": e.get("causal_position", {}),
            }
            for e in entries
            if e.get("causal_position", {}).get("sequence", 0) > since_sequence
        ]
        changes.sort(key=lambda c: c["causal_position"].get("sequence", 0))

        period_entropy = sum(
            c["integration_cost"].get("entries_revised", 0) * 0.3
            + c["integration_cost"].get("references_broken", 0) * 0.5
            + c["integration_cost"].get("catalog_shift", 0.0)
            for c in changes
        )

        return {
            "changes": changes,
            "notebook_entropy": round(period_entropy, 4),
            "since_sequence": since_sequence,
        }


# ---------------------------------------------------------------------------
# HTTP API Server
# ---------------------------------------------------------------------------

class NotebookHTTPHandler(BaseHTTPRequestHandler):
    """
    Bootstrap HTTP API. Six operations mapped to REST endpoints.

    POST   /notebooks                              -> create notebook
    GET    /notebooks                              -> list notebooks
    POST   /notebooks/{id}/entries                 -> WRITE
    PUT    /notebooks/{id}/entries/{entry_id}       -> REVISE
    GET    /notebooks/{id}/entries/{entry_id}       -> READ
    GET    /notebooks/{id}/browse?query=&max=       -> BROWSE
    POST   /notebooks/{id}/share                   -> SHARE
    GET    /notebooks/{id}/observe?since=            -> OBSERVE
    """

    store: NotebookStore = None  # set by server setup

    def _send_json(self, data: dict, status: int = 200):
        body = json.dumps(data, indent=2).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _send_error(self, status: int, message: str):
        self._send_json({"error": message}, status)

    def _read_body(self) -> dict:
        length = int(self.headers.get("Content-Length", 0))
        if length == 0:
            return {}
        raw = self.rfile.read(length)
        return json.loads(raw)

    def _parse_path(self):
        parsed = urlparse(self.path)
        parts = [p for p in parsed.path.strip("/").split("/") if p]
        params = parse_qs(parsed.query)
        return parts, params

    def do_GET(self):
        parts, params = self._parse_path()

        try:
            if parts == ["notebooks"]:
                self._send_json({"notebooks": self.store.list_notebooks()})
                return

            if len(parts) >= 2 and parts[0] == "notebooks":
                notebook_id = parts[1]

                if len(parts) == 3 and parts[2] == "browse":
                    query = params.get("query", [""])[0]
                    max_entries = int(params.get("max", ["50"])[0])
                    result = self.store.browse(notebook_id, query, max_entries)
                    if result is None:
                        self._send_error(404, "Notebook not found")
                    else:
                        self._send_json(result)
                    return

                if len(parts) == 3 and parts[2] == "observe":
                    since = int(params.get("since", ["0"])[0])
                    result = self.store.observe(notebook_id, since)
                    if result is None:
                        self._send_error(404, "Notebook not found")
                    else:
                        self._send_json(result)
                    return

                if len(parts) == 4 and parts[2] == "entries":
                    entry_id = parts[3]
                    result = self.store.read_entry(notebook_id, entry_id)
                    if result is None:
                        self._send_error(404, "Entry not found")
                    else:
                        self._send_json(result)
                    return

                if len(parts) == 2:
                    meta = self.store.get_notebook(notebook_id)
                    if meta is None:
                        self._send_error(404, "Notebook not found")
                    else:
                        self._send_json(meta)
                    return

            self._send_error(404, "Not found")

        except Exception as e:
            self._send_error(500, str(e))

    def do_POST(self):
        parts, params = self._parse_path()

        try:
            if parts == ["notebooks"]:
                body = self._read_body()
                name = body.get("name", "unnamed")
                owner = body.get("owner", "system")
                result = self.store.create_notebook(name, owner)
                self._send_json(result, 201)
                return

            if len(parts) >= 2 and parts[0] == "notebooks":
                notebook_id = parts[1]

                if len(parts) == 3 and parts[2] == "entries":
                    body = self._read_body()
                    result = self.store.write_entry(
                        notebook_id=notebook_id,
                        content=body.get("content", ""),
                        content_type=body.get("content_type", "text/plain"),
                        topic=body.get("topic", ""),
                        references=body.get("references", []),
                        author=body.get("author", "anonymous"),
                    )
                    self._send_json(result, 201)
                    return

                if len(parts) == 3 and parts[2] == "share":
                    body = self._read_body()
                    result = self.store.share_notebook(
                        notebook_id=notebook_id,
                        entity=body.get("entity", ""),
                        read=body.get("read", True),
                        write=body.get("write", False),
                    )
                    if result is None:
                        self._send_error(404, "Notebook not found")
                    else:
                        self._send_json(result)
                    return

            self._send_error(404, "Not found")

        except Exception as e:
            self._send_error(500, str(e))

    def do_PUT(self):
        parts, params = self._parse_path()

        try:
            if (
                len(parts) == 4
                and parts[0] == "notebooks"
                and parts[2] == "entries"
            ):
                notebook_id = parts[1]
                entry_id = parts[3]
                body = self._read_body()
                result = self.store.revise_entry(
                    notebook_id=notebook_id,
                    entry_id=entry_id,
                    new_content=body.get("content", ""),
                    reason=body.get("reason", ""),
                    author=body.get("author", "anonymous"),
                )
                if result is None:
                    self._send_error(404, "Entry not found")
                else:
                    self._send_json(result)
                return

            self._send_error(404, "Not found")

        except Exception as e:
            self._send_error(500, str(e))

    def log_message(self, format, *args):
        """Structured logging."""
        sys.stderr.write(
            f"[{_now_iso()}] {self.address_string()} - {format % args}\n"
        )


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Bootstrap Notebook Server")
    parser.add_argument("--port", type=int, default=8723, help="Server port")
    parser.add_argument(
        "--data", type=str, default="./notebook-data", help="Data directory"
    )
    args = parser.parse_args()

    store = NotebookStore(args.data)
    NotebookHTTPHandler.store = store

    server = HTTPServer(("0.0.0.0", args.port), NotebookHTTPHandler)
    print(f"Bootstrap Notebook Server")
    print(f"  Data:  {os.path.abspath(args.data)}")
    print(f"  Port:  {args.port}")
    print(f"  URL:   http://localhost:{args.port}")
    print()
    print("Endpoints:")
    print("  POST   /notebooks                          Create notebook")
    print("  GET    /notebooks                          List notebooks")
    print("  POST   /notebooks/{{id}}/entries             WRITE")
    print("  PUT    /notebooks/{{id}}/entries/{{eid}}       REVISE")
    print("  GET    /notebooks/{{id}}/entries/{{eid}}       READ")
    print("  GET    /notebooks/{{id}}/browse?query=&max=  BROWSE")
    print("  POST   /notebooks/{{id}}/share               SHARE")
    print("  GET    /notebooks/{{id}}/observe?since=       OBSERVE")
    print()
    print("Ready. Ctrl+C to stop.")

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down.")
        server.server_close()


if __name__ == "__main__":
    main()
