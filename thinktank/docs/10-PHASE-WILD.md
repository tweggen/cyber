# 10 — Phase Wild: Claims-Aware MCP Server

**Status:** Planned — not yet implemented.
**Goal:** An MCP server for the thinktank that uses claims, embeddings, and the comparison graph for intelligent retrieval — replacing brute-force full-text search with claim-aware semantic navigation.

## Motivation

The current `notebook-mcp` (in `mcp/notebook_mcp.py`) targets the Rust notebook server's six operations. It uses `notebook_search` which does trigram substring matching on raw content — slow, noisy, and blind to meaning. Meanwhile, the thinktank v2 has built a rich knowledge graph:

- **Claims**: every entry is distilled into 1-20 short declarative statements with confidence scores
- **Embeddings**: claims are embedded into dense vectors (via Ollama's nomic-embed-text)
- **Comparisons**: entries are linked by entropy (novelty) and friction (contradiction) scores
- **Integration Status**: entries progress through `probation` → `integrated`/`contested` based on comparison results
- **Topics**: hierarchical slash-separated paths with prefix filtering

The "wild" MCP should make this infrastructure the primary retrieval mechanism. When a user asks "what does the notebook say about authentication?", instead of substring-matching "auth" against 10,000 entries, we should:

1. Embed the query
2. Find the top-K nearest entries by embedding similarity
3. Return their claims (not raw content) as a compact summary
4. Let the user drill into specific entries for full content

## Architecture

```
Claude Code / Claude Desktop
    ↕ MCP (stdio JSON-RPC)
wild-mcp (Python, mcp/wild_mcp.py)
    ↕ HTTP + Bearer JWT
Thinktank Notebook.Server (localhost:5000)
```

Same deployment model as notebook-mcp: single Python file, stdio transport, env var config.

## Key Design Decisions

### 1. Claims-First Retrieval

The MCP returns **claims** as the primary unit of information, not raw entry content. Claims are compact (1-2 sentences each), pre-distilled, and carry confidence scores. This means:

- Search results return claims with entry IDs, not full content blobs
- The LLM can reason over claims efficiently (20 claims fit in ~500 tokens vs. 20 entries at ~50,000 tokens)
- The user drills into specific entries only when they need the full source

### 2. Three Retrieval Modes

| Mode | Input | Mechanism | Use Case |
|---|---|---|---|
| **Semantic** | Natural language query | Embed query → cosine similarity vs. entry embeddings | "What does the notebook know about X?" |
| **Structural** | Topic prefix, friction threshold, claims status | Database indexes + filters | "Show me all architecture decisions" or "What's controversial?" |
| **Graph** | Entry ID | Follow comparison links (entropy/friction edges) | "What relates to this entry? What contradicts it?" |

### 3. New Server Endpoint Required: Semantic Search

The embedding nearest-neighbor search (`FindNearestByEmbeddingAsync`) is currently internal — only used during `COMPARE_CLAIMS` job execution. The MCP needs it exposed as an API endpoint.

**New endpoint:** `POST /notebooks/{id}/semantic-search`

```json
// Request
{
    "query": "authentication and authorization",
    "top_k": 10,
    "min_similarity": 0.3
}

// Response
{
    "results": [
        {
            "entry_id": "uuid",
            "topic": "architecture/security-model",
            "similarity": 0.87,
            "claims": [
                { "text": "Each thinktank operates at a single classification level", "confidence": 0.95 },
                { "text": "JWT scopes exist but are not enforced", "confidence": 0.90 }
            ],
            "claims_status": "distilled",
            "max_friction": 0.1,
            "integration_status": "integrated"
        }
    ]
}
```

The server embeds the query string using the same Ollama embedding model as the claim pipeline, then runs the existing `FindNearestByEmbeddingAsync` query (extended to return `integration_status`). This is the only new server-side code required.

**Decision: embed on server or in MCP?** On the server. The MCP shouldn't need Ollama access — it's a thin API client. The server already has the embedding model configured and the query is a simple Ollama `/api/embed` call.

## MCP Tools

### Core Retrieval Tools

**`wild_search`** — The primary tool. Combines semantic + lexical retrieval.

```
Input:  query (string), mode ("semantic"|"lexical"|"hybrid"), top_k (int), topic_prefix (string?), integration_status (string?)
Output: ranked list of {entry_id, topic, similarity, claims[], max_friction, integration_status}
```

- `semantic`: embed query → cosine similarity (requires new endpoint)
- `lexical`: trigram search on content + claims (existing `/search`)
- `hybrid`: run both, merge by reciprocal rank fusion, deduplicate

**`wild_related`** — Follow the comparison graph from an entry.

```
Input:  entry_id (string), direction ("similar"|"contradicts"|"all"), max_results (int)
Output: list of {entry_id, topic, entropy, friction, contradictions[], claims[], integration_status}
```

Reads the entry's comparisons, fetches related entries' claims. Lets the LLM explore the knowledge graph by following edges.

**`wild_claims`** — Get claims for one or more entries without full content.

```
Input:  entry_ids (string[])
Output: list of {entry_id, topic, claims[], claims_status, confidence_avg, integration_status}
```

Batch fetch — avoids N+1 reads when exploring search results.

**`wild_read`** — Full entry read (same as notebook_read but with comparison detail).

```
Input:  entry_id (string)
Output: full entry with content, claims, comparisons, references, revisions
```

### Navigation Tools

**`wild_topics`** — Browse the topic hierarchy.

```
Input:  prefix (string?), min_entries (int?)
Output: list of {topic, entry_count, avg_friction, claims_status_breakdown}
```

Uses existing `/browse?topic_prefix=...` with aggregation.

**`wild_friction`** — Find controversial areas.

```
Input:  min_friction (float), topic_prefix (string?), limit (int)
Output: list of {entry_id, topic, max_friction, integration_status, contradiction_count, top_contradiction}
```

Uses existing `/browse?has_friction_above=...&needs_review=true`. Can also filter directly via `integration_status=contested` to find entries that failed the friction threshold during the integration lifecycle.

**`wild_recent`** — What changed since last check.

```
Input:  since_sequence (int)
Output: list of changes with claims summaries
```

Uses existing `/observe`, enriches with claims for each changed entry.

### Write Tools

**`wild_write`** — Write entry (delegates to existing batch endpoint).
**`wild_revise`** — Revise entry (delegates to existing revise endpoint).

Same as notebook-mcp but with `source` field support for content filters.

### Meta Tools

**`wild_status`** — Notebook health: total entries, % with claims distilled, job queue depth, top friction areas, integration status breakdown (probation/integrated/contested counts).

## MCP Prompts

**`research`** — "Research a topic in the notebook"
```
1. Call wild_search with semantic mode
2. Examine top results' claims
3. Follow comparison graph for related/contradicting entries
4. Synthesize findings
```

**`contradictions`** — "Find and resolve contradictions"
```
1. Call wild_friction to find high-friction entries
2. Read the contradicting entries
3. Analyze whether contradictions are genuine or contextual
4. Write resolution entries
```

**`explore`** — "Explore what the notebook knows"
```
1. Call wild_topics to see the topic landscape
2. Pick interesting areas, call wild_search within them
3. Follow comparison graph to find clusters
4. Report findings with entry references
```

## Implementation Plan

### Step 1: Server-Side Semantic Search Endpoint

**New endpoint in Notebook.Server.**

| File | Change |
|---|---|
| `Notebook.Server/Endpoints/SearchEndpoints.cs` | Add `POST /notebooks/{id}/semantic-search` |
| `Notebook.Server/Models/SearchModels.cs` | Add `SemanticSearchRequest`, `SemanticSearchResult` DTOs |
| `Notebook.Data/Repositories/IEntryRepository.cs` | Add `SemanticSearchAsync(notebookId, queryEmbedding, topK, minSimilarity)` |
| `Notebook.Data/Repositories/EntryRepository.cs` | Implement (reuse `FindNearestByEmbeddingAsync` pattern but return claims) |

The endpoint:
1. Receives query string
2. Calls Ollama `/api/embed` to get the query embedding (needs `IOllamaClient` in server, or a lighter embed-only client)
3. Runs cosine similarity search against entry embeddings
4. Returns entries with claims, similarity scores

**Design choice — Ollama dependency in Notebook.Server:**
The server currently has no Ollama dependency (only ThinkerAgent does). Two options:
- **Option A:** Add a lightweight embed-only HTTP client to Notebook.Server. Config: `Embedding:OllamaUrl`, `Embedding:Model`.
- **Option B:** Accept a pre-computed embedding vector in the request. The MCP calls Ollama directly for embedding, sends the vector. Server only does the similarity search.

**Recommended: Option A.** Keeps the MCP thin (no Ollama dependency, no model config), and the server already knows which embedding model was used for existing embeddings (must match).

### Step 2: Batch Claims Endpoint

**New endpoint in Notebook.Server.**

`POST /notebooks/{id}/claims/batch` — fetch claims for multiple entry IDs in one call.

```json
// Request
{ "entry_ids": ["uuid1", "uuid2", "uuid3"] }

// Response
{
    "entries": [
        { "id": "uuid1", "topic": "...", "claims": [...], "claims_status": "distilled" },
        ...
    ]
}
```

Avoids N+1 reads when the MCP explores search results.

### Step 3: MCP Server Implementation

**Create:** `mcp/wild_mcp.py`

Structure mirrors `notebook_mcp.py`:
- Env vars: `THINKTANK_URL`, `THINKTANK_NOTEBOOK_ID`, `THINKTANK_TOKEN`
- Same MCP JSON-RPC stdio protocol
- Tools map to the API surface described above

### Step 4: Hybrid Search (Reciprocal Rank Fusion)

Implement client-side in the MCP:

```python
def hybrid_search(query, top_k, topic_prefix):
    semantic = api_semantic_search(query, top_k * 2)
    lexical = api_lexical_search(query, top_k * 2, topic_prefix)

    # Reciprocal Rank Fusion (k=60)
    scores = {}
    for rank, result in enumerate(semantic):
        scores[result.id] = scores.get(result.id, 0) + 1 / (60 + rank)
    for rank, result in enumerate(lexical):
        scores[result.id] = scores.get(result.id, 0) + 1 / (60 + rank)

    merged = sorted(scores.items(), key=lambda x: -x[1])[:top_k]
    return [fetch_claims(id) for id, _ in merged]
```

### Step 5: Integration Tests

Test the semantic search endpoint:
- Write entries → wait for DISTILL_CLAIMS + EMBED_CLAIMS to complete → semantic search → verify results
- Test with queries that match semantically but not lexically
- Test hybrid merge deduplication

## Files Summary

| File | Change |
|---|---|
| `Notebook.Server/Endpoints/SearchEndpoints.cs` | Add semantic search + batch claims endpoints |
| `Notebook.Server/Models/SearchModels.cs` | New DTOs |
| `Notebook.Server/Services/EmbeddingService.cs` | **New** — lightweight Ollama embed client for query embedding |
| `Notebook.Data/Repositories/IEntryRepository.cs` | Add `SemanticSearchAsync`, `GetClaimsBatchAsync` |
| `Notebook.Data/Repositories/EntryRepository.cs` | Implement new queries |
| `Notebook.Server/Program.cs` | Register EmbeddingService, config |
| `mcp/wild_mcp.py` | **New** — full MCP server |
| `tests/Notebook.Tests/Endpoints/SemanticSearchTests.cs` | **New** — integration tests |

## Future Enhancements

1. **pgvector** — Replace the `double precision[]` + SQL cosine similarity with pgvector's HNSW index for O(log n) nearest-neighbor. Currently the scan is O(n).
2. **Query expansion** — Use the LLM to expand the user's query into multiple claim-like statements before embedding, improving recall.
3. **Cluster navigation** — Expose the Rust server's cluster/catalog structure through the MCP, letting the LLM navigate clusters rather than individual entries.
4. **Caching** — Cache claim summaries per-topic in the MCP process to avoid repeated fetches within a conversation.
5. **Streaming results** — For large notebooks, stream semantic search results as they're scored rather than waiting for the full top-K.
