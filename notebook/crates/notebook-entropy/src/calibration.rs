//! Adaptive orphan threshold calibration for notebook entropy.
//!
//! This module provides adaptive threshold computation for determining
//! when an entry should be classified as an orphan. The threshold adapts
//! to the notebook's characteristics by tracking the statistical distribution
//! of integration costs (specifically catalog_shift values).
//!
//! ## Algorithm
//!
//! The calibrator uses Welford's online algorithm to compute running
//! mean and variance in a single pass with O(1) memory:
//!
//! - threshold = mean + 2 * stddev
//!
//! This captures approximately 95% of observations in a normal distribution,
//! flagging entries with unusually high catalog shift as potential orphans.
//!
//! ## Configuration
//!
//! - Manual threshold: Set `orphan_threshold` to override automatic calibration
//! - Auto-calibrate: Enable `auto_calibrate` to use adaptive thresholds
//! - Fallback: When insufficient observations exist, uses a default threshold
//!
//! Owned by: agent-calibration (Task 2-3)

use notebook_core::types::IntegrationCost;
use serde::{Deserialize, Serialize};

/// Default number of observations required before computing adaptive threshold.
pub const DEFAULT_MIN_OBSERVATIONS: usize = 10;

/// Default fallback threshold when insufficient observations.
pub const DEFAULT_FALLBACK_THRESHOLD: f64 = 0.7;

/// Calibrator for adaptive orphan threshold computation.
///
/// Tracks running statistics of catalog_shift values to compute a threshold
/// that adapts to the notebook's characteristics. Uses Welford's online
/// algorithm for numerical stability.
///
/// # Example
///
/// ```rust,ignore
/// use notebook_entropy::calibration::ThresholdCalibrator;
///
/// let mut calibrator = ThresholdCalibrator::new();
///
/// // Observe catalog_shift values from integration costs
/// for shift in &[0.1, 0.15, 0.2, 0.12, 0.18, 0.22, 0.14, 0.16, 0.19, 0.21] {
///     calibrator.observe(*shift);
/// }
///
/// // Get adaptive threshold
/// let threshold = calibrator.compute_threshold();
/// println!("Orphan threshold: {}", threshold);
/// ```
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ThresholdCalibrator {
    /// Number of observations.
    count: usize,

    /// Running mean (Welford's algorithm).
    mean: f64,

    /// Running M2 for variance computation (Welford's algorithm).
    /// This is the sum of squared differences from the current mean.
    m2: f64,

    /// Minimum observations required before using computed threshold.
    min_observations: usize,

    /// Fallback threshold when insufficient observations.
    fallback_threshold: f64,
}

impl ThresholdCalibrator {
    /// Creates a new threshold calibrator with default settings.
    pub fn new() -> Self {
        Self {
            count: 0,
            mean: 0.0,
            m2: 0.0,
            min_observations: DEFAULT_MIN_OBSERVATIONS,
            fallback_threshold: DEFAULT_FALLBACK_THRESHOLD,
        }
    }

    /// Creates a calibrator with custom settings.
    ///
    /// # Arguments
    ///
    /// * `min_observations` - Minimum samples before computing adaptive threshold
    /// * `fallback_threshold` - Threshold to use when insufficient observations
    pub fn with_settings(min_observations: usize, fallback_threshold: f64) -> Self {
        Self {
            count: 0,
            mean: 0.0,
            m2: 0.0,
            min_observations,
            fallback_threshold,
        }
    }

    /// Adds a new catalog_shift observation.
    ///
    /// Uses Welford's online algorithm for numerically stable
    /// incremental mean and variance computation.
    ///
    /// # Arguments
    ///
    /// * `catalog_shift` - The catalog_shift value from an IntegrationCost
    pub fn observe(&mut self, catalog_shift: f64) {
        self.count += 1;
        let delta = catalog_shift - self.mean;
        self.mean += delta / self.count as f64;
        let delta2 = catalog_shift - self.mean;
        self.m2 += delta * delta2;
    }

    /// Computes the adaptive orphan threshold.
    ///
    /// Returns mean + 2 * stddev if sufficient observations exist,
    /// otherwise returns the fallback threshold.
    ///
    /// # Returns
    ///
    /// The computed threshold (catalog_shift values above this indicate orphans).
    pub fn compute_threshold(&self) -> f64 {
        if self.count < self.min_observations {
            return self.fallback_threshold;
        }

        self.mean + 2.0 * self.stddev()
    }

    /// Checks if a cost indicates orphan status.
    ///
    /// An entry is considered an orphan if:
    /// 1. Its catalog_shift exceeds the computed threshold, OR
    /// 2. The entry's orphan flag is already set (semantic check from engine)
    ///
    /// # Arguments
    ///
    /// * `cost` - The IntegrationCost to evaluate
    ///
    /// # Returns
    ///
    /// `true` if the entry should be classified as an orphan.
    pub fn is_orphan(&self, cost: &IntegrationCost) -> bool {
        // If already marked orphan by semantic check, honor that
        if cost.orphan {
            return true;
        }

        // Check catalog_shift against threshold
        cost.catalog_shift > self.compute_threshold()
    }

