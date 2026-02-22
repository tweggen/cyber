# Robot Workers

Stateless LLM-powered worker processes that pull jobs from the notebook server's job queue, process them with Claude Haiku, and push results back.

## Overview

Robot workers implement the knowledge pipeline:
1. **Claim Distillation** (DISTILL_CLAIMS) — Extract key factual statements from entry content
2. **Claim Comparison** (COMPARE_CLAIMS) — Measure novelty and contradiction between claim sets
3. **Topic Classification** (CLASSIFY_TOPIC) — Assign entries to topics based on their claims

Workers are:
- **Stateless** — Can be scaled horizontally with no coordination
- **HTTP-based** — Work identically regardless of backend implementation (Rust, .NET, etc.)
- **Fault-tolerant** — Handle network errors and LLM failures gracefully
- **Observable** — Log all operations for debugging and monitoring

## Installation

```bash
cd notebook/robots
pip install -r requirements.txt
```

## Running Workers

### Single Worker

Process all job types:

```bash
python robot.py \
  --server http://localhost:5000 \
  --notebook 2f00ed6c-4fa0-475d-a762-f29309ec2304 \
  --worker-id robot-haiku-1 \
  --token "$JWT_TOKEN" \
  --model claude-haiku-4-5-20251001
```

### Specialized Workers (by Job Type)

Only process claim distillation:

```bash
python robot.py \
  --server http://localhost:5000 \
  --notebook 2f00ed6c-4fa0-475d-a762-f29309ec2304 \
  --worker-id robot-distill-1 \
  --token "$JWT_TOKEN" \
  --job-type DISTILL_CLAIMS
```

Only process comparisons:

```bash
python robot.py \
  --server http://localhost:5000 \
  --notebook 2f00ed6c-4fa0-475d-a762-f29309ec2304 \
  --worker-id robot-compare-1 \
  --token "$JWT_TOKEN" \
  --job-type COMPARE_CLAIMS
```

### Parallel Workers

Run 4 workers in parallel:

```bash
for i in 1 2 3 4; do
  python robot.py \
    --server http://localhost:5000 \
    --notebook 2f00ed6c-4fa0-475d-a762-f29309ec2304 \
    --worker-id "robot-haiku-$i" \
    --token "$JWT_TOKEN" &
done
wait
```

## Command-Line Options

| Option | Required | Default | Description |
|--------|:--------:|:-------:|-------------|
| `--server` | ✅ | — | Notebook server URL (e.g., `http://localhost:5000`) |
| `--notebook` | ✅ | — | Notebook UUID |
| `--worker-id` | ✅ | — | Unique worker identifier (e.g., `robot-haiku-1`) |
| `--token` | ✅ | — | JWT Bearer token for authentication |
| `--job-type` | — | all | Job type to process: `DISTILL_CLAIMS`, `COMPARE_CLAIMS`, or `CLASSIFY_TOPIC` |
| `--model` | — | `claude-haiku-4-5-20251001` | Anthropic model to use |
| `--poll-interval` | — | `5.0` | Seconds between poll attempts when no jobs available |

## Getting a JWT Token

Obtain a token from the notebook server (or admin panel):

```bash
# Via admin panel at /profile
# Or via API
curl -X POST http://localhost:5000/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"user","password":"pass"}'
```

## Job Processing Flow

1. **Worker pulls next job** from `GET /notebooks/{id}/jobs/next`
   - Returns job ID, type, and payload
   - Blocks until a job is available

2. **Worker processes job** with Claude Haiku:
   - Builds prompt from payload
   - Calls LLM API
   - Parses structured result

3. **Worker submits result** via `POST /notebooks/{id}/jobs/{id}/complete`
   - Sends worker ID and parsed result
   - Server stores result in database
   - Triggers next step in pipeline

4. **On error**: `POST /notebooks/{id}/jobs/{id}/fail`
   - Worker logs error and reports failure
   - Server marks job as failed

## Job Types

### DISTILL_CLAIMS

**Payload:**
```json
{
  "content": "Long text to extract claims from",
  "max_claims": 12,
  "context_claims": [
    {"text": "System uses OAuth", "confidence": 0.9}
  ]
}
```

**Result:**
```json
{
  "claims": [
    {"text": "...", "confidence": 0.95},
    ...
  ]
}
```

### COMPARE_CLAIMS

**Payload:**
```json
{
  "claims_a": [{"text": "...", "confidence": 0.9}],
  "claims_b": [{"text": "...", "confidence": 0.9}]
}
```

