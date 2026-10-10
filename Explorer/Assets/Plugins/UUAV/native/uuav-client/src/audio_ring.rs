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
//!
//! The ring also keeps the consumption ledger the pull-pacing feedback is
//! built on: every sample that leaves the ring — read, dropped at the
//! watermark, or flushed by [`AudioRing::clear`] — counts as *removed*,
//! so the helper never re-pulls content the ring already disposed of.

use std::collections::VecDeque;
use std::num::NonZeroUsize;
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
    /// Channel count of the negotiated output format; drop amounts are
    /// aligned to it so a drop can never shift the interleave.
    channels: NonZeroUsize,
    primed: bool,
    /// Frames removed since the last [`AudioRing::reset`]: read + dropped +
    /// cleared. The pull-pacing feedback ledger; resets with the helper
    /// binding, unlike the cumulative diagnostics below.
    removed_frames: u64,
    // cumulative diagnostics; survive reset() and clear()
    /// Primed -> unprimed transitions: one per audible gap.
    underruns: u64,
    /// Samples dropped by the high watermark.
    watermark_dropped: u64,
    /// Samples accepted from the helper.
    written: u64,
    /// Samples handed to the audio thread.
    read: u64,
}

impl Ring {
    fn count_removed_samples(&mut self, samples: usize) {
        let frames = (samples / self.channels) as u64;
        self.removed_frames = self.removed_frames.saturating_add(frames);
    }
}

fn sanitize_channels(channels: i32) -> NonZeroUsize {
    NonZeroUsize::new(channels.max(1) as usize).unwrap_or(NonZeroUsize::MIN)
}

/// Plain snapshot of the ring's level and cumulative counters.
#[derive(Default, Clone, Copy)]
pub struct AudioRingStats {
    pub fill_samples: u64,
    pub samples_per_second: u64,
    pub primed: bool,
    pub underruns: u64,
    pub watermark_dropped: u64,
    pub written: u64,
    pub read: u64,
}

pub struct AudioRing(Mutex<Ring>);

impl AudioRing {
    pub fn new(sample_rate: i32, channels: i32) -> Self {
        let channels = sanitize_channels(channels);
        let samples_per_second = (sample_rate.max(1) as usize).saturating_mul(channels.get());
        Self(Mutex::new(Ring {
            samples: VecDeque::with_capacity(duration_samples(samples_per_second, CAPACITY)),
            samples_per_second,
            channels,
            primed: false,
            removed_frames: 0,
            underruns: 0,
            watermark_dropped: 0,
            written: 0,
            read: 0,
        }))
    }

    /// IO-thread side: queue one packet, dropping the oldest samples above
    /// the high watermark.
    pub fn write(&self, samples: &[f32]) {
        let Ok(mut ring) = self.0.lock() else {
            return;
        };
        ring.samples.extend(samples.iter().copied());
        ring.written = ring.written.saturating_add(samples.len() as u64);

        // both the cap and the drop amount are whole frames, so a drop can
        // never swap the channel interleave
        let cap = duration_samples(ring.samples_per_second, HIGH_WATERMARK);
        let cap = cap.saturating_sub(cap % ring.channels);
        let excess = ring.samples.len().saturating_sub(cap);
        if excess > 0 {
            let missing_to_frame = ring.channels.get().saturating_sub(excess % ring.channels);
            let pad = missing_to_frame % ring.channels;
            let excess = excess.saturating_add(pad).min(ring.samples.len());
            ring.samples.drain(..excess);
            ring.watermark_dropped = ring.watermark_dropped.saturating_add(excess as u64);
            ring.count_removed_samples(excess);
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
        ring.read = ring.read.saturating_add(available as u64);
        ring.count_removed_samples(available);
        if available < dst.len() {
            // underrun: silence the rest and gather a fresh cushion before
            // resuming, one longer gap instead of machine-gun stutter
            ring.primed = false;
            ring.underruns = ring.underruns.saturating_add(1);
            for slot in dst.iter_mut().skip(available) {
                *slot = 0.0;
            }
        }
        available
    }

    /// Flushes buffered samples that became stale (seek, media switch)
    /// while the helper binding stays live: the flushed samples count as
    /// removed so the pull pacing never re-sends them. Cumulative
    /// diagnostics survive.
    pub fn clear(&self) {
        if let Ok(mut ring) = self.0.lock() {
            let flushed = ring.samples.len();
            ring.samples.clear();
            ring.primed = false;
            ring.count_removed_samples(flushed);
        }
    }

    /// Drops everything and restarts the feedback ledger (output format
    /// changed / helper died); cumulative diagnostics survive.
    pub fn reset(&self, sample_rate: i32, channels: i32) {
        if let Ok(mut ring) = self.0.lock() {
            ring.samples.clear();
            ring.primed = false;
            ring.removed_frames = 0;
            ring.channels = sanitize_channels(channels);
            ring.samples_per_second =
                (sample_rate.max(1) as usize).saturating_mul(ring.channels.get());
        }
    }

    /// Frames removed since the last [`Self::reset`]; the pull-pacing
    /// feedback value.
    pub fn removed_frames(&self) -> u64 {
        self.0.lock().map_or(0, |ring| ring.removed_frames)
    }

    /// Level and cumulative counters, as one consistent snapshot.
    pub fn stats(&self) -> AudioRingStats {
        self.0.lock().map_or_else(
            |_| AudioRingStats::default(),
            |ring| AudioRingStats {
                fill_samples: ring.samples.len() as u64,
                samples_per_second: ring.samples_per_second as u64,
                primed: ring.primed,
                underruns: ring.underruns,
                watermark_dropped: ring.watermark_dropped,
                written: ring.written,
                read: ring.read,
            },
        )
    }
}

#[allow(clippy::cast_precision_loss)] // audio rates are far below 2^52
fn duration_samples(samples_per_second: usize, duration: Duration) -> usize {
    (samples_per_second as f64 * duration.as_secs_f64()) as usize
}

#[cfg(test)]
mod tests {
    use super::*;

