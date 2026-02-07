//! Retroactive cost propagation for the Knowledge Exchange Platform.
//!
//! When a WRITE causes existing entries to shift clusters, those entries'
//! cumulative_cost metadata should update. This module provides the
//! background job infrastructure for asynchronous cost propagation.
//!
//! ## Architecture
//!
//! - `PropagationJob`: A unit of work representing cost updates to affected entries
//! - `PropagationQueue`: In-memory FIFO queue for pending jobs
//! - `PropagationWorker`: Background task that processes the queue asynchronously
//!
//! ## Idempotency
//!
//! Jobs are designed to be idempotent - safe to replay without double-counting.
//! Each job has a unique ID, and completed job IDs are tracked to prevent
//! duplicate processing.
//!
//! ## Example
//!
//! ```rust,ignore
//! use notebook_entropy::propagation::{PropagationJob, PropagationQueue, PropagationWorker};
//! use notebook_core::types::{NotebookId, EntryId};
//!
//! // Create queue and enqueue a job
//! let queue = PropagationQueue::new();
//! let job = PropagationJob::new(
//!     notebook_id,
//!     vec![entry1, entry2],
//!     0.5,
//! );
//! queue.enqueue(job);
//!
//! // Start background worker
//! let worker = PropagationWorker::new(queue.clone());
//! worker.start();
//! ```
//!
//! Owned by: agent-propagation (Task 2-4)

use notebook_core::types::{EntryId, NotebookId};
use std::collections::{HashSet, VecDeque};
use std::sync::{Arc, Mutex};
use std::time::Duration;
use tokio::sync::watch;
use tracing::{debug, info, warn};
use uuid::Uuid;

/// Error types for propagation operations.
#[derive(Debug, Clone, thiserror::Error)]
pub enum PropagationError {
    /// Queue lock was poisoned.
    #[error("queue lock poisoned")]
    LockPoisoned,

    /// Worker is already running.
    #[error("worker already running")]
    AlreadyRunning,

    /// Worker is not running.
    #[error("worker not running")]
    NotRunning,

    /// Cost update failed.
    #[error("cost update failed: {0}")]
    UpdateFailed(String),
}

/// A job representing cost updates to be propagated to affected entries.
///
/// Each job tracks:
/// - Which notebook the entries belong to
/// - Which entries need their cumulative_cost updated
/// - The cost delta to add
///
/// Jobs are idempotent - each has a unique ID to prevent double processing.
#[derive(Debug, Clone, PartialEq)]
pub struct PropagationJob {
    /// Unique identifier for this job (for idempotency).
    pub job_id: Uuid,

    /// The notebook containing the affected entries.
    pub notebook_id: NotebookId,

    /// Entry IDs that need cumulative_cost updates.
    pub affected_entry_ids: Vec<EntryId>,

    /// The cost delta to add to each affected entry's cumulative_cost.
    pub cost_delta: f64,
}

impl PropagationJob {
    /// Creates a new propagation job.
    ///
    /// # Arguments
    ///
    /// * `notebook_id` - The notebook containing affected entries
    /// * `affected_entry_ids` - Entries whose cumulative_cost should be updated
    /// * `cost_delta` - The amount to add to each entry's cumulative_cost
    pub fn new(notebook_id: NotebookId, affected_entry_ids: Vec<EntryId>, cost_delta: f64) -> Self {
        Self {
            job_id: Uuid::new_v4(),
            notebook_id,
            affected_entry_ids,
            cost_delta,
        }
    }

    /// Creates a job with a specific job_id (for testing or replay).
    pub fn with_id(
        job_id: Uuid,
        notebook_id: NotebookId,
        affected_entry_ids: Vec<EntryId>,
        cost_delta: f64,
    ) -> Self {
        Self {
            job_id,
            notebook_id,
            affected_entry_ids,
            cost_delta,
        }
    }