**Result:**
```json
{
  "entropy": 0.33,
  "friction": 0.0,
  "contradictions": [
    {
      "claim_a": "...",
      "claim_b": "...",
      "severity": 0.8
    }
  ]
}
```

### CLASSIFY_TOPIC

**Payload:**
```json
{
  "claims": [{"text": "...", "confidence": 0.9}],
  "available_topics": ["devops", "security", "architecture"]
}
```

**Result:**
```json
{
  "primary_topic": "architecture",
  "secondary_topics": ["devops"],
  "new_topic": null
}
```

## Testing

Run unit tests for prompt templates and parsing:

```bash
pip install pytest
pytest test_prompts.py -v
```

## End-to-End Verification

1. **Start notebook server:**
   ```bash
   dotnet run --project backend/src/Notebook.Server
   ```

2. **Create an entry** (which creates DISTILL_CLAIMS job):
   ```bash
   curl -X POST http://localhost:5000/notebooks/{id}/entries \
     -H "Authorization: Bearer $TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"content":"Your text here","content_type":"text/plain"}'
   ```

3. **Start worker:**
   ```bash
   python robot.py --server http://localhost:5000 \
     --notebook {id} --worker-id robot-1 --token $TOKEN
   ```

4. **Verify claims were extracted:**
   ```bash
   curl http://localhost:5000/notebooks/{id}/entries/{entry_id} \
     -H "Authorization: Bearer $TOKEN" | jq '.claims'
   ```

5. **Verify COMPARE_CLAIMS jobs were created:**
   ```bash
   curl http://localhost:5000/notebooks/{id}/jobs/stats \
     -H "Authorization: Bearer $TOKEN" | jq '.COMPARE_CLAIMS'
   ```

## Monitoring

Workers log all operations to stdout. Log levels:
- **INFO** — Job pulled, job completed, worker startup/shutdown
- **DEBUG** — No jobs available (every 60 seconds)
- **ERROR** — Job failures, API errors, unexpected exceptions

Example output:

```
2026-02-22 10:15:30 INFO [robot] Starting robot worker: id=robot-haiku-1 model=claude-haiku-4-5-20251001 server=http://localhost:5000 notebook=2f00ed6c-4fa0-475d-a762-f29309ec2304
2026-02-22 10:15:35 INFO [robot] Processing job 550e8400-e29b-41d4-a716-446655440000 (type=DISTILL_CLAIMS)
2026-02-22 10:15:38 INFO [robot] Job 550e8400-e29b-41d4-a716-446655440000 completed (total: 1 completed, 0 failed)
```

## Architecture Notes

- **Polling model:** Workers poll for jobs (no push/subscribe). Simplicity over latency.
- **Stateless:** Workers store no local state. Can be killed/restarted at any time.
- **Language-agnostic:** Job API is HTTP-only. Workers can be implemented in any language.
- **Model selection:** Uses Claude Haiku by default for cost efficiency. Can override with `--model`.
- **Error handling:** Failed jobs don't block the queue. Retry mechanism is server-side.

## FAQ

**Q: How many workers do I need?**

A: Depends on load. Start with 2-4 workers and monitor job queue depth. Scale horizontally by adding more worker processes.

**Q: Can workers process different notebooks?**

A: Yes, start separate worker processes with different `--notebook` IDs. Each worker is independent.

**Q: What happens if a worker crashes?**

A: Jobs become stuck in "in_progress" state. The server has a timeout (configurable) after which jobs are re-queued. Restart the worker and it will pick up pending jobs.

**Q: Can I use a different LLM?**

A: Yes, via `--model`. Use any Anthropic model ID. Prompt templates assume Claude's JSON parsing ability.

**Q: How do I know if a job failed?**

A: Check worker logs. Failed jobs are reported via `POST /jobs/{id}/fail` and marked as "failed" on the server. View them at `/admin/notebooks/{id}` in the admin panel.

## Development

### Code Structure

- **robot.py** — Main worker loop (pull, process, submit)
- **prompts.py** — LLM prompt builders and result parsers
- **test_prompts.py** — Unit tests for prompts and parsing

### Adding a New Job Type

1. Add job type to `CLASSIFY_TOPIC` choices in `robot.py`
2. Add `build_X_prompt()` function to `prompts.py`
3. Add `parse_X_result()` function to `prompts.py`
4. Add test cases to `test_prompts.py`
5. Update this README

---

**Status:** ✅ Phase 5 Complete (February 22, 2026)

For issues or questions, file an issue on GitHub.
