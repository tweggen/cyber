//! Catalog caching with intelligent invalidation.
//!
//! This module provides a thread-safe cache for generated catalogs with
//! configurable invalidation rules based on catalog shift and age.
//!
//! ## Features
//!
//! - Thread-safe access via `Arc<RwLock<>>`
//! - Configurable shift threshold for invalidation
//! - Time-based expiration with configurable max age
//! - Stale-while-revalidate pattern support
//!
//! ## Example
//!
//! ```rust,ignore
//! use notebook_entropy::cache::{CatalogCache, CacheConfig};
//! use notebook_entropy::catalog::Catalog;
//! use notebook_core::types::NotebookId;
//!
//! // Create cache with default config
//! let cache = CatalogCache::new();
//!
//! // Store a catalog
//! cache.set(notebook_id, catalog.clone(), 42);
//!
//! // Retrieve from cache
//! if let Some(cached) = cache.get(&notebook_id) {
//!     if !cached.is_stale(&cache.config()) {
//!         // Use fresh catalog
//!     }
//! }
//!
//! // Invalidate on high-shift write
//! cache.invalidate_if_stale(&notebook_id, 0.15); // Will invalidate (> 0.1 threshold)
//! ```
//!
//! Owned by: agent-cache (Task 3-4)

use crate::catalog::Catalog;
use notebook_core::types::NotebookId;
use std::collections::HashMap;
use std::sync::{Arc, RwLock};
use std::time::{Duration, Instant};

/// Default threshold for catalog shift invalidation.
/// Invalidate cache when write causes shift > 0.1.
pub const DEFAULT_SHIFT_THRESHOLD: f64 = 0.1;

/// Default maximum cache age in seconds (5 minutes).
pub const DEFAULT_MAX_AGE_SECS: u64 = 300;

/// Configuration for cache behavior.
#[derive(Debug, Clone, Copy)]
pub struct CacheConfig {
    /// Catalog shift threshold for invalidation.
    /// If a write causes `catalog_shift > shift_threshold`, the cache is invalidated.
    pub shift_threshold: f64,

    /// Maximum age of cache entries in seconds.
    /// Entries older than this are considered expired.
    pub max_age_secs: u64,

    /// Grace period for stale-while-revalidate in seconds.
    /// After max_age, entries are stale but still serveable for this duration.
    pub stale_grace_secs: u64,
}

impl Default for CacheConfig {
    fn default() -> Self {
        Self {
            shift_threshold: DEFAULT_SHIFT_THRESHOLD,
            max_age_secs: DEFAULT_MAX_AGE_SECS,
            stale_grace_secs: 60, // 1 minute grace period
        }
    }
}

impl CacheConfig {
    /// Creates a new configuration with custom settings.
    pub fn new(shift_threshold: f64, max_age_secs: u64) -> Self {
        Self {
            shift_threshold,
            max_age_secs,
            stale_grace_secs: 60,
        }
    }

    /// Sets the stale grace period.
    pub fn with_stale_grace(mut self, secs: u64) -> Self {
        self.stale_grace_secs = secs;
        self
    }
}

/// Status of a cached catalog.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CacheStatus {
    /// Cache entry is fresh and valid.
    Fresh,
    /// Cache entry is stale but can be served while revalidating.
    Stale,
    /// Cache entry has expired and should not be served.
    Expired,
}

/// A cached catalog with metadata.
#[derive(Debug, Clone)]
pub struct CachedCatalog {
    /// The cached catalog.
    pub catalog: Catalog,

    /// When the catalog was cached.
    pub cached_at: Instant,

    /// The sequence number when the catalog was generated.
    pub cached_at_sequence: u64,
}

impl CachedCatalog {
    /// Creates a new cached catalog.
    pub fn new(catalog: Catalog, sequence: u64) -> Self {
        Self {
            catalog,
            cached_at: Instant::now(),
            cached_at_sequence: sequence,
        }
    }

    /// Returns the age of this cache entry.
    pub fn age(&self) -> Duration {
        self.cached_at.elapsed()
    }

    /// Returns the age in seconds.
    pub fn age_secs(&self) -> u64 {
        self.age().as_secs()
    }