    /// Returns the number of entries affected by this job.
    pub fn affected_count(&self) -> usize {
        self.affected_entry_ids.len()
    }

    /// Returns true if this job has no affected entries.
    pub fn is_empty(&self) -> bool {
        self.affected_entry_ids.is_empty()
    }
}

/// Thread-safe queue for propagation jobs.
///
/// Uses a VecDeque internally with Mutex protection for concurrent access.
/// Jobs are processed in FIFO order.
#[derive(Debug, Clone)]
pub struct PropagationQueue {
    inner: Arc<Mutex<VecDeque<PropagationJob>>>,
}

impl PropagationQueue {
    /// Creates a new empty propagation queue.
    pub fn new() -> Self {
        Self {
            inner: Arc::new(Mutex::new(VecDeque::new())),
        }
    }

    /// Enqueues a job for processing.
    ///
    /// Jobs with empty affected_entry_ids are silently dropped.
    pub fn enqueue(&self, job: PropagationJob) {
        if job.is_empty() {
            debug!("Dropping empty propagation job {}", job.job_id);
            return;
        }

        match self.inner.lock() {
            Ok(mut queue) => {
                debug!(
                    "Enqueuing propagation job {} with {} entries",
                    job.job_id,
                    job.affected_count()
                );
                queue.push_back(job);
            }
            Err(e) => {
                warn!("Failed to enqueue job: lock poisoned: {}", e);
            }
        }
    }

    /// Dequeues and returns the next job, if any.
    pub fn process_next(&self) -> Option<PropagationJob> {
        match self.inner.lock() {
            Ok(mut queue) => queue.pop_front(),
            Err(e) => {
                warn!("Failed to process job: lock poisoned: {}", e);
                None
            }
        }
    }

    /// Returns the number of pending jobs.
    pub fn len(&self) -> usize {
        match self.inner.lock() {
            Ok(queue) => queue.len(),
            Err(_) => 0,
        }
    }

    /// Returns true if there are no pending jobs.
    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }

    /// Clears all pending jobs from the queue.
    pub fn clear(&self) {
        if let Ok(mut queue) = self.inner.lock() {
            queue.clear();
        }
    }
}

impl Default for PropagationQueue {
    fn default() -> Self {
        Self::new()
    }
}

/// Trait for updating cumulative costs on entries.
///
/// Implementations handle the actual storage updates. This abstraction
/// allows the worker to be tested without a real database.
pub trait CostUpdater: Send + Sync {
    /// Updates the cumulative_cost for the given entries.
    ///
    /// # Arguments
    ///
    /// * `notebook_id` - The notebook containing the entries
    /// * `entry_ids` - The entries to update
    /// * `cost_delta` - The amount to add to each entry's cumulative_cost
    ///
    /// # Returns
    ///
    /// The number of entries successfully updated.
    fn update_cumulative_cost(
        &self,
        notebook_id: NotebookId,
        entry_ids: &[EntryId],
        cost_delta: f64,
    ) -> Result<usize, PropagationError>;
}

/// A no-op cost updater for testing.
#[derive(Debug, Default)]
pub struct NoOpCostUpdater;

impl CostUpdater for NoOpCostUpdater {
    fn update_cumulative_cost(
        &self,
        _notebook_id: NotebookId,
        entry_ids: &[EntryId],
        _cost_delta: f64,
    ) -> Result<usize, PropagationError> {
        Ok(entry_ids.len())
    }
}

/// Statistics about worker processing.
#[derive(Debug, Clone, Default)]
pub struct WorkerStats {
    /// Total jobs processed.
    pub jobs_processed: u64,
    /// Total entries updated.
    pub entries_updated: u64,
    /// Jobs skipped due to idempotency.
    pub jobs_skipped: u64,
    /// Jobs that failed.
    pub jobs_failed: u64,
}

