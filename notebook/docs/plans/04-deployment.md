# Plan 04: Deployment

Coolify configuration, docker-compose updates, environment variables, Traefik routing, verification checklist.

## Prerequisites

- Plans 01-03 fully implemented and tested locally
- Both `notebook-server` and `notebook-frontend` Docker images build successfully
- Coolify instance running with Traefik reverse proxy

## Architecture

```
Internet
    |
    v
Traefik (Coolify-managed)
    |
    +-- cyber.nassau-records.de --> notebook-frontend (Blazor, port 5000)
    |
    +-- api.cyber.nassau-records.de --> notebook-server (Rust/Axum, port 3000)
    |
    +-- (internal network only)
            |
            +-- PostgreSQL (Apache AGE, port 5432)
```

Both services share a Coolify internal network so the frontend can reach the API via internal hostname.

## Step 1: Update docker-compose.yml for Local Development

### File: `notebook/docker-compose.yml`

Replace with a full local development stack:

```yaml
version: "3.9"

services:
  postgres:
    image: apache/age:PG16_latest
    container_name: notebook-postgres
    environment:
      POSTGRES_USER: notebook
      POSTGRES_PASSWORD: notebook_dev
      POSTGRES_DB: notebook
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./migrations/init.sql:/docker-entrypoint-initdb.d/01-init.sql:ro
      - ./migrations/002_schema.sql:/docker-entrypoint-initdb.d/02-schema.sql:ro
      - ./migrations/003_graph.sql:/docker-entrypoint-initdb.d/03-graph.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U notebook -d notebook"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  notebook-server:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: notebook-server
    ports:
      - "3000:3000"
    environment:
      DATABASE_URL: postgres://notebook:notebook_dev@postgres:5432/notebook
      PORT: "3000"
      LOG_LEVEL: info
      DATABASE_RUN_MIGRATIONS: "true"
      JWT_SECRET: "${JWT_SECRET:-dev_secret_change_me_in_production_32chars}"
      JWT_EXPIRY_HOURS: "24"
      ADMIN_USERNAME: "${ADMIN_USERNAME:-admin}"
      ADMIN_PASSWORD: "${ADMIN_PASSWORD:-adminpass123}"
      CORS_ALLOWED_ORIGINS: "*"
    depends_on:
      postgres:
        condition: service_healthy
    restart: unless-stopped

  notebook-frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: notebook-frontend
    ports:
      - "5000:5000"
    environment:
      ASPNETCORE_URLS: "http://+:5000"
      NOTEBOOK_API_URL: "http://notebook-server:3000"
    depends_on:
      - notebook-server
    restart: unless-stopped

volumes:
  postgres_data:
    driver: local
```

### File: `notebook/.env` (NEW — git-ignored, for local dev)

```bash
JWT_SECRET=dev_secret_change_me_in_production_at_least_32_chars
ADMIN_USERNAME=admin
ADMIN_PASSWORD=adminpass123
```

Add to `.gitignore` (if not already):

```
.env
```

## Step 2: Verify Docker Builds

### Build notebook-server

```bash
cd notebook/
docker build -t notebook-server -f Dockerfile .
```

Expected: builds successfully, image contains `/usr/local/bin/notebook-server`.

### Build notebook-frontend

```bash
cd notebook/frontend/
docker build -t notebook-frontend -f Dockerfile .
```

Expected: builds successfully, image contains `/app/NotebookFrontend.dll`.

### Run full stack locally

```bash
cd notebook/
docker-compose up -d
```

Wait for all services to be healthy:

```bash
docker-compose ps
```

Expected: postgres (healthy), notebook-server (running), notebook-frontend (running).

## Step 3: Coolify Configuration

### Service 1: PostgreSQL (if not already deployed)

- **Type**: Docker image
- **Image**: `apache/age:PG16_latest`
- **Port**: 5432
- **Environment variables**:
  - `POSTGRES_USER=notebook`
  - `POSTGRES_PASSWORD=<strong-password>`
  - `POSTGRES_DB=notebook`
- **Volume**: Persistent volume for `/var/lib/postgresql/data`
- **Network**: Internal Coolify network (e.g., `notebook-net`)
- **No public domain** — internal only

### Service 2: notebook-server

- **Type**: Dockerfile
- **Source**: Git repo, path `notebook/Dockerfile`, build context `notebook/`
- **Port**: 3000
- **Domain**: `api.cyber.nassau-records.de`
- **Network**: Same Coolify internal network as PostgreSQL
- **Environment variables**:

| Variable | Value | Notes |
|----------|-------|-------|
| `DATABASE_URL` | `postgres://notebook:<password>@<postgres-hostname>:5432/notebook` | Use Coolify internal hostname |
| `PORT` | `3000` | |
| `LOG_LEVEL` | `info` | |
| `DATABASE_RUN_MIGRATIONS` | `true` | |
| `JWT_SECRET` | `<generate-strong-random-64-char-string>` | **Required for production** |
| `JWT_EXPIRY_HOURS` | `24` | |
| `ADMIN_USERNAME` | `admin` | Only needed on first deploy |
| `ADMIN_PASSWORD` | `<strong-password>` | Only needed on first deploy |
| `CORS_ALLOWED_ORIGINS` | `https://cyber.nassau-records.de` | Restrict in production |