    /// Determines the status of this cache entry.
    pub fn status(&self, config: &CacheConfig) -> CacheStatus {
        let age_secs = self.age_secs();

        if age_secs <= config.max_age_secs {
            CacheStatus::Fresh
        } else if age_secs <= config.max_age_secs + config.stale_grace_secs {
            CacheStatus::Stale
        } else {
            CacheStatus::Expired
        }
    }

    /// Returns true if this entry is stale (but potentially still serveable).
    pub fn is_stale(&self, config: &CacheConfig) -> bool {
        matches!(
            self.status(config),
            CacheStatus::Stale | CacheStatus::Expired
        )
    }

    /// Returns true if this entry has fully expired.
    pub fn is_expired(&self, config: &CacheConfig) -> bool {
        self.status(config) == CacheStatus::Expired
    }
}

/// Thread-safe catalog cache.
///
/// The cache stores generated catalogs keyed by notebook ID, with
/// automatic invalidation based on catalog shift and age.
#[derive(Debug, Clone)]
pub struct CatalogCache {
    /// The cached catalogs.
    cache: Arc<RwLock<HashMap<NotebookId, CachedCatalog>>>,

    /// Cache configuration.
    config: CacheConfig,
}

impl Default for CatalogCache {
    fn default() -> Self {
        Self::new()
    }
}

impl CatalogCache {
    /// Creates a new catalog cache with default configuration.
    pub fn new() -> Self {
        Self {
            cache: Arc::new(RwLock::new(HashMap::new())),
            config: CacheConfig::default(),
        }
    }

    /// Creates a catalog cache with custom configuration.
    pub fn with_config(config: CacheConfig) -> Self {
        Self {
            cache: Arc::new(RwLock::new(HashMap::new())),
            config,
        }
    }

    /// Returns the cache configuration.
    pub fn config(&self) -> &CacheConfig {
        &self.config
    }

    /// Gets a cached catalog for a notebook.
    ///
    /// Returns `None` if not cached or if the entry has fully expired.
    /// Returns stale entries (caller should check status and potentially
    /// trigger background revalidation).
    pub fn get(&self, notebook_id: &NotebookId) -> Option<CachedCatalog> {
        let cache = self.cache.read().ok()?;
        let entry = cache.get(notebook_id)?;

        // Don't return fully expired entries
        if entry.is_expired(&self.config) {
            return None;
        }

        Some(entry.clone())
    }

    /// Gets a cached catalog with its status.
    ///
    /// Returns `(cached_catalog, status)` if present and not expired.
    pub fn get_with_status(
        &self,
        notebook_id: &NotebookId,
    ) -> Option<(CachedCatalog, CacheStatus)> {
        let cache = self.cache.read().ok()?;
        let entry = cache.get(notebook_id)?;

        let status = entry.status(&self.config);

        // Don't return fully expired entries
        if status == CacheStatus::Expired {
            return None;
        }

        Some((entry.clone(), status))
    }

    /// Stores a catalog in the cache.
    ///
    /// # Arguments
    ///
    /// * `notebook_id` - The notebook to cache for
    /// * `catalog` - The generated catalog
    /// * `sequence` - The current sequence number for staleness tracking
    pub fn set(&self, notebook_id: NotebookId, catalog: Catalog, sequence: u64) {
        if let Ok(mut cache) = self.cache.write() {
            cache.insert(notebook_id, CachedCatalog::new(catalog, sequence));
        }
    }

    /// Removes a catalog from the cache.
    pub fn invalidate(&self, notebook_id: &NotebookId) -> bool {
        if let Ok(mut cache) = self.cache.write() {
            return cache.remove(notebook_id).is_some();
        }
        false
    }

    /// Conditionally invalidates based on catalog shift.
    ///
    /// Invalidates the cache entry if `catalog_shift > config.shift_threshold`.
    ///
    /// # Returns
    ///
    /// `true` if the cache was invalidated, `false` otherwise.
    pub fn invalidate_if_stale(&self, notebook_id: &NotebookId, catalog_shift: f64) -> bool {
        if catalog_shift > self.config.shift_threshold {
            self.invalidate(notebook_id)
        } else {
            false
        }
    }

    /// Returns true if a cached entry exists and is fresh.
    pub fn is_fresh(&self, notebook_id: &NotebookId) -> bool {
        if let Ok(cache) = self.cache.read() {
            if let Some(entry) = cache.get(notebook_id) {
                return entry.status(&self.config) == CacheStatus::Fresh;
            }
        }
        false
    }

