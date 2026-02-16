# 07 — Content Normalization

## Concept

The notebook is representation-agnostic: it accepts any content type and never imposes a format-specific internal schema. But the downstream consumers — LLMs for claim distillation, full-text search, and human readers — all work best with structured plain text. Raw HTML wastes tokens on markup, degrades search relevance, and forces every downstream consumer to handle format-specific parsing.

Normalization bridges this gap. It converts incoming content into **markdown** — structured plain English that preserves headings, lists, code blocks, and tables while stripping presentational markup. The system remains open to any input format, but what gets stored and indexed is always LLM-digestible.

### Why markdown

Markdown is the intermediate representation because:
- It IS "structured plain English": headings (`#`), lists (`-`), code (triple backticks), tables, links
- LLMs understand it natively — it's their lingua franca
- It's human-readable without rendering
- It preserves enough structure for fragmentation to split at natural boundaries (heading levels)
- It's lossless for the information that matters (text, structure) while discarding what doesn't (CSS, layout)

## The Normalization Contract

```
Input:  raw content bytes + content_type (e.g., "text/html")
Output: content as markdown + content_type set to "text/markdown"

Side effect: original_content_type records the input format for provenance
```

### Behavior by content type

| Input content_type | Action | Stored content_type | original_content_type |
|-------------------|--------|--------------------|-----------------------|
| `text/plain` | Passthrough (no change) | `text/plain` | not set |
| `text/markdown` | Passthrough (no change) | `text/markdown` | not set |
| `text/html` | Strip tags, convert structure to markdown | `text/markdown` | `text/html` |
| Unknown type | Store as-is with warning | unchanged | not set |

### Invariants

- `text/plain` and `text/markdown` always pass through unchanged — no unnecessary processing
- Normalization never discards textual information, only presentational markup
- The `original_content_type` field is only set when normalization actually transforms content
- Unknown content types are stored as-is — the system does not refuse content it can't normalize

## HTML Normalization Rules

HTML is the first (and currently only) format requiring normalization, driven by the Confluence ingest use case.

### Convert to markdown

- `<h1>` through `<h6>` → `#` through `######`
- `<p>` → paragraph (double newline)
- `<ul>/<ol>` with `<li>` → markdown list syntax (`-` or `1.`)
- `<pre>/<code>` → triple-backtick code blocks
- `<table>` → markdown table format
- `<a href="url">text</a>` → `[text](url)`
- `<strong>/<b>` → `**bold**`
- `<em>/<i>` → `*italic*`
- `<img>` → `![alt text](src)` (reference only, not the image data)
- `<blockquote>` → `>` prefix
- `<hr>` → `---`

### Strip without replacement

- `<div>`, `<span>` — container elements with no semantic meaning
- `<style>`, `<script>` — presentational/behavioral, not content
- CSS classes, IDs, inline styles — layout metadata
- Empty elements — no information value
- HTML comments — not content

### Source-specific boilerplate

Source-specific boilerplate (Confluence macros, navigation elements, page metadata blocks) should be stripped by the **ingest script** before upload, not by the generic normalizer. The normalizer handles standard HTML; the ingest script handles source-specific cleanup. This keeps the normalizer general-purpose.

## Server-Side Enforcement

Normalization happens in the server's write path — both single writes and batch writes.

### Why server-side

- **Single enforcement point**: every write goes through the server, including MCP writes from Claude, batch ingest, and interactive uploads. Normalizing at the server means no content reaches storage unnormalized.
- **Ingest scripts don't need to remember**: scripts can upload raw HTML and the server handles it.
- **Search works correctly**: what's indexed is what the LLM sees — clean markdown, not HTML soup.
- **Interactive writes get the same treatment**: if Claude pastes an HTML snippet via MCP, it gets normalized just like a bulk import.

### Write path (pseudocode)

```
on_write(entry):
    if entry.content_type in ["text/plain", "text/markdown"]:
        # Passthrough — store as-is
        store(entry)
    elif entry.content_type == "text/html":
        # Normalize
        normalized = html_to_markdown(entry.content)
        entry.original_content_type = entry.content_type
        entry.content = normalized
        entry.content_type = "text/markdown"
        store(entry)
    else:
        # Unknown type — store as-is, log warning
        log.warn(f"Unknown content_type: {entry.content_type}, storing as-is")
        store(entry)
```

### Batch writes

Batch write applies the same normalization per-entry. Each entry in a batch is normalized independently based on its own `content_type`.

## Provenance: `original_content_type`

When normalization transforms content, the original format is recorded in `original_content_type`. This serves:

- **Auditing**: know which entries were transformed and from what format
- **Re-normalization**: if the normalizer improves, entries can be re-processed from the original source
- **Debugging**: if normalization produces bad output, trace it back to the input format

### Schema change (future)

```sql
ALTER TABLE entries ADD COLUMN original_content_type TEXT;
-- Nullable. Only set when normalization occurred.
-- NULL means the stored content is in its original format.
```

## Relationship to Fragmentation

Normalization and fragmentation are two separate concerns that were previously entangled in format-specific ingest scripts.

```
                    ┌─────────────┐
  Raw content ───→  │ Normalize   │ ───→ Markdown
  (any format)      │ (server)    │      (clean text)
                    └─────────────┘
                          │
                          ▼
                    ┌─────────────┐
  Markdown    ───→  │ Fragment    │ ───→ Entry fragments
  (clean text)      │ (script or  │      (each < token budget)
                    │  server)    │
                    └─────────────┘
```

**Normalization happens BEFORE fragmentation.** Fragmentation operates on markdown, splitting at heading boundaries (`#`, `##`, etc.). This means fragmentation logic is format-agnostic — it never needs to understand HTML, only markdown structure.

### Two valid workflows

1. **Server normalizes, script fragments**: Upload raw HTML. Server normalizes to markdown. Script downloads, fragments if needed, re-uploads fragments.
2. **Script pre-normalizes and fragments**: Script converts HTML to markdown locally, fragments, uploads as `text/markdown`. Server passes through.

Workflow 2 is more efficient for bulk ingest (avoids round-trips). Workflow 1 is simpler for interactive/ad-hoc writes.

## Future Extensions

### Additional normalizers

The normalizer interface is designed for extension. Future content types:

| Content type | Normalizer | Notes |
|-------------|-----------|-------|
| `application/pdf` | PDF text extraction → markdown | Would need a PDF library |
| `text/csv` | CSV → markdown table | Straightforward conversion |
| `application/json` | JSON → formatted markdown code block | Preserve structure |
| `text/xml` | XML → markdown (context-dependent) | May need domain-specific handling |

### Raw content preservation

A future enhancement could store the raw original content as a blob alongside the normalized version, enabling re-normalization when normalizers improve. This is not in the initial implementation — `original_content_type` provides enough provenance for now.
