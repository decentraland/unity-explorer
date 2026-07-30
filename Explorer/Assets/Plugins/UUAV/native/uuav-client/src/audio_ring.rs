//! Per-player jitter ring between the helper's audio packets and Unity's
//! audio thread. The samples are already media-clock aligned by the core;
//! this ring only absorbs transport jitter and the slow skew between the
//! helper's timer and Unity's audio hardware clock:
//!
//! - it stays silent until [`PRIME`] worth of audio queued (so a burst of
//!   underruns doesn't stutter right after start/seek), re-priming after
//!   every underrun;
//! - above [`HIGH_WATERMARK`] the oldest samples are dropped, bounding the
//!   added latency when Unity consumes slower than the helper produces.

use std::collections::VecDeque;
use std::sync::Mutex;
use std::time::Duration;

const PRIME: Duration = Duration::from_millis(40);
const HIGH_WATERMARK: Duration = Duration::from_millis(150);
/// Preallocated ring capacity (samples are only dropped, never reallocated).
const CAPACITY: Duration = Duration::from_millis(250);

struct Ring {
    samples: VecDeque<f32>,
    /// Samples per second x channels, from the negotiated output format.
    samples_per_second: usize,
    primed: bool,
}

pub struct AudioRing(Mutex<Ring>);

impl AudioRing {
    pub fn new(sample_rate: i32, channels: i32) -> Self {
        let samples_per_second =
            (sample_rate.max(1) as usize).saturating_mul(channels.max(1) as usize);
        Self(Mutex::new(Ring {
            samples: VecDeque::with_capacity(duration_samples(samples_per_second, CAPACITY)),
            samples_per_second,
            primed: false,
        }))
    }

    /// IO-thread side: queue one packet, dropping the oldest samples above
    /// the high watermark.
    pub fn write(&self, samples: &[f32]) {
        let Ok(mut ring) = self.0.lock() else {
            return;
        };
        ring.samples.extend(samples.iter().copied());

        let cap = duration_samples(ring.samples_per_second, HIGH_WATERMARK);
        let excess = ring.samples.len().saturating_sub(cap);
        if excess > 0 {
            ring.samples.drain(..excess);
        }
    }

    /// Audio-thread side: fills `dst` and returns the number of samples
    /// written (0 while priming); the caller pads the remainder as silence.
    pub fn read(&self, dst: &mut [f32]) -> usize {
        let Ok(mut ring) = self.0.lock() else {
            return 0;
        };

        if !ring.primed {
            if ring.samples.len() < duration_samples(ring.samples_per_second, PRIME) {
                return 0;
            }
            ring.primed = true;
        }

        let available = ring.samples.len().min(dst.len());
        for slot in dst.iter_mut().take(available) {
            // pop_front on a non-empty deque; the bound above guarantees it
            *slot = ring.samples.pop_front().unwrap_or_default();
        }
        if available < dst.len() {
            // underrun: silence the rest and gather a fresh cushion before
            // resuming, one longer gap instead of machine-gun stutter
            ring.primed = false;
            for slot in dst.iter_mut().skip(available) {
                *slot = 0.0;
            }
        }
        available
    }

    /// Drops everything (output format changed / media switched).
    pub fn reset(&self, sample_rate: i32, channels: i32) {
        if let Ok(mut ring) = self.0.lock() {
            ring.samples.clear();
            ring.primed = false;
            ring.samples_per_second =
                (sample_rate.max(1) as usize).saturating_mul(channels.max(1) as usize);
        }
    }
}

#[allow(clippy::cast_precision_loss)] // audio rates are far below 2^52
fn duration_samples(samples_per_second: usize, duration: Duration) -> usize {
    (samples_per_second as f64 * duration.as_secs_f64()) as usize
}
