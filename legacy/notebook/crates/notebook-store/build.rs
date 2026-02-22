use std::fs;
use std::path::PathBuf;

/// SQL files needed at compile time (embedded via `include_str!` in schema.rs).
const MIGRATION_FILES: &[&str] = &[
    "002_schema.sql",
    "003_graph.sql",
    "004_coherence_links.sql",
    "006_notebook_sequence.sql",
];

fn main() {
    let manifest_dir = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    let out_dir = PathBuf::from(std::env::var("OUT_DIR").unwrap());

    // Candidate directories where migrations might live:
    // 1. Local dev: repo-root/infrastructure/postgres/migrations/server/ (current layout)
    // 2. Local dev: repo-root/infrastructure/postgres/migrations/ (flat, pre-reorganization)
    // 3. Legacy: repo-root/postgres/migrations/ (old repo structure)
    // 4. Docker build: workspace-root/migrations/server/ (COPY'd by Dockerfile, current)
    // 5. Docker build: workspace-root/migrations/ (COPY'd by Dockerfile, flat)
    let candidates: Vec<PathBuf> = vec![
        manifest_dir.join("../../../../infrastructure/postgres/migrations/server"),
        manifest_dir.join("../../../../infrastructure/postgres/migrations"),
        manifest_dir.join("../../../postgres/migrations"),
        manifest_dir.join("../../migrations/server"),
        manifest_dir.join("../../migrations"),
    ];

    let migrations_dir = candidates
        .iter()
        .find(|p| p.join(MIGRATION_FILES[0]).exists())
        .unwrap_or_else(|| {
            panic!(
                "Could not find migrations directory. Searched:\n{}",
                candidates
                    .iter()
                    .map(|p| format!("  - {}", p.display()))
                    .collect::<Vec<_>>()
                    .join("\n")
            )
        });

    let dest = out_dir.join("migrations");
    fs::create_dir_all(&dest).expect("failed to create OUT_DIR/migrations");

    for file in MIGRATION_FILES {
        let src = migrations_dir.join(file);
        let dst = dest.join(file);
        fs::copy(&src, &dst).unwrap_or_else(|e| {
            panic!("failed to copy {} -> {}: {}", src.display(), dst.display(), e)
        });
        println!("cargo:rerun-if-changed={}", src.canonicalize().unwrap().display());
    }
}
