# Step 9: Semantic Search UI

**Depends on:** Steps 4 (Filtered Browse & Search), 6 (MCP Updates)

## Goal

Expose the existing backend semantic search endpoint (`POST /notebooks/{notebookId}/semantic-search`) through the frontend admin panel and MCP server. The backend already supports embedding-based cosine similarity search via Ollama/OpenAI — this step wires it to user-facing interfaces.

## 9.1 — Frontend DTOs

Added to `frontend/admin/Models/NotebookModels.cs`:

- `SemanticSearchResultDto` — mirrors backend `SemanticSearchResult`: EntryId, Topic, Similarity, Claims, ClaimsStatus, MaxFriction, IntegrationStatus
- `SemanticSearchResponse` — wrapper with `List<SemanticSearchResultDto> Results`

## 9.2 — Frontend API Client

Added `SemanticSearchAsync` to `frontend/admin/Services/NotebookApiClient.cs`:

- `POST /notebooks/{notebookId}/semantic-search`
- Body: `{ "query": "...", "top_k": 10, "min_similarity": 0.3 }`
- Returns `SemanticSearchResponse`

## 9.3 — Frontend UI

Updated `frontend/admin/Components/Pages/Notebooks/View.razor`:

- **Search mode toggle** — dropdown next to search button: "Lexical" (default) or "Semantic"
- **Button label** — shows "Search" for lexical, "Semantic Search" for semantic mode
- **Semantic results table** — shows Topic, Similarity (%), Claims Status, Integration Status, Max Friction
- **RunServerSearch** dispatches to correct API based on `searchMode`
- **503 handling** — graceful message when embedding service is unavailable
- **ClearServerSearch** clears both lexical and semantic result lists

## 9.4 — MCP Tool

Added `thinktank_semantic_search` tool to `backend/mcp/thinktank_mcp.py`:

- `tool_semantic_search(query, top_k=10, min_similarity=0.3)` — POST to `/notebooks/{NOTEBOOK_ID}/semantic-search`
- Tool schema with `query` (required), `top_k` (optional int), `min_similarity` (optional number)
- Wired into TOOLS list and `tools/call` dispatch

## Verify

1. `dotnet build` the frontend (`frontend/admin/`) — no compilation errors
2. Search mode toggle appears on notebook view page
3. Lexical search still works as before (regression)
4. If embedding service running: semantic search returns results with similarity scores
5. If embedding service NOT running: graceful 503 error message
6. MCP tool appears in tool list (`thinktank_semantic_search`)

## Summary of Changes

| File | Change |
|------|--------|
| `frontend/admin/Models/NotebookModels.cs` | `SemanticSearchResultDto`, `SemanticSearchResponse` DTOs |
| `frontend/admin/Services/NotebookApiClient.cs` | `SemanticSearchAsync` method |
| `frontend/admin/Components/Pages/Notebooks/View.razor` | Search mode toggle, semantic results display, updated search methods |
| `backend/mcp/thinktank_mcp.py` | `tool_semantic_search`, tool schema, dispatch case |

**Status:** ✅ Complete (Feb 22, 2026)
