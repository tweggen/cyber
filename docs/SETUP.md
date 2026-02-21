# Development Setup & Build Instructions

Getting started with Cyber development.

## Prerequisites

- **.NET 10+** (for frontend and backend)
- **Rust 1.80+** (optional, for legacy backend reference)
- **Docker & Docker Compose**
- **PostgreSQL** (via Docker)
- **Python 3.9+** (optional, for utilities)

## Quick Start

### 1. Clone & Navigate
```bash
git clone https://github.com/tweggen/cyber.git
cd cyber
```

### 2. Start Database
```bash
docker compose -f infrastructure/docker-compose.yml up -d
```

### 3. Build Frontend
```bash
cd frontend/admin
dotnet restore
dotnet build
dotnet run
# Open http://localhost:5000
```

### 4. Build Backend
```bash
cd backend/src/Notebook.Server
dotnet restore
dotnet build
dotnet run
# Listens on http://localhost:5201 (or check configuration)
```

## Development Workflow

### Building

**Frontend:**
```bash
cd frontend/admin
dotnet build                    # Compile
dotnet build -c Release         # Production build
```

**Backend:**
```bash
cd backend/src
dotnet build                    # Build all projects
dotnet build -c Release         # Production build
```

**Legacy (Rust - Reference Only):**
```bash
cd legacy/notebook
cargo build                     # Build all crates
cargo build --release          # Optimized build
```

### Running

**Frontend (Development):**
```bash
cd frontend/admin
dotnet run
```

**Backend (Development):**
```bash
cd backend/src/Notebook.Server
dotnet run
```

**Legacy HTTP Server:**
```bash
cd legacy/notebook
cargo run --bin notebook-server
```

### Testing

**Frontend & Backend:**
```bash
cd backend
dotnet test                                    # Run all tests
dotnet test --filter "Category=Integration"  # Integration tests only
```

**Legacy:**
```bash
cd legacy/notebook
cargo test                      # Run all tests
cargo test -p notebook-entropy # Single crate
```

### Code Quality

**Format Check:**
```bash
cd frontend/admin && dotnet format --verify-no-changes
cd backend && dotnet format --verify-no-changes
cd legacy/notebook && cargo fmt --check
```

**Linting:**
```bash
# .NET (via Roslyn analyzers in build)
cd backend && dotnet build /p:TreatWarningsAsErrors=true

# Rust
cd legacy/notebook && cargo clippy -- -D warnings
```

## Infrastructure

### Docker Compose

Start the full stack:
```bash
docker compose -f infrastructure/docker-compose.yml up -d
```

Services started:
- PostgreSQL (port 5432)
- Apache AGE graph extension

### Database Initialization

The database initializes automatically with Docker Compose. For manual setup:
```bash
cd infrastructure/postgres
./init-thinktank.sh
```

Migrations apply automatically when the backend starts.

## Configuration

### Backend Configuration
Edit `backend/src/Notebook.Server/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=thinktank;..."
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Frontend Configuration
Edit `frontend/admin/appsettings.json`:
```json
{
  "NotebookApi": {
    "BaseUrl": "http://localhost:5201",
    "McpType": "notebook"
  }
}
```

## Troubleshooting

### Database Connection Issues
```bash
# Verify PostgreSQL is running
docker ps | grep postgres

# Check logs
docker logs <container-id>

# Restart services
docker compose -f infrastructure/docker-compose.yml restart
```

### Port Already in Use
- Frontend: Change port in `frontend/admin/Properties/launchSettings.json`
- Backend: Change port in `backend/src/Notebook.Server/appsettings.json`

### Build Failures
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build

# Clear NuGet cache
dotnet nuget locals all --clear
```

## Common Tasks

### Add a New NuGet Package
```bash
cd backend
dotnet add [PROJECT] package [PACKAGE-NAME]
```

### Run a Specific Test
```bash
cd backend
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

### View Backend API Documentation
The Swagger UI is available at `http://localhost:5201/swagger` (if enabled).

### Check Feature Coverage
See [10-USER-FACING-FEATURES.md](architecture/10-USER-FACING-FEATURES.md) for the latest implementation status (currently 13/16 = 81%).

## Next Steps

1. Read [ARCHITECTURE.md](ARCHITECTURE.md) for system overview
2. Check [docs/roadmap/README.md](roadmap/README.md) for current work items
3. See [../backend/README.md](../backend/README.md) for backend-specific details
4. Read [../CLAUDE.md](../CLAUDE.md) for development philosophy

---

**Need help?** Check the detailed documentation in the `architecture/` directory or the feature matrix in `10-USER-FACING-FEATURES.md`.