    const RATE: i32 = 48_000;
    const CHANNELS: i32 = 2;

    fn samples(count: usize) -> Vec<f32> {
        vec![0.5; count]
    }

    fn prime_samples() -> usize {
        duration_samples((RATE * CHANNELS) as usize, PRIME)
    }

    fn watermark_samples() -> usize {
        duration_samples((RATE * CHANNELS) as usize, HIGH_WATERMARK)
    }

    #[test]
    fn stays_silent_until_primed_without_counting_an_underrun() {
        let ring = AudioRing::new(RATE, CHANNELS);
        ring.write(&samples(prime_samples() - 2));

        let mut dst = [1.0_f32; 64];
        assert_eq!(ring.read(&mut dst), 0);
        // priming does not touch dst: the FFI caller silences on 0. Compared
        // by bits, because the sentinel is the literal this test just wrote
        // and never a computed value
        assert!(dst.iter().all(|&s| s.to_bits() == 1.0_f32.to_bits()));
        assert_eq!(ring.stats().underruns, 0);
        assert!(!ring.stats().primed);
    }

    #[test]
    fn underrun_counts_once_per_gap_and_reprimes() {
        let ring = AudioRing::new(RATE, CHANNELS);
        let fill = prime_samples();
        ring.write(&samples(fill));

        // over-read: partial fill, one underrun, back to priming
        let mut dst = vec![1.0_f32; fill + 64];
        assert_eq!(ring.read(&mut dst), fill);
        assert!(dst.get(fill..).is_some_and(|tail| tail.iter().all(|&s| s == 0.0)));
        assert_eq!(ring.stats().underruns, 1);
        assert!(!ring.stats().primed);

        // silent reads while re-priming do not count more underruns
        assert_eq!(ring.read(&mut dst), 0);
        assert_eq!(ring.stats().underruns, 1);
    }

    #[test]
    fn watermark_drops_the_exact_excess_channel_aligned() {
        let ring = AudioRing::new(RATE, CHANNELS);
        let over = 100;
        ring.write(&samples(watermark_samples() + over));

        let stats = ring.stats();
        assert_eq!(stats.watermark_dropped, over as u64);
        assert_eq!(stats.fill_samples % CHANNELS as u64, 0);
        assert_eq!(stats.fill_samples, watermark_samples() as u64);
    }

    #[test]
    fn watermark_drop_stays_frame_aligned_at_odd_caps() {
        // 22050 Hz stereo: 150 ms is 6615 samples — not a channel multiple
        let ring = AudioRing::new(22_050, CHANNELS);
        let per_second = (22_050 * CHANNELS) as usize;
        let cap = duration_samples(per_second, HIGH_WATERMARK);
        assert_eq!(cap % CHANNELS as usize, 1, "test premise: odd cap");

        ring.write(&samples(cap + 99));

        let stats = ring.stats();
        assert_eq!(stats.fill_samples % CHANNELS as u64, 0);
        assert_eq!(stats.watermark_dropped % CHANNELS as u64, 0);
    }

    #[test]
    fn bookkeeping_tracks_written_read_and_removed() {
        let ring = AudioRing::new(RATE, CHANNELS);
        let fill = prime_samples();
        ring.write(&samples(fill));

        let mut dst = vec![0.0_f32; 512];
        assert_eq!(ring.read(&mut dst), 512);

        let stats = ring.stats();
        assert_eq!(stats.written, fill as u64);
        assert_eq!(stats.read, 512);
        assert_eq!(stats.fill_samples, (fill - 512) as u64);
        assert_eq!(ring.removed_frames(), 512 / CHANNELS as u64);
    }

    #[test]
    fn clear_counts_removed_but_keeps_diagnostics() {
        let ring = AudioRing::new(RATE, CHANNELS);
        let fill = prime_samples();
        ring.write(&samples(fill));
        ring.clear();

        let stats = ring.stats();
        assert_eq!(stats.fill_samples, 0);
        assert!(!stats.primed);
        assert_eq!(stats.written, fill as u64);
        assert_eq!(ring.removed_frames(), (fill / CHANNELS as usize) as u64);

        // cleared content is never served again
        let mut dst = [1.0_f32; 64];
        assert_eq!(ring.read(&mut dst), 0);
    }

    #[test]
    fn reset_restarts_the_feedback_ledger_but_keeps_diagnostics() {
        let ring = AudioRing::new(RATE, CHANNELS);
        let fill = prime_samples();
        ring.write(&samples(fill + 64));
        let mut dst = vec![0.0_f32; fill + 128];
        ring.read(&mut dst);
        assert!(ring.removed_frames() > 0);
        let underruns = ring.stats().underruns;
        assert_eq!(underruns, 1);

        ring.reset(RATE, CHANNELS);

        assert_eq!(ring.removed_frames(), 0);
        let stats = ring.stats();
        assert_eq!(stats.underruns, underruns);
        assert_eq!(stats.fill_samples, 0);
        assert!(stats.written > 0);
    }
}