/// Background worker that processes the propagation queue.
///
/// The worker polls the queue at a configurable interval and processes
/// jobs asynchronously. It tracks completed job IDs to ensure idempotency.
pub struct PropagationWorker<U: CostUpdater> {
    /// The queue to process jobs from.
    queue: PropagationQueue,

    /// The cost updater implementation.
    updater: Arc<U>,

    /// Set of completed job IDs for idempotency.
    completed_jobs: Arc<Mutex<HashSet<Uuid>>>,

    /// Processing statistics.
    stats: Arc<Mutex<WorkerStats>>,

    /// Poll interval for checking the queue.
    poll_interval: Duration,

    /// Shutdown signal sender.
    shutdown_tx: Option<watch::Sender<bool>>,

    /// Shutdown signal receiver for spawned tasks.
    shutdown_rx: watch::Receiver<bool>,
}

impl<U: CostUpdater + 'static> PropagationWorker<U> {
    /// Creates a new worker with the given queue and cost updater.
    pub fn new(queue: PropagationQueue, updater: U) -> Self {
        let (shutdown_tx, shutdown_rx) = watch::channel(false);
        Self {
            queue,
            updater: Arc::new(updater),
            completed_jobs: Arc::new(Mutex::new(HashSet::new())),
            stats: Arc::new(Mutex::new(WorkerStats::default())),
            poll_interval: Duration::from_millis(100),
            shutdown_tx: Some(shutdown_tx),
            shutdown_rx,
        }
    }

    /// Sets the poll interval for queue checking.
    pub fn with_poll_interval(mut self, interval: Duration) -> Self {
        self.poll_interval = interval;
        self
    }

    /// Returns the current worker statistics.
    pub fn stats(&self) -> WorkerStats {
        self.stats.lock().map(|s| s.clone()).unwrap_or_default()
    }

    /// Returns the current queue depth.
    pub fn queue_depth(&self) -> usize {
        self.queue.len()
    }

    /// Starts the background worker.
    ///
    /// Spawns a tokio task that polls the queue and processes jobs.
    /// Returns a handle that can be used to monitor the worker.
    pub fn start(&self) -> tokio::task::JoinHandle<()> {
        let queue = self.queue.clone();
        let updater = self.updater.clone();
        let completed_jobs = self.completed_jobs.clone();
        let stats = self.stats.clone();
        let poll_interval = self.poll_interval;
        let mut shutdown_rx = self.shutdown_rx.clone();

        tokio::spawn(async move {
            let mut interval = tokio::time::interval(poll_interval);

            loop {
                tokio::select! {
                    _ = interval.tick() => {
                        // Process all available jobs
                        while let Some(job) = queue.process_next() {
                            let job_id = job.job_id;
                            let start = std::time::Instant::now();

                            // Idempotency check
                            let is_completed = completed_jobs
                                .lock()
                                .map(|set| set.contains(&job_id))
                                .unwrap_or(false);

                            if is_completed {
                                debug!("Skipping already-completed job {}", job_id);
                                if let Ok(mut s) = stats.lock() {
                                    s.jobs_skipped += 1;
                                }
                                continue;
                            }

                            // Process the job
                            match updater.update_cumulative_cost(
                                job.notebook_id,
                                &job.affected_entry_ids,
                                job.cost_delta,
                            ) {
                                Ok(count) => {
                                    let elapsed = start.elapsed();
                                    info!(
                                        "Processed propagation job {} in {:?}: {} entries updated",
                                        job_id, elapsed, count
                                    );

                                    // Mark as completed and update stats
                                    if let Ok(mut set) = completed_jobs.lock() {
                                        set.insert(job_id);
                                    }
                                    if let Ok(mut s) = stats.lock() {
                                        s.jobs_processed += 1;
                                        s.entries_updated += count as u64;
                                    }
                                }
                                Err(e) => {
                                    warn!("Failed to process job {}: {}", job_id, e);
                                    if let Ok(mut s) = stats.lock() {
                                        s.jobs_failed += 1;
                                    }
                                }
                            }
                        }

                        // Log queue depth periodically
                        let depth = queue.len();
                        if depth > 0 {
                            debug!("Propagation queue depth: {}", depth);
                        }
                    }
                    _ = shutdown_rx.changed() => {
                        if *shutdown_rx.borrow() {
                            info!("Propagation worker shutting down");
                            break;
                        }
                    }
                }
            }
        })
    }

    /// Signals the worker to shut down.
    pub fn shutdown(&mut self) {
        if let Some(tx) = self.shutdown_tx.take() {
            let _ = tx.send(true);
        }
    }
}

