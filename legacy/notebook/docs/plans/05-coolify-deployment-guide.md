# Plan 05: Coolify Deployment Guide

Step-by-step guide to deploy the three services (notebook-server, admin, PostgreSQL/AGE) to Coolify.

## Current State

| Component | Dockerfile | Docker-ready | Coolify-ready |
|---|---|---|---|
| **notebook-server** (Rust, port 3000) | Yes | Yes | Almost |
| **admin** (Blazor .NET 10, port 5000) | **No** | No | No |
| **PostgreSQL/AGE** | N/A (uses image) | Yes | Almost |

## Architecture

```
Internet
    |
    v
Traefik (Coolify-managed, auto TLS)
    |
    +-- cyber.nassau-records.de --> admin (Blazor Server, port 5000)
    |
    +-- notebook.nassau-records.de --> notebook-server (Rust/Axum, port 3000)
    |
    +-- (internal network only)
            |
            +-- PostgreSQL (Apache AGE, port 5432)
                  ├── database: notebook       (used by notebook-server)
                  └── database: notebook_admin (used by admin)
```

All three services share a Coolify internal network. PostgreSQL is never exposed publicly.

## Step 1: Create `admin/Dockerfile`

This is the biggest blocker — the admin service has no Dockerfile yet.

### File: `admin/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "NotebookAdmin.dll"]
```

### File: `admin/.dockerignore`

```
bin/
obj/
*.user
*.suo
.vs/
appsettings.Development.json
```

### Verify locally

```bash
cd admin/
docker build -t notebook-admin .
docker run --rm -p 5000:5000 notebook-admin
```

## Step 2: PostgreSQL — Two Databases

The current setup creates only the `notebook` database. The admin service requires a separate `notebook_admin` database.

### Option A (Recommended): Init script

Create `notebook/migrations/000_create_admin_db.sql`:

```sql
SELECT 'CREATE DATABASE notebook_admin'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'notebook_admin')\gexec
```

Mount it as the first init script in docker-compose:

```yaml
volumes:
  - ./migrations/000_create_admin_db.sql:/docker-entrypoint-initdb.d/00-admin-db.sql:ro
```

### Option B: Separate PostgreSQL instance

Use a second PostgreSQL service in Coolify. Simpler to configure but uses more resources.

## Step 3: Generate Production Secrets

### Ed25519 keypair for JWT signing

The admin service signs JWTs; notebook-server verifies them. They must share the same keypair.

```bash
# Generate keypair
openssl genpkey -algorithm ed25519 -out jwt-private.pem
openssl pkey -in jwt-private.pem -pubout -out jwt-public.pem

# For the admin service (Jwt__PrivateKey): base64-encode the private key PEM
base64 -i jwt-private.pem   # → set as Jwt__PrivateKey in admin

# For notebook-server (JWT_PUBLIC_KEY): use the PEM text DIRECTLY (not base64-encoded)
# The Rust server calls DecodingKey::from_ed_pem(), which expects PEM with headers:
#   -----BEGIN PUBLIC KEY-----
#   ...
#   -----END PUBLIC KEY-----
cat jwt-public.pem           # → set as JWT_PUBLIC_KEY in notebook-server (raw PEM text)
```

> **Important:** `JWT_PUBLIC_KEY` for notebook-server must be the raw PEM text (with `-----BEGIN PUBLIC KEY-----` header), NOT base64-encoded. The admin's `Jwt__PrivateKey` is base64-encoded PKCS#8 DER — these are different formats.

### Strong PostgreSQL password

```bash
openssl rand -hex 16
```

Store all secrets in Coolify's environment variable UI (mark as sensitive). Never commit them to git.

## Step 4: DNS Configuration

Point both domains to the Coolify server IP:

```
notebook.nassau-records.de   A  <coolify-server-ip>
cyber.nassau-records.de      A  <coolify-server-ip>
```

Coolify/Traefik handles TLS certificates via Let's Encrypt automatically.

## Step 5: Coolify Service Configuration

Create three services on the **same Coolify internal network**.

### Service 1: PostgreSQL (internal only)

| Setting | Value |
|---|---|
| **Type** | Docker Image |
| **Image** | `apache/age:PG16_latest` |
| **Port** | 5432 |
| **Public domain** | None (internal only) |
| **Volume** | Persistent volume → `/var/lib/postgresql/data` |
| **Health check** | `pg_isready -U notebook -d notebook` |

**Environment variables:**

| Variable | Value |
|---|---|
| `POSTGRES_USER` | `notebook` |
| `POSTGRES_PASSWORD` | `<generated-strong-password>` |
| `POSTGRES_DB` | `notebook` |

**Init scripts:** Mount the SQL migration files as volumes into `/docker-entrypoint-initdb.d/` (ordered by filename):

1. `000_create_admin_db.sql` — creates the `notebook_admin` database
2. `init.sql` — enables Apache AGE extension, creates graph
3. `002_schema.sql` — core tables
4. `003_graph.sql` — graph schema

> **Note:** Init scripts only run on first startup (empty data volume). For existing databases, run migrations manually.

### Service 2: notebook-server (public API)

| Setting | Value |
|---|---|
| **Type** | Dockerfile |
| **Source** | Git repo, Dockerfile at `notebook/Dockerfile`, build context `notebook/` |
| **Port** | 3000 |
| **Public domain** | `notebook.nassau-records.de` |
| **Health check** | `GET /health` → 200 |

**Environment variables:**

| Variable | Value | Notes |
|---|---|---|
| `DATABASE_URL` | `postgres://notebook:<password>@<postgres-hostname>:5432/notebook` | Use Coolify internal hostname for postgres |
| `PORT` | `3000` | |
| `LOG_LEVEL` | `info` | |
| `DATABASE_RUN_MIGRATIONS` | `true` | Runs sqlx migrations on startup |
| `JWT_PUBLIC_KEY` | `<ed25519-public-key-PEM-text>` | Raw PEM text (with BEGIN/END headers), must match the admin's private key |
| `CORS_ALLOWED_ORIGINS` | `https://cyber.nassau-records.de` | Restrict to admin domain |
| `ALLOW_DEV_IDENTITY` | `false` | Disable X-Author-Id header in production |
| `ENFORCE_SCOPES` | `true` | Enforce JWT scope checking |