- **Health check**: `GET /health` should return 200
- **Traefik labels** (Coolify sets these automatically):
  - Route: `Host(\`api.cyber.nassau-records.de\`)`
  - TLS: enabled (Let's Encrypt)

### Service 3: notebook-frontend

- **Type**: Dockerfile
- **Source**: Git repo, path `notebook/frontend/Dockerfile`, build context `notebook/frontend/`
- **Port**: 5000
- **Domain**: `cyber.nassau-records.de`
- **Network**: Same Coolify internal network
- **Environment variables**:

| Variable | Value | Notes |
|----------|-------|-------|
| `ASPNETCORE_URLS` | `http://+:5000` | |
| `NOTEBOOK_API_URL` | `http://<notebook-server-hostname>:3000` | Internal Coolify hostname |

- **Traefik labels** (Coolify sets automatically):
  - Route: `Host(\`cyber.nassau-records.de\`)`
  - TLS: enabled (Let's Encrypt)

### Important Coolify Notes

1. **Internal networking**: Ensure all three services are on the same Coolify network so they can reach each other by container hostname.
2. **Build context**: For `notebook-server`, the Dockerfile is at `notebook/Dockerfile` and the build context must be `notebook/` (so it can COPY crates/, migrations/, etc.).
3. **Persistent storage**: PostgreSQL volume must persist across redeployments.
4. **JWT_SECRET**: Generate once, store in Coolify secrets. Must be the same across server restarts or all existing tokens become invalid.
5. **Admin bootstrap**: `ADMIN_USERNAME`/`ADMIN_PASSWORD` only create a user if the users table is empty. After first deployment, these can be removed (but leaving them is safe).

## Step 4: Generate JWT Secret

Generate a strong random secret for production:

```bash
openssl rand -hex 32
```

This produces a 64-character hex string. Store it as `JWT_SECRET` in Coolify's environment variables (mark as secret/sensitive).

## Step 5: DNS Configuration

Ensure DNS records point to the Coolify server:

```
api.cyber.nassau-records.de  A  <coolify-server-ip>
cyber.nassau-records.de      A  <coolify-server-ip>
```

Or if using a wildcard:

```
*.cyber.nassau-records.de    A  <coolify-server-ip>
```

## Step 6: Deploy

1. Push code to the git repository
2. In Coolify, trigger builds for both services
3. Wait for builds to complete
4. Verify services are running in Coolify dashboard

## Step 7: Verification Checklist

### Infrastructure

- [ ] PostgreSQL container is healthy (`pg_isready`)
- [ ] notebook-server container is running
- [ ] notebook-frontend container is running
- [ ] All three containers can reach each other on internal network

### API (notebook-server)

- [ ] `GET https://api.cyber.nassau-records.de/health` returns 200
- [ ] `GET https://api.cyber.nassau-records.de/notebooks` returns 401 (no auth)
- [ ] `POST https://api.cyber.nassau-records.de/api/auth/login` with admin credentials returns JWT
- [ ] `GET https://api.cyber.nassau-records.de/notebooks` with JWT returns 200
- [ ] `GET https://api.cyber.nassau-records.de/api/users` with admin JWT returns user list
- [ ] `POST https://api.cyber.nassau-records.de/api/users` creates a new user
- [ ] `GET https://api.cyber.nassau-records.de/api/usage` returns usage log

### Frontend (notebook-frontend)

- [ ] `https://cyber.nassau-records.de` loads the Blazor app
- [ ] Login page accepts admin credentials
- [ ] Dashboard shows notebooks after login
- [ ] Users page is accessible for admin
- [ ] Can create a new user via UI
- [ ] Can create a new notebook via UI
- [ ] Notebook detail page shows metadata and participants
- [ ] Usage log page shows audit trail
- [ ] Logout button works

### End-to-End Flow

1. [ ] Login as admin via frontend
2. [ ] Create a new user "alice" via Users page
3. [ ] Login as alice (logout first, then login)
4. [ ] Create a notebook as alice
5. [ ] Login as admin, verify alice's notebook appears in admin view
6. [ ] Check usage log shows all actions (create_user, create_notebook)
7. [ ] Grant access to a notebook, verify participant list updates
8. [ ] Revoke access, verify participant removed

### Security

- [ ] All API routes (except `/health` and `/api/auth/login`) require JWT
- [ ] Expired JWT returns 401
- [ ] Non-admin users cannot access `/api/users` (403)
- [ ] Non-admin users can only see their own usage log
- [ ] CORS is properly restricted in production
- [ ] JWT_SECRET is not committed to git
- [ ] Password hashes use Argon2 (not plaintext)

### Performance

- [ ] Login response time < 500ms
- [ ] Notebook list response time < 200ms
- [ ] Frontend page load time < 3s

## Rollback Plan

If deployment fails:

1. **Database migration issue**: The `005_users.sql` migration uses `CREATE TABLE IF NOT EXISTS` — it's safe to re-run. If data is corrupted, restore from PostgreSQL backup.
2. **notebook-server crash**: Check logs (`docker logs notebook-server`). Common issues: missing `JWT_SECRET`, database connection failed. Roll back to previous image in Coolify.
3. **Frontend can't reach API**: Check `NOTEBOOK_API_URL` env var, verify internal network connectivity (`docker exec notebook-frontend curl http://notebook-server:3000/health`).

## Files Changed Summary

| File | Action |
|------|--------|
| `notebook/docker-compose.yml` | Modified — add server and frontend services |
| `notebook/.env` | New (git-ignored) — local dev environment variables |
| `notebook/frontend/Dockerfile` | New (created in Plan 03) |

## Post-Deployment Tasks

1. Remove `ADMIN_USERNAME` and `ADMIN_PASSWORD` from environment after first successful login (optional, safe to leave)
2. Set up PostgreSQL backups in Coolify
3. Monitor server logs for auth failures (possible brute force)
4. Set up uptime monitoring for both domains