#[cfg(test)]
impl<U: CostUpdater + 'static> PropagationWorker<U> {
    /// Checks if a job has already been processed (test-only helper).
    fn is_completed(&self, job_id: &Uuid) -> bool {
        self.completed_jobs
            .lock()
            .map(|set| set.contains(job_id))
            .unwrap_or(false)
    }

    /// Marks a job as completed (test-only helper).
    fn mark_completed(&self, job_id: Uuid) {
        if let Ok(mut set) = self.completed_jobs.lock() {
            set.insert(job_id);
        }
    }

    /// Processes a single job synchronously (test-only helper).
    fn process_job(&self, job: PropagationJob) {
        let job_id = job.job_id;
        let start = std::time::Instant::now();

        // Idempotency check
        if self.is_completed(&job_id) {
            debug!("Skipping already-completed job {}", job_id);
            if let Ok(mut stats) = self.stats.lock() {
                stats.jobs_skipped += 1;
            }
            return;
        }

        // Process the job
        match self.updater.update_cumulative_cost(
            job.notebook_id,
            &job.affected_entry_ids,
            job.cost_delta,
        ) {
            Ok(count) => {
                let elapsed = start.elapsed();
                info!(
                    "Processed propagation job {} in {:?}: {} entries updated",
                    job_id, elapsed, count
                );

                self.mark_completed(job_id);
                if let Ok(mut stats) = self.stats.lock() {
                    stats.jobs_processed += 1;
                    stats.entries_updated += count as u64;
                }
            }
            Err(e) => {
                warn!("Failed to process job {}: {}", job_id, e);
                if let Ok(mut stats) = self.stats.lock() {
                    stats.jobs_failed += 1;
                }
            }
        }
    }
}