    /// Returns true if a cached entry needs revalidation.
    ///
    /// This is true if:
    /// - No entry exists
    /// - Entry is stale
    /// - Entry is expired
    pub fn needs_revalidation(&self, notebook_id: &NotebookId) -> bool {
        if let Ok(cache) = self.cache.read() {
            if let Some(entry) = cache.get(notebook_id) {
                return entry.is_stale(&self.config);
            }
        }
        true // No entry = needs validation
    }

    /// Clears all cached entries.
    pub fn clear(&self) {
        if let Ok(mut cache) = self.cache.write() {
            cache.clear();
        }
    }

    /// Returns the number of cached entries.
    pub fn len(&self) -> usize {
        self.cache.read().map(|c| c.len()).unwrap_or(0)
    }

    /// Returns true if the cache is empty.
    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }

    /// Removes all expired entries from the cache.
    ///
    /// Returns the number of entries removed.
    pub fn evict_expired(&self) -> usize {
        if let Ok(mut cache) = self.cache.write() {
            let before = cache.len();
            cache.retain(|_, entry| !entry.is_expired(&self.config));
            before - cache.len()
        } else {
            0
        }
    }

    /// Returns statistics about the cache.
    pub fn stats(&self) -> CacheStats {
        if let Ok(cache) = self.cache.read() {
            let total = cache.len();
            let mut fresh = 0;
            let mut stale = 0;
            let mut expired = 0;

            for entry in cache.values() {
                match entry.status(&self.config) {
                    CacheStatus::Fresh => fresh += 1,
                    CacheStatus::Stale => stale += 1,
                    CacheStatus::Expired => expired += 1,
                }
            }

            CacheStats {
                total,
                fresh,
                stale,
                expired,
            }
        } else {
            CacheStats::default()
        }
    }
}

