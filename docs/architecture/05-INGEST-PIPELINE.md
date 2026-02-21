# 05 — Ingest Pipeline

## Use Case

Ingest 14,000 HTML pages from a Confluence dump into the notebook, with all information readable and the notebook reasonably indexed throughout.

## End-to-End Flow

```
 Confluence HTML files on disk
            │
            ▼
 ┌──────────────────────┐
 │  Phase 1a: EXTRACT   │  No LLM. Python script.
 │  Parse source files  │  Seconds per file.
 │  + extract metadata  │
 └──────────┬───────────┘
            │
            ▼
 ┌──────────────────────┐
 │  Phase 1b: FRAGMENT  │  No LLM. Python script.
 │  Split at markdown   │  Operates on markdown.
 │  heading boundaries  │  (pre- or post-normalization)
 │  if > token budget   │
 └──────────┬───────────┘
            │
            ▼
 ┌──────────────────────┐
 │  Phase 2: UPLOAD     │  No LLM. Batch write API.
 │  Batch write to      │  Server normalizes on write
 │  notebook server     │  (HTML→markdown, etc.)
 │  (raw or normalized) │  ~140 API calls total.
 └──────────┬───────────┘
            │
            ▼
 ┌──────────────────────┐
 │  Phase 3: DISTILL    │  Cheap LLM (Haiku). Robot workers.
 │  Content → claims    │  14,000 jobs. ~30 min with 4 robots.
 │  Fragment if needed  │  Cost: ~$3
 └──────────┬───────────┘
            │
            ▼
 ┌──────────────────────┐
 │  Phase 4: COMPARE    │  Cheap LLM (Haiku). Robot workers.
 │  Claims vs topic     │  14,000 jobs. ~30 min with 4 robots.
 │  index → entropy +   │  Cost: ~$3
 │  friction scores     │
 └──────────┬───────────┘
            │
            ▼
 ┌──────────────────────┐
 │  Phase 5: REVIEW     │  Strong LLM (Sonnet/Opus). On-demand.
 │  Resolve high-       │  Only high-friction entries.
 │  friction entries    │  Maybe 5-10% of total.
 │  Consolidate topics  │  Cost: ~$50-100
 └──────────────────────┘
```

## Phase 1a: Extract

### Script: `confluence_extract.py`

Input: Directory of HTML files from Confluence export.

The extract phase focuses on **metadata extraction and source-specific cleanup** — not format conversion. Format conversion (HTML→markdown) is handled by the server's normalization layer on write (see `07-NORMALIZATION.md`). Ingest scripts MAY pre-normalize for efficiency or control, but it is not required.

Per file:
1. Extract metadata:
   - **Title**: from `<title>` or first `<h1>`
   - **Space**: Confluence space key (from path or metadata)
   - **Page ID**: Confluence page ID (from filename or metadata)
   - **Parent page**: if available in metadata (for hierarchy reconstruction)
   - **Labels/tags**: if present
2. Strip Confluence-specific boilerplate that is not meaningful content:
   - `<ac:*>` macro tags (Confluence-specific)
   - `<ri:*>` resource identifier tags
   - Empty containers, navigation, sidebars
   - Table of contents macros, user profile cards, page metadata blocks
   - "Created by / Last modified by" footers
3. Pass content with `content_type: "text/html"` — the server normalizes on write

Output: JSON Lines file, one object per page:

```jsonl
{"title": "CI/CD Pipeline Setup", "space": "ENG", "page_id": "12345", "content": "<h1>CI/CD Pipeline...</h1>...", "content_type": "text/html", "topic": "confluence/ENG/ci-cd"}
```

### Topic derivation

Use the Confluence space hierarchy to derive topics automatically:
- Space `ENG` → `confluence/ENG/`
- Page path `ENG > DevOps > CI/CD Pipeline` → `confluence/ENG/devops/ci-cd-pipeline`
- This gives a reasonable starting taxonomy without any LLM involvement

### Alternative: pre-normalize in the script

Ingest scripts can optionally normalize before upload (e.g., using `html2text` or BeautifulSoup) and upload as `content_type: "text/markdown"`. This is useful when:
- The source format needs domain-specific cleanup the server's generic normalizer can't handle
- You want to verify the normalization output before uploading
- You're processing locally and want to reduce server-side work

When pre-normalizing, preserve:
- Headings (as markdown `#`)
- Code blocks (triple backticks)
- Tables (markdown table format)
- Lists (markdown list syntax)
- Links (markdown link syntax)
- Images: alt text reference, not image data

## Phase 1b: Fragment

### Format-agnostic fragmentation

Fragmentation operates on **markdown** content. If the ingest script pre-normalizes, it can fragment before upload. If not, the server normalizes on write, and fragmentation happens on the normalized markdown.

If content exceeds ~4,000 tokens:
1. Split at markdown heading boundaries (`#`, `##`, `###`)
2. If a section still exceeds the threshold, split at paragraph boundaries
3. Each fragment becomes a separate entry with `fragment_of` pointing to the artifact entry

Output with fragments:

```jsonl
{"title": "CI/CD Pipeline Setup", "space": "ENG", "page_id": "12345", "content": "...", "content_type": "text/markdown", "topic": "confluence/ENG/ci-cd", "fragment_of": null, "fragment_index": null}
{"title": "CI/CD Pipeline Setup (part 2)", "space": "ENG", "page_id": "12345", "content": "...", "content_type": "text/markdown", "topic": "confluence/ENG/ci-cd", "fragment_of": "12345", "fragment_index": 1}
```