/// Creates a propagation job from integration cost results.
///
/// This is a convenience function to create a job after computing
/// integration cost for a new entry.
///
/// # Arguments
///
/// * `notebook_id` - The notebook the entry was added to
/// * `affected_entry_ids` - Entries that changed clusters due to the new entry
/// * `integration_cost` - The computed integration cost
///
/// # Returns
///
/// A PropagationJob if there are affected entries, or None if no propagation needed.
pub fn create_propagation_job(
    notebook_id: NotebookId,
    affected_entry_ids: Vec<EntryId>,
    entries_revised: u32,
    references_broken: u32,
    catalog_shift: f64,
) -> Option<PropagationJob> {
    if affected_entry_ids.is_empty() {
        return None;
    }

    // Compute cost delta from integration cost components
    // Each affected entry accumulates a portion of the disruption cost
    let cost_delta =
        (entries_revised as f64 * 0.5) + (references_broken as f64 * 0.3) + (catalog_shift * 0.2);

    Some(PropagationJob::new(
        notebook_id,
        affected_entry_ids,
        cost_delta,
    ))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn make_entry_id() -> EntryId {
        EntryId::new()
    }

    fn make_notebook_id() -> NotebookId {
        NotebookId::new()
    }

    #[test]
    fn propagation_job_new() {
        let notebook_id = make_notebook_id();
        let entry1 = make_entry_id();
        let entry2 = make_entry_id();

        let job = PropagationJob::new(notebook_id, vec![entry1, entry2], 0.5);

        assert_eq!(job.notebook_id, notebook_id);
        assert_eq!(job.affected_entry_ids.len(), 2);
        assert_eq!(job.cost_delta, 0.5);
        assert_eq!(job.affected_count(), 2);
        assert!(!job.is_empty());
    }

    #[test]
    fn propagation_job_empty() {
        let notebook_id = make_notebook_id();
        let job = PropagationJob::new(notebook_id, vec![], 0.5);

        assert!(job.is_empty());
        assert_eq!(job.affected_count(), 0);
    }

    #[test]
    fn propagation_job_with_id() {
        let job_id = Uuid::new_v4();
        let notebook_id = make_notebook_id();

        let job = PropagationJob::with_id(job_id, notebook_id, vec![make_entry_id()], 1.0);

        assert_eq!(job.job_id, job_id);
    }

    #[test]
    fn propagation_queue_new() {
        let queue = PropagationQueue::new();
        assert!(queue.is_empty());
        assert_eq!(queue.len(), 0);
    }

    #[test]
    fn propagation_queue_enqueue_dequeue() {
        let queue = PropagationQueue::new();
        let notebook_id = make_notebook_id();

        let job1 = PropagationJob::new(notebook_id, vec![make_entry_id()], 0.5);
        let job2 = PropagationJob::new(notebook_id, vec![make_entry_id()], 1.0);

        queue.enqueue(job1.clone());
        queue.enqueue(job2.clone());

        assert_eq!(queue.len(), 2);

        let dequeued1 = queue.process_next().unwrap();
        assert_eq!(dequeued1.job_id, job1.job_id);

        let dequeued2 = queue.process_next().unwrap();
        assert_eq!(dequeued2.job_id, job2.job_id);

        assert!(queue.is_empty());
        assert!(queue.process_next().is_none());
    }

    #[test]
    fn propagation_queue_drops_empty_jobs() {
        let queue = PropagationQueue::new();
        let notebook_id = make_notebook_id();

        let empty_job = PropagationJob::new(notebook_id, vec![], 0.5);
        queue.enqueue(empty_job);

        assert!(queue.is_empty());
    }

    #[test]
    fn propagation_queue_clear() {
        let queue = PropagationQueue::new();
        let notebook_id = make_notebook_id();

        queue.enqueue(PropagationJob::new(notebook_id, vec![make_entry_id()], 0.5));
        queue.enqueue(PropagationJob::new(notebook_id, vec![make_entry_id()], 1.0));

        assert_eq!(queue.len(), 2);

        queue.clear();
        assert!(queue.is_empty());
    }

    #[test]
    fn propagation_queue_clone_shares_state() {
        let queue1 = PropagationQueue::new();
        let queue2 = queue1.clone();
        let notebook_id = make_notebook_id();

        queue1.enqueue(PropagationJob::new(notebook_id, vec![make_entry_id()], 0.5));

        assert_eq!(queue2.len(), 1);

        let _ = queue2.process_next();
        assert!(queue1.is_empty());
    }

    #[test]
    fn no_op_cost_updater() {
        let updater = NoOpCostUpdater;
        let notebook_id = make_notebook_id();
        let entries = vec![make_entry_id(), make_entry_id()];

        let result = updater.update_cumulative_cost(notebook_id, &entries, 0.5);
        assert!(result.is_ok());
        assert_eq!(result.unwrap(), 2);
    }

    #[test]
    fn worker_idempotency() {
        let queue = PropagationQueue::new();
        let worker = PropagationWorker::new(queue.clone(), NoOpCostUpdater);
        let notebook_id = make_notebook_id();

        // Create job with known ID
        let job_id = Uuid::new_v4();
        let job = PropagationJob::with_id(job_id, notebook_id, vec![make_entry_id()], 0.5);

        // Process job once
        worker.process_job(job.clone());
        assert!(worker.is_completed(&job_id));

        // Process same job again - should be skipped
        worker.process_job(job);

        let stats = worker.stats();
        assert_eq!(stats.jobs_processed, 1);
        assert_eq!(stats.jobs_skipped, 1);
    }

    #[test]
    fn worker_stats() {
        let queue = PropagationQueue::new();
        let worker = PropagationWorker::new(queue.clone(), NoOpCostUpdater);
        let notebook_id = make_notebook_id();

        let job = PropagationJob::new(notebook_id, vec![make_entry_id(), make_entry_id()], 0.5);
        worker.process_job(job);

        let stats = worker.stats();
        assert_eq!(stats.jobs_processed, 1);
        assert_eq!(stats.entries_updated, 2);
        assert_eq!(stats.jobs_failed, 0);
    }

    #[test]
    fn create_propagation_job_none_for_empty() {
        let notebook_id = make_notebook_id();
        let job = create_propagation_job(notebook_id, vec![], 5, 2, 0.3);
        assert!(job.is_none());
    }

    #[test]
    fn create_propagation_job_computes_delta() {
        let notebook_id = make_notebook_id();
        let job = create_propagation_job(
            notebook_id,
            vec![make_entry_id()],
            10,  // entries_revised
            4,   // references_broken
            0.5, // catalog_shift
        )
        .unwrap();

        // cost_delta = (10 * 0.5) + (4 * 0.3) + (0.5 * 0.2) = 5.0 + 1.2 + 0.1 = 6.3
        assert!((job.cost_delta - 6.3).abs() < 0.001);
    }

    #[test]
    fn worker_poll_interval() {
        let queue = PropagationQueue::new();
        let worker = PropagationWorker::new(queue, NoOpCostUpdater)
            .with_poll_interval(Duration::from_millis(50));

        assert_eq!(worker.poll_interval, Duration::from_millis(50));
    }

    #[test]
    fn worker_queue_depth() {
        let queue = PropagationQueue::new();
        let worker = PropagationWorker::new(queue.clone(), NoOpCostUpdater);
        let notebook_id = make_notebook_id();

        assert_eq!(worker.queue_depth(), 0);

        queue.enqueue(PropagationJob::new(notebook_id, vec![make_entry_id()], 0.5));
        assert_eq!(worker.queue_depth(), 1);
    }

    #[tokio::test]
    async fn worker_start_and_shutdown() {
        let queue = PropagationQueue::new();
        let mut worker = PropagationWorker::new(queue.clone(), NoOpCostUpdater)
            .with_poll_interval(Duration::from_millis(10));

        let notebook_id = make_notebook_id();
        queue.enqueue(PropagationJob::new(notebook_id, vec![make_entry_id()], 0.5));

        let handle = worker.start();

        // Give worker time to process
        tokio::time::sleep(Duration::from_millis(50)).await;

        // Verify job was processed
        assert!(queue.is_empty());

        // Shutdown
        worker.shutdown();
        let _ = tokio::time::timeout(Duration::from_millis(100), handle).await;
    }

    #[tokio::test]
    async fn worker_processes_multiple_jobs() {
        let queue = PropagationQueue::new();
        let mut worker = PropagationWorker::new(queue.clone(), NoOpCostUpdater)
            .with_poll_interval(Duration::from_millis(10));

        let notebook_id = make_notebook_id();

        // Enqueue multiple jobs
        for _ in 0..5 {
            queue.enqueue(PropagationJob::new(notebook_id, vec![make_entry_id()], 0.5));
        }

        let handle = worker.start();

        // Give worker time to process all jobs
        tokio::time::sleep(Duration::from_millis(100)).await;

        // Verify all jobs processed
        assert!(queue.is_empty());

        let stats = worker.stats();
        assert_eq!(stats.jobs_processed, 5);

        worker.shutdown();
        let _ = tokio::time::timeout(Duration::from_millis(100), handle).await;
    }
}
