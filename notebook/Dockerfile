# ---- Build stage ----
FROM rust:1.88-bookworm AS builder

WORKDIR /app

# Copy workspace manifests first for better layer caching
COPY Cargo.toml Cargo.lock ./
COPY crates/notebook-core/Cargo.toml crates/notebook-core/Cargo.toml
COPY crates/notebook-server/Cargo.toml crates/notebook-server/Cargo.toml
COPY crates/notebook-store/Cargo.toml crates/notebook-store/Cargo.toml
COPY crates/notebook-entropy/Cargo.toml crates/notebook-entropy/Cargo.toml
COPY cli/Cargo.toml cli/Cargo.toml

# Create dummy source files so cargo can resolve dependencies
RUN mkdir -p crates/notebook-core/src && echo "" > crates/notebook-core/src/lib.rs && \
    mkdir -p crates/notebook-server/src && echo "fn main() {}" > crates/notebook-server/src/main.rs && \
    mkdir -p crates/notebook-store/src && echo "" > crates/notebook-store/src/lib.rs && \
    mkdir -p crates/notebook-entropy/src && echo "" > crates/notebook-entropy/src/lib.rs && \
    mkdir -p cli/src && echo "fn main() {}" > cli/src/main.rs

# Cache dependency build
RUN cargo build --release --bin notebook-server 2>/dev/null || true

# Copy real source code
COPY crates/ crates/
COPY cli/ cli/

# Touch source files to invalidate cached dummy builds
RUN find crates/ cli/ -name "*.rs" -exec touch {} +

# Build the actual binary
RUN cargo build --release --bin notebook-server

# ---- Runtime stage ----
FROM debian:bookworm-slim

RUN apt-get update && \
    apt-get install -y --no-install-recommends ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# Copy the compiled binary
COPY --from=builder /app/target/release/notebook-server /usr/local/bin/

# Copy migrations so the server can run them on startup
COPY migrations/ /app/migrations/

EXPOSE 3000

ENV PORT=3000
ENV LOG_LEVEL=info
ENV DATABASE_RUN_MIGRATIONS=true

CMD ["notebook-server"]