### Service 3: admin (public frontend)

| Setting | Value |
|---|---|
| **Type** | Dockerfile |
| **Source** | Git repo, Dockerfile at `admin/Dockerfile`, build context `admin/` |
| **Port** | 5000 |
| **Public domain** | `cyber.nassau-records.de` |

**Environment variables:**

| Variable | Value | Notes |
|---|---|---|
| `ASPNETCORE_URLS` | `http://+:5000` | |
| `ASPNETCORE_ENVIRONMENT` | `Production` | |
| `ConnectionStrings__DefaultConnection` | `Host=<postgres-hostname>;Database=notebook_admin;Username=notebook;Password=<password>` | Same PG instance, different database |
| `NotebookApi__BaseUrl` | `http://<notebook-server-hostname>:3000` | Internal Coolify hostname |
| `Jwt__Issuer` | `notebook-admin` | |
| `Jwt__ExpiryMinutes` | `60` | |
| `Jwt__PrivateKey` | `<base64-encoded-ed25519-private-key>` | Signs JWTs for the Rust API |
| `Jwt__PublicKey` | `<base64-encoded-ed25519-public-key>` | |

## Step 6: Deployment Order

Deploy in this order — each service depends on the previous:

1. **PostgreSQL** — wait for healthy status, verify both databases exist
2. **notebook-server** — runs sqlx migrations on startup, verify `/health` returns 200
3. **admin** — runs EF Core migrations on startup, verify the Blazor UI loads

## Step 7: Verification Checklist

### Infrastructure

- [ ] PostgreSQL container is healthy (`pg_isready`)
- [ ] Both databases exist (`notebook` and `notebook_admin`)
- [ ] notebook-server container is running
- [ ] admin container is running
- [ ] All three containers can reach each other on internal network

### API (notebook-server)

- [ ] `GET https://notebook.nassau-records.de/health` returns 200
- [ ] Unauthenticated requests return 401
- [ ] JWT from admin's `/auth/token` endpoint is accepted by notebook-server

### Frontend (admin)

- [ ] `https://cyber.nassau-records.de` loads the Blazor app
- [ ] Login page accepts credentials
- [ ] Dashboard shows notebooks after login
- [ ] `/auth/register` endpoint creates users
- [ ] `/auth/token` endpoint returns valid JWTs

### Security

- [ ] PostgreSQL is not reachable from the internet
- [ ] CORS is restricted to `https://cyber.nassau-records.de`
- [ ] `ALLOW_DEV_IDENTITY` is `false`
- [ ] JWT keys are not committed to git
- [ ] TLS is active on both public domains

## Rollback Plan

| Issue | Resolution |
|---|---|
| Database migration failure | sqlx and EF Core migrations are idempotent — safe to re-run. Restore from PG backup if data is corrupted. |
| notebook-server crash | Check logs. Common causes: missing `JWT_PUBLIC_KEY`, wrong `DATABASE_URL`. Roll back to previous image in Coolify. |
| admin can't reach API | Verify `NotebookApi__BaseUrl` uses the correct internal hostname. Test with `curl` from inside the container. |
| TLS certificate failure | Ensure DNS is pointing to Coolify server. Check Traefik logs. Coolify retries Let's Encrypt automatically. |

## Post-Deployment

1. Set up PostgreSQL backups (Coolify has built-in backup scheduling)
2. Monitor server logs for auth failures
3. Set up uptime monitoring for both public domains
4. Remove dev keys from `appsettings.Development.json` if the repo is public