## Phase 2: Upload

### Script: `confluence_upload.py`

Reads the JSON Lines file from Phase 1 and batch-writes to the notebook server.

The `content_type` field tells the server what format the content is in. If `content_type` is `text/html`, the server normalizes it to markdown on write. If the ingest script pre-normalized, it should set `content_type: "text/markdown"` and the server passes it through.

```python
import requests
import json

SERVER = "http://localhost:3000"
NOTEBOOK_ID = "2f00ed6c-4fa0-475d-a762-f29309ec2304"
BATCH_SIZE = 100

entries = []
with open("extracted.jsonl") as f:
    for line in f:
        page = json.loads(line)
        entries.append({
            "content": page["content"],
            "topic": page["topic"],
            "content_type": page.get("content_type", "text/html"),
            "fragment_of": page.get("fragment_of"),
            "fragment_index": page.get("fragment_index"),
        })

# Upload in batches
for i in range(0, len(entries), BATCH_SIZE):
    batch = entries[i:i+BATCH_SIZE]
    response = requests.post(
        f"{SERVER}/notebooks/{NOTEBOOK_ID}/batch",
        json={"entries": batch, "author": "confluence-import"}
    )
    print(f"Batch {i//BATCH_SIZE + 1}: {response.json()['jobs_created']} jobs queued")
```

### Duration estimate
- 14,000 entries ÷ 100 per batch = 140 API calls
- At ~100ms per call: ~14 seconds total
- Server normalization adds negligible overhead (HTML→markdown is CPU-cheap)

## Phase 3: Distill (Robot Workers)

Happens automatically. Phase 2 queued DISTILL_CLAIMS jobs for every entry. Robot workers pick them up.

### Context for distillation

For fragment entries, the robot receives the parent artifact's claims as context (if already distilled). For first-pass where no context exists yet:
1. Distill fragments without context (blind distillation)
2. Once all fragments are done, distill the artifact entry using fragment claims as context
3. Optionally: re-distill fragments with artifact context (second pass, higher quality)

The second pass is optional. It improves claim quality but doubles the distillation cost. Decision: skip for initial ingest, do it selectively for high-value topics later.

### Monitoring

```bash
# Check distillation progress
curl "$SERVER/notebooks/$NOTEBOOK_ID/jobs/stats"

# Response:
{
  "DISTILL_CLAIMS": { "pending": 8420, "in_progress": 4, "completed": 5576, "failed": 0 },
  "COMPARE_CLAIMS": { "pending": 5576, "in_progress": 0, "completed": 0, "failed": 0 },
  "CLASSIFY_TOPIC": { "pending": 0, "in_progress": 0, "completed": 0, "failed": 0 }
}
```

## Phase 4: Compare (Robot Workers)

Also happens automatically. When claims are written to an entry, the server queues COMPARE_CLAIMS jobs against relevant topic indices.

### Bootstrapping topic indices

Problem: at the start of ingest, there are no topic indices for the Confluence content. The Confluence space hierarchy provides initial topics, but there are no claim-sets to compare against.

Solution: a bootstrap sequence:
1. Upload all entries (Phase 2)
2. Distill all claims (Phase 3)
3. Build initial topic indices:
   - Group entries by topic prefix (e.g., `confluence/ENG/`)
   - For each topic group: take the top-confidence claims across all entries, deduplicate, select top N → these become the topic index claims
   - This can be done by a robot (DISTILL_CLAIMS job with all claims from a topic as input) or by a simple script
4. Now run COMPARE_CLAIMS jobs against the freshly created topic indices

This bootstrap step could be a dedicated script or a special job type (`BOOTSTRAP_INDEX`).

## Phase 5: Review (On-Demand, Expensive LLM)

After phases 1-4, the notebook contains:
- 14,000+ entries with claims
- Entropy and friction scores for each
- Entries flagged as `needs_review` where friction > threshold

A Claude agent (interactive or scheduled) handles:
1. **Contradiction resolution:** Read high-friction entries and their contradicting pairs. Determine if the contradiction is a genuine error, a temporal update, or a context difference. Write a resolution entry.
2. **Consolidation:** For topics with >20 entries, write a consolidation entry synthesizing the key knowledge.
3. **Index quality review:** Check that topic indices accurately reflect their entries. Revise as needed.

### Prioritization

Not all high-friction entries are equally important. The agent should process:
1. High friction + high confidence claims first (strong contradictions about central facts)
2. Topics with the most entries next (highest consolidation value)
3. Cross-topic contradictions last (require broader context)

## Cost Summary

| Phase | Processor | Duration (4 robots) | Cost |
|-------|-----------|---------------------|------|
| Extract + Fragment | Python script | ~5 minutes | $0 |
| Upload + Normalize | Python script + server | ~15 seconds | $0 |
| Distill | Haiku robots | ~30 minutes | ~$3 |
| Compare | Haiku robots | ~30 minutes | ~$3 |
| Review | Sonnet/Opus agent | hours (interactive) | ~$50-100 |
| **Total** | | **~1 hour automated + interactive review** | **~$60-110** |

Compare to: routing everything through Opus → >$1,000, many hours, no parallelism.

## Incremental Ingest

The pipeline isn't just for one-time bulk import. For ongoing use:
- New content → extract → batch write → robots distill + compare
- The same pipeline handles 1 page or 10,000
- The job queue absorbs spikes and processes at robot capacity
- Topic indices evolve as new content arrives