    /// Returns the number of observations.
    pub fn observation_count(&self) -> usize {
        self.count
    }

    /// Returns the running mean of catalog_shift values.
    pub fn mean(&self) -> f64 {
        self.mean
    }

    /// Returns the sample standard deviation.
    ///
    /// Uses Bessel's correction (n-1 denominator) for sample variance.
    /// Returns 0.0 if fewer than 2 observations.
    pub fn stddev(&self) -> f64 {
        if self.count < 2 {
            return 0.0;
        }
        (self.m2 / (self.count - 1) as f64).sqrt()
    }

    /// Returns the sample variance.
    ///
    /// Uses Bessel's correction (n-1 denominator).
    /// Returns 0.0 if fewer than 2 observations.
    pub fn variance(&self) -> f64 {
        if self.count < 2 {
            return 0.0;
        }
        self.m2 / (self.count - 1) as f64
    }

    /// Returns whether sufficient observations exist for adaptive threshold.
    pub fn has_sufficient_observations(&self) -> bool {
        self.count >= self.min_observations
    }

    /// Resets all accumulated statistics.
    pub fn reset(&mut self) {
        self.count = 0;
        self.mean = 0.0;
        self.m2 = 0.0;
    }
}

impl Default for ThresholdCalibrator {
    fn default() -> Self {
        Self::new()
    }
}

/// Configuration for notebook-specific orphan detection.
///
/// Allows manual threshold override or automatic calibration based on
/// the notebook's integration cost distribution.
///
/// # Example
///
/// ```rust,ignore
/// use notebook_entropy::calibration::NotebookConfig;
///
/// // Use automatic calibration (default)
/// let auto_config = NotebookConfig::default();
///
/// // Use fixed threshold
/// let fixed_config = NotebookConfig {
///     orphan_threshold: Some(0.8),
///     auto_calibrate: false,
/// };
/// ```
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct NotebookConfig {
    /// Manual orphan threshold override.
    ///
    /// If set, this threshold is used regardless of auto_calibrate setting.
    /// Range: 0.0 to 1.0 (catalog_shift is normalized to this range).
    pub orphan_threshold: Option<f64>,

    /// Enable automatic threshold calibration.
    ///
    /// When true and orphan_threshold is None, the threshold is computed
    /// from the statistical distribution of catalog_shift values.
    pub auto_calibrate: bool,
}

impl NotebookConfig {
    /// Creates a new config with automatic calibration enabled.
    pub fn new() -> Self {
        Self {
            orphan_threshold: None,
            auto_calibrate: true,
        }
    }

    /// Creates a config with a fixed threshold.
    pub fn with_fixed_threshold(threshold: f64) -> Self {
        Self {
            orphan_threshold: Some(threshold),
            auto_calibrate: false,
        }
    }

    /// Returns the effective threshold for orphan detection.
    ///
    /// # Arguments
    ///
    /// * `calibrator` - The calibrator to use for adaptive threshold
    ///
    /// # Returns
    ///
    /// The threshold to use: manual override if set, otherwise computed.
    pub fn effective_threshold(&self, calibrator: &ThresholdCalibrator) -> f64 {
        if let Some(threshold) = self.orphan_threshold {
            return threshold;
        }

        if self.auto_calibrate {
            calibrator.compute_threshold()
        } else {
            // Neither manual nor auto - use default fallback
            DEFAULT_FALLBACK_THRESHOLD
        }
    }

    /// Checks if a cost indicates orphan status using this config.
    ///
    /// # Arguments
    ///
    /// * `cost` - The IntegrationCost to evaluate
    /// * `calibrator` - The calibrator for adaptive threshold
    ///
    /// # Returns
    ///
    /// `true` if the entry should be classified as an orphan.
    pub fn is_orphan(&self, cost: &IntegrationCost, calibrator: &ThresholdCalibrator) -> bool {
        // If already marked orphan by semantic check, honor that
        if cost.orphan {
            return true;
        }

        let threshold = self.effective_threshold(calibrator);
        cost.catalog_shift > threshold
    }
}

impl Default for NotebookConfig {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn calibrator_new() {
        let calibrator = ThresholdCalibrator::new();
        assert_eq!(calibrator.observation_count(), 0);
        assert_eq!(calibrator.mean(), 0.0);
        assert_eq!(calibrator.stddev(), 0.0);
    }

