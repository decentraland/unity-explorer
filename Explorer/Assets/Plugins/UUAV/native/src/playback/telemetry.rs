//! Cumulative audio-pipeline diagnostics, shared between the playback
//! threads and the engine-facing stats getter.

use std::sync::Arc;
use std::sync::atomic::{AtomicU64, Ordering};

use crate::AudioPipelineStats;

/// Per-player audio counters; relaxed atomics bumped on the hot paths and
/// snapshotted by the stats getter, so a snapshot is monotonic per field
/// but not cross-field consistent. Owned by the player, so the values
/// accumulate across url switches and seeks.
#[derive(Default)]
pub(crate) struct AudioTelemetry {
    /// Interleaved samples deleted by drift correction (audio ran late
    /// against the master clock).
    drift_dropped_samples: AtomicU64,
    /// Reads answered with silence because audio ran ahead of the clock.
    silence_pulls: AtomicU64,
    /// Decoded frames that had to wait for ring space at least once.
    /// Grows in normal steady state (the ring runs full by design); a
    /// starvation signal only together with a low ring fill.
    ring_stalls: AtomicU64,
    /// Decoded-ring occupancy after the last read, interleaved samples.
    ring_fill_samples: AtomicU64,
}

pub(crate) type SharedAudioTelemetry = Arc<AudioTelemetry>;

impl AudioTelemetry {
    pub(crate) fn add_drift_dropped(&self, samples: usize) {
        self.drift_dropped_samples
            .fetch_add(samples as u64, Ordering::Relaxed);
    }

    pub(crate) fn count_silence_pull(&self) {
        self.silence_pulls.fetch_add(1, Ordering::Relaxed);
    }

    pub(crate) fn count_ring_stall(&self) {
        self.ring_stalls.fetch_add(1, Ordering::Relaxed);
    }

    pub(crate) fn store_ring_fill(&self, samples: usize) {
        self.ring_fill_samples
            .store(samples as u64, Ordering::Relaxed);
    }

    pub(crate) fn snapshot(&self) -> AudioPipelineStats {
        AudioPipelineStats {
            drift_dropped_samples: self.drift_dropped_samples.load(Ordering::Relaxed),
            silence_pulls: self.silence_pulls.load(Ordering::Relaxed),
            ring_stalls: self.ring_stalls.load(Ordering::Relaxed),
            ring_fill_samples: self.ring_fill_samples.load(Ordering::Relaxed),
        }
    }
}