/// Statistics about cache state.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct CacheStats {
    /// Total number of entries.
    pub total: usize,
    /// Number of fresh entries.
    pub fresh: usize,
    /// Number of stale entries.
    pub stale: usize,
    /// Number of expired entries.
    pub expired: usize,
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::catalog::{Catalog, ClusterSummary};
    use notebook_core::types::{CausalPosition, EntryId};

    fn make_test_catalog(entropy: f64) -> Catalog {
        Catalog {
            clusters: vec![ClusterSummary {
                topic: "test topic".to_string(),
                summary: "Test summary.".to_string(),
                entry_count: 5,
                cumulative_cost: 1.5,
                stability: 10,
                representative_entry_ids: vec![EntryId::new()],
            }],
            notebook_entropy: entropy,
            total_entries: 100,
            generated_at: CausalPosition::first(),
        }
    }

    #[test]
    fn cache_config_default() {
        let config = CacheConfig::default();
        assert_eq!(config.shift_threshold, DEFAULT_SHIFT_THRESHOLD);
        assert_eq!(config.max_age_secs, DEFAULT_MAX_AGE_SECS);
        assert_eq!(config.stale_grace_secs, 60);
    }

    #[test]
    fn cache_config_custom() {
        let config = CacheConfig::new(0.2, 600);
        assert_eq!(config.shift_threshold, 0.2);
        assert_eq!(config.max_age_secs, 600);
    }

    #[test]
    fn cache_config_with_stale_grace() {
        let config = CacheConfig::default().with_stale_grace(120);
        assert_eq!(config.stale_grace_secs, 120);
    }

    #[test]
    fn cached_catalog_new() {
        let catalog = make_test_catalog(5.0);
        let cached = CachedCatalog::new(catalog.clone(), 42);

        assert_eq!(cached.cached_at_sequence, 42);
        assert_eq!(cached.catalog.notebook_entropy, 5.0);
        assert!(cached.age_secs() < 1); // Just created
    }

    #[test]
    fn cached_catalog_status_fresh() {
        let catalog = make_test_catalog(5.0);
        let cached = CachedCatalog::new(catalog, 42);
        let config = CacheConfig::default();

        assert_eq!(cached.status(&config), CacheStatus::Fresh);
        assert!(!cached.is_stale(&config));
        assert!(!cached.is_expired(&config));
    }

    #[test]
    fn catalog_cache_new() {
        let cache = CatalogCache::new();
        assert!(cache.is_empty());
        assert_eq!(cache.len(), 0);
    }

    #[test]
    fn catalog_cache_with_config() {
        let config = CacheConfig::new(0.05, 60);
        let cache = CatalogCache::with_config(config);
        assert_eq!(cache.config().shift_threshold, 0.05);
        assert_eq!(cache.config().max_age_secs, 60);
    }

    #[test]
    fn catalog_cache_set_get() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();
        let catalog = make_test_catalog(5.0);

        cache.set(notebook_id, catalog.clone(), 100);

        let cached = cache.get(&notebook_id);
        assert!(cached.is_some());

        let cached = cached.unwrap();
        assert_eq!(cached.catalog.notebook_entropy, 5.0);
        assert_eq!(cached.cached_at_sequence, 100);
    }

    #[test]
    fn catalog_cache_get_missing() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();

        assert!(cache.get(&notebook_id).is_none());
    }

    #[test]
    fn catalog_cache_get_with_status() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();
        let catalog = make_test_catalog(5.0);

        cache.set(notebook_id, catalog, 100);

        let result = cache.get_with_status(&notebook_id);
        assert!(result.is_some());

        let (cached, status) = result.unwrap();
        assert_eq!(cached.cached_at_sequence, 100);
        assert_eq!(status, CacheStatus::Fresh);
    }

    #[test]
    fn catalog_cache_invalidate() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();
        let catalog = make_test_catalog(5.0);

        cache.set(notebook_id, catalog, 100);
        assert!(cache.get(&notebook_id).is_some());

        let removed = cache.invalidate(&notebook_id);
        assert!(removed);
        assert!(cache.get(&notebook_id).is_none());

        // Second invalidation returns false
        let removed_again = cache.invalidate(&notebook_id);
        assert!(!removed_again);
    }

    #[test]
    fn catalog_cache_invalidate_if_stale_below_threshold() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();
        let catalog = make_test_catalog(5.0);

        cache.set(notebook_id, catalog, 100);

        // Shift below threshold (0.1) - should NOT invalidate
        let invalidated = cache.invalidate_if_stale(&notebook_id, 0.05);
        assert!(!invalidated);
        assert!(cache.get(&notebook_id).is_some());
    }

    #[test]
    fn catalog_cache_invalidate_if_stale_above_threshold() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();
        let catalog = make_test_catalog(5.0);

        cache.set(notebook_id, catalog, 100);

        // Shift above threshold (0.1) - should invalidate
        let invalidated = cache.invalidate_if_stale(&notebook_id, 0.15);
        assert!(invalidated);
        assert!(cache.get(&notebook_id).is_none());
    }

    #[test]
    fn catalog_cache_invalidate_if_stale_at_threshold() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();
        let catalog = make_test_catalog(5.0);

        cache.set(notebook_id, catalog, 100);

        // Shift exactly at threshold - should NOT invalidate (> not >=)
        let invalidated = cache.invalidate_if_stale(&notebook_id, 0.1);
        assert!(!invalidated);
        assert!(cache.get(&notebook_id).is_some());
    }

    #[test]
    fn catalog_cache_is_fresh() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();
        let catalog = make_test_catalog(5.0);

        assert!(!cache.is_fresh(&notebook_id));

        cache.set(notebook_id, catalog, 100);
        assert!(cache.is_fresh(&notebook_id));
    }

    #[test]
    fn catalog_cache_needs_revalidation_missing() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();

        // Missing entry needs revalidation
        assert!(cache.needs_revalidation(&notebook_id));
    }

    #[test]
    fn catalog_cache_needs_revalidation_fresh() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();
        let catalog = make_test_catalog(5.0);

        cache.set(notebook_id, catalog, 100);

        // Fresh entry does NOT need revalidation
        assert!(!cache.needs_revalidation(&notebook_id));
    }

    #[test]
    fn catalog_cache_clear() {
        let cache = CatalogCache::new();

        // Add multiple entries
        for i in 0..5 {
            let notebook_id = NotebookId::new();
            let catalog = make_test_catalog(i as f64);
            cache.set(notebook_id, catalog, i);
        }

        assert_eq!(cache.len(), 5);

        cache.clear();

        assert!(cache.is_empty());
        assert_eq!(cache.len(), 0);
    }

    #[test]
    fn catalog_cache_len_is_empty() {
        let cache = CatalogCache::new();
        assert!(cache.is_empty());
        assert_eq!(cache.len(), 0);

        let notebook_id = NotebookId::new();
        let catalog = make_test_catalog(5.0);
        cache.set(notebook_id, catalog, 100);

        assert!(!cache.is_empty());
        assert_eq!(cache.len(), 1);
    }

    #[test]
    fn catalog_cache_stats_empty() {
        let cache = CatalogCache::new();
        let stats = cache.stats();

        assert_eq!(stats.total, 0);
        assert_eq!(stats.fresh, 0);
        assert_eq!(stats.stale, 0);
        assert_eq!(stats.expired, 0);
    }

    #[test]
    fn catalog_cache_stats_with_entries() {
        let cache = CatalogCache::new();

        // Add fresh entries
        for _ in 0..3 {
            let notebook_id = NotebookId::new();
            let catalog = make_test_catalog(5.0);
            cache.set(notebook_id, catalog, 100);
        }

        let stats = cache.stats();
        assert_eq!(stats.total, 3);
        assert_eq!(stats.fresh, 3);
        assert_eq!(stats.stale, 0);
        assert_eq!(stats.expired, 0);
    }

    #[test]
    fn catalog_cache_clone_shares_state() {
        let cache1 = CatalogCache::new();
        let cache2 = cache1.clone();

        let notebook_id = NotebookId::new();
        let catalog = make_test_catalog(5.0);

        // Set via cache1
        cache1.set(notebook_id, catalog, 100);

        // Should be visible via cache2
        assert!(cache2.get(&notebook_id).is_some());
        assert_eq!(cache1.len(), cache2.len());
    }

    #[test]
    fn catalog_cache_multiple_notebooks() {
        let cache = CatalogCache::new();

        let notebook1 = NotebookId::new();
        let notebook2 = NotebookId::new();
        let notebook3 = NotebookId::new();

        cache.set(notebook1, make_test_catalog(1.0), 1);
        cache.set(notebook2, make_test_catalog(2.0), 2);
        cache.set(notebook3, make_test_catalog(3.0), 3);

        assert_eq!(cache.len(), 3);

        let cached1 = cache.get(&notebook1).unwrap();
        assert_eq!(cached1.catalog.notebook_entropy, 1.0);

        let cached2 = cache.get(&notebook2).unwrap();
        assert_eq!(cached2.catalog.notebook_entropy, 2.0);

        let cached3 = cache.get(&notebook3).unwrap();
        assert_eq!(cached3.catalog.notebook_entropy, 3.0);
    }

    #[test]
    fn catalog_cache_update_existing() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();

        // Set initial
        cache.set(notebook_id, make_test_catalog(1.0), 1);

        // Update with new catalog
        cache.set(notebook_id, make_test_catalog(2.0), 2);

        // Should have updated value
        assert_eq!(cache.len(), 1);
        let cached = cache.get(&notebook_id).unwrap();
        assert_eq!(cached.catalog.notebook_entropy, 2.0);
        assert_eq!(cached.cached_at_sequence, 2);
    }

    #[test]
    fn cache_status_equality() {
        assert_eq!(CacheStatus::Fresh, CacheStatus::Fresh);
        assert_eq!(CacheStatus::Stale, CacheStatus::Stale);
        assert_eq!(CacheStatus::Expired, CacheStatus::Expired);
        assert_ne!(CacheStatus::Fresh, CacheStatus::Stale);
        assert_ne!(CacheStatus::Stale, CacheStatus::Expired);
    }

    #[test]
    fn cache_stats_default() {
        let stats = CacheStats::default();
        assert_eq!(stats.total, 0);
        assert_eq!(stats.fresh, 0);
        assert_eq!(stats.stale, 0);
        assert_eq!(stats.expired, 0);
    }

    #[test]
    fn catalog_cache_default() {
        let cache = CatalogCache::default();
        assert!(cache.is_empty());
        assert_eq!(cache.config().shift_threshold, DEFAULT_SHIFT_THRESHOLD);
    }

    #[test]
    fn evict_expired_no_expired() {
        let cache = CatalogCache::new();
        let notebook_id = NotebookId::new();

        cache.set(notebook_id, make_test_catalog(5.0), 100);

        // No expired entries
        let evicted = cache.evict_expired();
        assert_eq!(evicted, 0);
        assert_eq!(cache.len(), 1);
    }

    // Note: Testing time-based expiration would require mocking Instant
    // which is complex. The logic is tested via the status methods.
    // In production, consider using a mockable clock trait.
}