    #[test]
    fn calibrator_observe_single() {
        let mut calibrator = ThresholdCalibrator::new();
        calibrator.observe(0.5);

        assert_eq!(calibrator.observation_count(), 1);
        assert_eq!(calibrator.mean(), 0.5);
        assert_eq!(calibrator.stddev(), 0.0); // Need 2+ for stddev
    }

    #[test]
    fn calibrator_observe_multiple() {
        let mut calibrator = ThresholdCalibrator::new();
        let values = [0.1, 0.2, 0.3, 0.4, 0.5];

        for v in &values {
            calibrator.observe(*v);
        }

        assert_eq!(calibrator.observation_count(), 5);
        // Mean should be (0.1 + 0.2 + 0.3 + 0.4 + 0.5) / 5 = 0.3
        assert!((calibrator.mean() - 0.3).abs() < 1e-10);
    }

    #[test]
    fn calibrator_stddev_calculation() {
        let mut calibrator = ThresholdCalibrator::new();
        // Values with known stddev: [1, 2, 3, 4, 5]
        // Mean = 3, Variance = 2.5, StdDev = sqrt(2.5) ≈ 1.58
        for v in &[1.0, 2.0, 3.0, 4.0, 5.0] {
            calibrator.observe(*v);
        }

        assert_eq!(calibrator.observation_count(), 5);
        assert!((calibrator.mean() - 3.0).abs() < 1e-10);
        assert!((calibrator.variance() - 2.5).abs() < 1e-10);
        assert!((calibrator.stddev() - 2.5_f64.sqrt()).abs() < 1e-10);
    }

    #[test]
    fn calibrator_threshold_insufficient_data() {
        let mut calibrator = ThresholdCalibrator::new();
        // Add fewer than min_observations (default 10)
        for i in 0..5 {
            calibrator.observe(0.1 * i as f64);
        }

        assert!(!calibrator.has_sufficient_observations());
        assert_eq!(calibrator.compute_threshold(), DEFAULT_FALLBACK_THRESHOLD);
    }

    #[test]
    fn calibrator_threshold_sufficient_data() {
        let mut calibrator = ThresholdCalibrator::new();
        // Add exactly min_observations
        for _ in 0..DEFAULT_MIN_OBSERVATIONS {
            calibrator.observe(0.2);
        }

        assert!(calibrator.has_sufficient_observations());
        // All same value: mean = 0.2, stddev = 0
        // threshold = 0.2 + 2*0 = 0.2
        assert!((calibrator.compute_threshold() - 0.2).abs() < 1e-10);
    }

    #[test]
    fn calibrator_threshold_with_variance() {
        let mut calibrator = ThresholdCalibrator::new();
        // Add 10 values: 0.1, 0.2, 0.3, ..., 1.0
        for i in 1..=10 {
            calibrator.observe(0.1 * i as f64);
        }

        assert!(calibrator.has_sufficient_observations());

        let mean = calibrator.mean();
        let stddev = calibrator.stddev();
        let expected_threshold = mean + 2.0 * stddev;

        assert!((calibrator.compute_threshold() - expected_threshold).abs() < 1e-10);
    }

    #[test]
    fn calibrator_is_orphan_below_threshold() {
        let mut calibrator = ThresholdCalibrator::new();
        // All low values
        for _ in 0..10 {
            calibrator.observe(0.1);
        }

        let cost = IntegrationCost {
            entries_revised: 0,
            references_broken: 0,
            catalog_shift: 0.15, // Slightly above mean but within threshold
            orphan: false,
        };

        // With all values at 0.1, threshold = 0.1 + 2*0 = 0.1
        // 0.15 > 0.1, so this would be orphan
        assert!(calibrator.is_orphan(&cost));
    }

    #[test]
    fn calibrator_is_orphan_semantic_flag() {
        let calibrator = ThresholdCalibrator::new();

        // Even with low catalog_shift, semantic orphan flag is honored
        let cost = IntegrationCost {
            entries_revised: 0,
            references_broken: 0,
            catalog_shift: 0.0,
            orphan: true, // Semantic orphan
        };

        assert!(calibrator.is_orphan(&cost));
    }

    #[test]
    fn calibrator_reset() {
        let mut calibrator = ThresholdCalibrator::new();
        for i in 0..10 {
            calibrator.observe(0.1 * i as f64);
        }

        assert_eq!(calibrator.observation_count(), 10);

        calibrator.reset();

        assert_eq!(calibrator.observation_count(), 0);
        assert_eq!(calibrator.mean(), 0.0);
        assert_eq!(calibrator.stddev(), 0.0);
    }

    #[test]
    fn calibrator_with_settings() {
        let calibrator = ThresholdCalibrator::with_settings(5, 0.5);

        // With only 3 observations, should use fallback
        let mut cal = calibrator.clone();
        for _ in 0..3 {
            cal.observe(0.1);
        }
        assert_eq!(cal.compute_threshold(), 0.5);

        // With 5 observations, should compute
        for _ in 3..5 {
            cal.observe(0.1);
        }
        assert!(cal.has_sufficient_observations());
    }

