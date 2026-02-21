# Chapter 9: ThinkerAgent Operator

## Role Overview

As a **ThinkerAgent Operator**, you deploy, configure, and monitor AI processing workers that analyze notebooks and extract knowledge. You're responsible for keeping these workers healthy and optimally configured.

**Key Responsibilities:**
- Deploy ThinkerAgent instances
- Configure Ollama and embedding endpoints
- Monitor worker health and performance
- Handle job failures and retries
- Optimize resource utilization
- Troubleshoot agent issues

**Required Permissions:**
- Infrastructure access (SSH, container platforms)
- Agent registration credentials
- System admin or operator role

**Typical Workflows:** 3 core workflows in this chapter

---

## Workflow 1: Deploying ThinkerAgents

### Overview

Deploy agent instances to infrastructure, connect them to Cyber, and verify health.

**Use case:** You're deploying a new embeddings agent to speed up search indexing. You provision the infrastructure, start the agent, and verify it connects to Cyber.

**Related workflows:**
- [Monitoring Job Queues](06-notebook-owner.md#workflow-4-monitoring-job-pipeline) — Monitor jobs agent processes
- [Configuring Agents](08-system-administrator.md#workflow-4-agent-management) — Register agents in Cyber

### Prerequisites

- [ ] Agent credentials from Cyber admin
- [ ] Infrastructure provisioned (VM, container, cloud instance)
- [ ] Network connectivity to Cyber server
- [ ] Ollama or embedding service running (if needed)

### Step-by-Step Instructions

#### Step 1: Provision Infrastructure

Provision a server or container for the agent:

```bash
# Example: Deploy as Docker container

docker run -d \
  --name cyber-embedding-worker \
  --restart unless-stopped \
  -e CYBER_AGENT_ID=research-embedder-abc123 \
  -e CYBER_AGENT_TOKEN=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9... \
  -e CYBER_SERVER=https://cyber.company.com \
  -e OLLAMA_URL=http://ollama:11434 \
  -e WORKER_THREADS=8 \
  -e MAX_QUEUE_SIZE=100 \
  -p 8080:8080 \
  cyber/embedding-worker:latest

# Verify container is running
docker ps | grep cyber-embedding-worker
```

#### Step 2: Configure Worker Environment

Set up environment variables:

```bash
# Create .env file
cat > worker.env << EOF
# Cyber Connection
CYBER_AGENT_ID=research-embedder-abc123
CYBER_AGENT_TOKEN=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
CYBER_SERVER=https://cyber.company.com
CYBER_HEARTBEAT_INTERVAL=30s

# Embedding Service
OLLAMA_URL=http://ollama.internal:11434
EMBEDDING_MODEL=nomic-embed-text
EMBEDDING_DIMENSION=768

# Worker Configuration
WORKER_THREADS=8
MAX_QUEUE_SIZE=100
JOB_TIMEOUT=30m
RETRY_ATTEMPTS=3
RETRY_BACKOFF=exponential

# Monitoring
LOG_LEVEL=info
PROMETHEUS_PORT=9090
HEALTH_CHECK_PORT=8080

# Security
TLS_ENABLED=true
TLS_CERT=/etc/agent/cert.pem
TLS_KEY=/etc/agent/key.pem
EOF

# Load environment
source worker.env
```

#### Step 3: Start Agent

Start the agent process:

```bash
# Start agent from binary
./cyber-worker start --config worker.env

# Or start via systemd (for persistent agents)
sudo systemctl start cyber-embedding-worker
sudo systemctl enable cyber-embedding-worker

# Check status
sudo systemctl status cyber-embedding-worker
```

#### Step 4: Verify Connection

Check that agent connects to Cyber:

```bash
# Check agent logs
docker logs cyber-embedding-worker | tail -20

# Expected output:
# [INFO] Connecting to https://cyber.company.com...
# [INFO] Heartbeat sent successfully
# [INFO] Agent registered: research-embedder-abc123
# [INFO] Ready to process jobs

# Verify health endpoint
curl http://localhost:8080/health
# Expected: {"status":"healthy","uptime":"2m30s","jobs_processed":0}
```

#### Step 5: Verify in Cyber Admin Panel

In Cyber, check agent status (Admin → Agents):

```
Agent: research-embedder
Status: ✅ Online (healthy)
Last Heartbeat: 2 minutes ago
Uptime: 2 minutes 30 seconds
Jobs Processed: 0 (waiting for first job)

Performance:
  CPU: 5% (idle)
  Memory: 0.8 GB / 4 GB
  Network: 10 Mbps (heartbeat)
```

### Verification

Confirm agent deployment is successful:

- [ ] Container/process is running
- [ ] Environment variables are set
- [ ] Health endpoint responds 200
- [ ] Agent appears in Cyber admin panel as "Online"
- [ ] Heartbeat is being sent regularly
- [ ] No errors in logs

---

## Workflow 2: Configuring Ollama and Embeddings

### Overview

Set up and configure Ollama (or other embedding service) for agents to use.

**Use case:** You need to switch to a faster embedding model. You update Ollama configuration, pull the new model, and restart agents.

**Related workflows:**
- [Deploying Agents](#workflow-1-deploying-thinkeragents) — Agents depend on Ollama
- [Monitoring Performance](#workflow-3-monitoring-worker-health) — Monitor embedding latency

### Prerequisites

- [ ] Ollama or embedding service installed
- [ ] GPU available (optional but recommended)
- [ ] Storage for models (~2-8 GB per model)

### Step-by-Step Instructions

#### Step 1: Install Ollama

```bash
# macOS
brew install ollama

# Linux
curl https://ollama.ai/install.sh | sh

# Windows
# Download installer from https://ollama.ai/download

# Verify installation
ollama --version
ollama serve  # Start Ollama service (runs on port 11434)
```

#### Step 2: Pull Embedding Models

```bash
# In another terminal, pull models
ollama pull nomic-embed-text   # Recommended: fast, accurate
ollama pull mxbai-embed-large  # Alternative: higher quality
ollama pull all-minilm-l6-v2   # Legacy: lightweight

# List available models
ollama list

# Output:
# NAME                           SIZE      MODIFIED
# nomic-embed-text:latest        274 MB    2 hours ago
# mxbai-embed-large:latest       669 MB    5 hours ago
# all-minilm-l6-v2:latest        92 MB     1 day ago
```

#### Step 3: Configure Agent to Use Model

```bash
# In worker.env, specify embedding model
EMBEDDING_MODEL=nomic-embed-text

# Restart agent to pick up new model
docker restart cyber-embedding-worker

# Verify model is being used
curl http://localhost:8080/model
# {"model":"nomic-embed-text","dimension":768,"latency_ms":12}
```

#### Step 4: Monitor Embedding Performance

```bash
# Check embedding latency
curl http://localhost:8080/metrics | grep embedding_latency

# Output:
# embedding_latency_ms: 12.3 (average)
# embedding_latency_p99_ms: 45.2 (99th percentile)

# If latency is high (>100ms), consider:
# 1. Reduce WORKER_THREADS (less contention)
# 2. Add GPU (GPU_ENABLED=true in env)
# 3. Switch to faster model (nomic-embed-text is fastest)
```

#### Step 5: Scale Ollama (Optional)

For high-load scenarios, run Ollama separately:

```bash
# Run Ollama on dedicated machine/container
docker run -d \
  --name ollama \
  --gpus all \
  -v ollama-data:/root/.ollama \
  -p 11434:11434 \
  ollama/ollama:latest

# Pull models into this instance
docker exec ollama ollama pull nomic-embed-text

# Configure agents to point to this Ollama instance
OLLAMA_URL=http://ollama.internal:11434
```

### Verification

Confirm Ollama is working:

- [ ] Ollama service is running
- [ ] Models are pulled and available
- [ ] Agents can connect and request embeddings
- [ ] Embedding latency is acceptable (< 50ms)
- [ ] No OOM or GPU errors in logs

---

## Workflow 3: Monitoring Worker Health

### Overview

Monitor agent health, diagnose issues, and handle failures.

**Use case:** An embedding agent stopped processing jobs. You check its status, see it ran out of memory, restart it, and increase memory allocation.

**Related workflows:**
- [System Monitoring](08-system-administrator.md#workflow-3-system-monitoring) — Platform-wide health
- [Deploying Agents](#workflow-1-deploying-thinkeragents) — Agent deployment

### Prerequisites

- [ ] Agent(s) deployed and running
- [ ] Access to agent logs and metrics
- [ ] Monitoring/alerting system (optional)

### Step-by-Step Instructions

#### Step 1: Check Agent Status

```bash
# Check via Cyber admin panel
# Admin → Agents → [Select agent]

# Or via API
curl -H "Authorization: Bearer TOKEN" \
  https://cyber.company.com/api/agents/research-embedder-abc123

# Response:
# {
#   "id": "research-embedder-abc123",
#   "status": "online",
#   "last_heartbeat": "2026-01-31T15:30:00Z",
#   "uptime": "2h15m",
#   "cpu_percent": 65,
#   "memory_mb": 2100,
#   "memory_limit_mb": 4096,
#   "jobs_in_progress": 3,
#   "jobs_completed": 1247,
#   "jobs_failed": 2
# }
```

#### Step 2: Review Agent Logs

```bash
# Check container logs
docker logs cyber-embedding-worker | tail -50

# Or systemd logs
journalctl -u cyber-embedding-worker -f

# Look for:
# [ERROR] Job processing failed
# [WARN] Memory usage at 85%
# [ERROR] Connection to Ollama lost
# [ERROR] Out of memory (OOM)
```

#### Step 3: Diagnose Common Issues

**Issue: Agent shows "Offline"**

```bash
# Check if container is running
docker ps | grep cyber-embedding-worker
# If not running: docker start cyber-embedding-worker

# Check network connectivity
curl -I http://cyber.company.com
# Should return HTTP 200

# Check credentials
echo $CYBER_AGENT_TOKEN | cut -d'.' -f1  # First part of JWT
# Should be valid token format
```

**Issue: High Memory Usage**

```bash
# Check current memory
docker stats cyber-embedding-worker
# Look for memory % and actual usage

# Reduce WORKER_THREADS
# Increase memory limit
docker update --memory 8g cyber-embedding-worker
docker restart cyber-embedding-worker
```

**Issue: Job Processing is Slow**

```bash
# Check embedding latency
curl http://localhost:8080/metrics | grep latency

# Check CPU usage
docker stats --no-stream cyber-embedding-worker
# If CPU < 50%, increase WORKER_THREADS
# If CPU > 95%, add more agents or reduce threads

# Check queue depth
curl http://localhost:8080/queue
# If queue > max_size, agent is overwhelmed
```

#### Step 4: Restart Agent

```bash
# Graceful restart (finish current jobs)
docker restart cyber-embedding-worker

# Verify it reconnects
sleep 10
docker logs cyber-embedding-worker | grep "registered"
# Should see: "Agent registered: research-embedder-abc123"

# Check in Cyber admin
# Status should be "Online" within 30 seconds
```

#### Step 5: Set Up Monitoring

```bash
# Export metrics for monitoring
docker run -d \
  -p 9090:9090 \
  -v /etc/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml \
  prom/prometheus:latest

# Prometheus config includes:
# - scrape_interval: 15s
# - targets: ['localhost:8080/metrics']
# - alert rules for high CPU, memory, queue depth

# Set up alerts
# If memory > 85% for 5 minutes → Page on-call
# If offline for > 2 minutes → Page on-call
# If queue depth > 500 → Alert (but don't page)
```

### Verification

Confirm monitoring is effective:

- [ ] Agent status is visible
- [ ] Logs can be accessed
- [ ] Metrics are being collected
- [ ] Issues are caught quickly
- [ ] Restart procedure works
- [ ] Agents recover automatically

---

## Summary: Quick Reference

### The 3 Workflows at a Glance

| Workflow | Purpose | Time | Frequency |
|----------|---------|------|-----------|
| **1. Deploy Agents** | Set up new workers | 15-30 min | Quarterly |
| **2. Configure Ollama** | Set up embedding service | 10-20 min | As needed |
| **3. Monitor Health** | Diagnose issues | 5-15 min | Daily |

### Your Agent Operating Cycle

```
1. Deploy Agent (once)
   ↓
2. Configure Ollama (once)
   ↓
3. Monitor Health (continuous)
   ↓
4. Optimize Performance (quarterly)
   ↓
5. Scale as Needed (annually)
```

---

## Related Personas

Your workflows overlap with:

- **[System Administrator](08-system-administrator.md)** — Register agents globally
- **[Organization Administrator](05-org-administrator.md)** — Configure agents per org
- **[Notebook Owner](06-notebook-owner.md)** — Monitor jobs agents process

---

**Last updated:** February 21, 2026
**Chapter version:** 1.0.0 (Beta)
**Platform version:** 2.1.0