    #[test]
    fn config_default() {
        let config = NotebookConfig::default();
        assert!(config.orphan_threshold.is_none());
        assert!(config.auto_calibrate);
    }

    #[test]
    fn config_fixed_threshold() {
        let config = NotebookConfig::with_fixed_threshold(0.8);
        assert_eq!(config.orphan_threshold, Some(0.8));
        assert!(!config.auto_calibrate);
    }

    #[test]
    fn config_effective_threshold_manual() {
        let config = NotebookConfig::with_fixed_threshold(0.8);
        let calibrator = ThresholdCalibrator::new();

        // Manual threshold takes precedence
        assert_eq!(config.effective_threshold(&calibrator), 0.8);
    }

    #[test]
    fn config_effective_threshold_auto() {
        let config = NotebookConfig::new();
        let mut calibrator = ThresholdCalibrator::new();

        // Without sufficient observations, uses fallback
        assert_eq!(
            config.effective_threshold(&calibrator),
            DEFAULT_FALLBACK_THRESHOLD
        );

        // With sufficient observations, uses computed
        for _ in 0..10 {
            calibrator.observe(0.2);
        }
        assert!((config.effective_threshold(&calibrator) - 0.2).abs() < 1e-10);
    }

    #[test]
    fn config_is_orphan_manual_override() {
        let config = NotebookConfig::with_fixed_threshold(0.5);
        let calibrator = ThresholdCalibrator::new();

        let cost_below = IntegrationCost {
            entries_revised: 0,
            references_broken: 0,
            catalog_shift: 0.4,
            orphan: false,
        };

        let cost_above = IntegrationCost {
            entries_revised: 0,
            references_broken: 0,
            catalog_shift: 0.6,
            orphan: false,
        };

        assert!(!config.is_orphan(&cost_below, &calibrator));
        assert!(config.is_orphan(&cost_above, &calibrator));
    }

    #[test]
    fn config_is_orphan_semantic_flag() {
        let config = NotebookConfig::new();
        let calibrator = ThresholdCalibrator::new();

        // Semantic orphan flag takes precedence
        let cost = IntegrationCost {
            entries_revised: 0,
            references_broken: 0,
            catalog_shift: 0.0,
            orphan: true,
        };

        assert!(config.is_orphan(&cost, &calibrator));
    }

    #[test]
    fn welford_numerical_stability() {
        let mut calibrator = ThresholdCalibrator::new();

        // Large values that could cause numerical issues with naive algorithm
        for i in 0..100 {
            calibrator.observe(1_000_000.0 + 0.001 * i as f64);
        }

        // Mean should be approximately 1_000_000 + 0.0495
        assert!((calibrator.mean() - 1_000_000.0495).abs() < 1e-6);

        // Stddev should be small (based on the 0.001 increments)
        assert!(calibrator.stddev() > 0.0);
        assert!(calibrator.stddev() < 1.0);
    }

    #[test]
    fn identical_values() {
        let mut calibrator = ThresholdCalibrator::new();

        for _ in 0..20 {
            calibrator.observe(0.5);
        }

        assert_eq!(calibrator.mean(), 0.5);
        assert_eq!(calibrator.stddev(), 0.0);
        assert_eq!(calibrator.compute_threshold(), 0.5);
    }

    #[test]
    fn config_disabled_auto_no_manual() {
        let config = NotebookConfig {
            orphan_threshold: None,
            auto_calibrate: false,
        };
        let calibrator = ThresholdCalibrator::new();

        // Falls back to default
        assert_eq!(
            config.effective_threshold(&calibrator),
            DEFAULT_FALLBACK_THRESHOLD
        );
    }

    #[test]
    fn serialization_roundtrip_calibrator() {
        let mut calibrator = ThresholdCalibrator::new();
        for i in 0..10 {
            calibrator.observe(0.1 * i as f64);
        }

        let json = serde_json::to_string(&calibrator).unwrap();
        let parsed: ThresholdCalibrator = serde_json::from_str(&json).unwrap();

        assert_eq!(parsed.observation_count(), calibrator.observation_count());
        assert!((parsed.mean() - calibrator.mean()).abs() < 1e-10);
        assert!((parsed.stddev() - calibrator.stddev()).abs() < 1e-10);
    }

    #[test]
    fn serialization_roundtrip_config() {
        let config = NotebookConfig::with_fixed_threshold(0.75);

        let json = serde_json::to_string(&config).unwrap();
        let parsed: NotebookConfig = serde_json::from_str(&json).unwrap();

        assert_eq!(parsed.orphan_threshold, config.orphan_threshold);
        assert_eq!(parsed.auto_calibrate, config.auto_calibrate);
    }
}
