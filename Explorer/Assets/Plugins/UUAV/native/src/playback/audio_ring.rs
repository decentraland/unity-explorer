//! Lock-free SPSC audio ring

use parking_lot::Mutex;
use ringbuf::traits::{Consumer, Observer, Producer, Split};
use ringbuf::{HeapCons, HeapProd, HeapRb};
use std::num::NonZeroUsize;
use std::sync::Arc;

use crate::AudioOptions;

/// Decoded audio buffered ahead of the clock, in seconds; the fixed
/// capacity of the sample ring.
const AUDIO_BUFFER_SECONDS: f64 = 1.0;
/// Audio drift beyond this is corrected by padding silence / dropping samples.
const AUDIO_DRIFT_TOLERANCE: f64 = 0.15;
/// Decoded audio frames are ~10 ms or longer, so one second of buffered
/// audio carries well under this many timestamps.
const PTS_MARKERS_CAP: usize = 256;

/// Media time anchor: ties a media timestamp to a position in the sample
/// stream.
#[derive(Clone, Copy)]
struct PtsMarker {
    /// Stream position, in interleaved samples since the ring pair was
    /// created.
    position: u64,
    /// Media time of the sample at `position`, in seconds.
    pts: f64,
}

/// What the audio callback must do after drift correction.
pub(super) enum ClockSync {
    /// Audio is aligned with the master clock (late samples were already
    /// dropped): consume normally.
    Consume,
    /// Audio is ahead of the master clock: emit silence to hold it back.
    EmitSilence,
}

/// Slot through which the receiver half is handed to the audio thread.
pub(super) type SharedAudioReceiver = Arc<Mutex<Option<AudioReceiver>>>;

/// The worker's grip on the whole ring: the sender half it fills, paired
/// with the slot its receiver half was published into. Both halves are
/// created and replaced together, so they always originate from the same
/// ring.
pub(super) struct AudioRing {
    tx: AudioSender,
    rx_slot: SharedAudioReceiver,
}

impl AudioRing {
    /// Creates a ring for `audio_options` and publishes its receiver into
    /// `rx_slot`. `playback_rate` is the varispeed the buffered samples
    /// were converted for; it scales the receiver's media-time math.
    pub(super) fn new(
        audio_options: AudioOptions,
        playback_rate: f64,
        rx_slot: SharedAudioReceiver,
    ) -> Self {
        let (tx, rx) = split(audio_options, playback_rate);
        *rx_slot.lock() = Some(rx);
        Self { tx, rx_slot }
    }

    /// Replaces both halves with a fresh ring for `audio_options` at
    /// `playback_rate`, discarding whatever the old ring still buffered.
    pub(super) fn replace(&mut self, audio_options: AudioOptions, playback_rate: f64) {
        let (tx, rx) = split(audio_options, playback_rate);
        self.tx = tx;
        *self.rx_slot.lock() = Some(rx);
    }

    /// Appends a decoded frame when the ring has room. An empty ring always
    /// accepts, so an oversized frame cannot deadlock the pipeline.
    pub(super) fn try_extend(&mut self, pts: Option<f64>, samples: &[f32]) -> bool {
        self.tx.try_extend(pts, samples)
    }

    /// Whether the receiver has consumed everything pushed so far.
    pub(super) fn is_drained(&self) -> bool {
        self.tx.is_drained()
    }
}

fn split(audio_options: AudioOptions, playback_rate: f64) -> (AudioSender, AudioReceiver) {
    let per_second = (audio_options.sample_rate.get() as usize)
        .saturating_mul(audio_options.channels_usize().get());
    let capacity = (per_second as f64 * AUDIO_BUFFER_SECONDS) as usize;

    let (samples_tx, samples_rx) = HeapRb::new(capacity).split();
    let (markers_tx, markers_rx) = HeapRb::new(PTS_MARKERS_CAP).split();

    (
        AudioSender {
            samples: samples_tx,
            markers: markers_tx,
            written: 0,
        },
        AudioReceiver {
            samples: samples_rx,
            markers: markers_rx,
            read: 0,
            anchor: None,
            pending: None,
            audio_options,
            playback_rate,
        },
    )
}

/// Worker-side half: pushes decoded samples and their timestamps.
struct AudioSender {
    samples: HeapProd<f32>,
    markers: HeapProd<PtsMarker>,
    /// Interleaved samples pushed since creation; stream position of the
    /// next pushed sample.
    written: u64,
}

impl AudioSender {
    fn try_extend(&mut self, pts: Option<f64>, samples: &[f32]) -> bool {
        if self.samples.vacant_len() < samples.len() && !self.samples.is_empty() {
            return false;
        }
        if let Some(pts) = pts {
            // a full marker ring only costs one re-anchor: harmless
            let _ = self.markers.try_push(PtsMarker {
                position: self.written,
                pts,
            });
        }
        // when accepted into an empty ring, a frame larger than the whole
        // ring is truncated (the VecDeque predecessor grew unboundedly);
        // sane decoder output is orders of magnitude below the capacity
        let pushed = self.samples.push_slice(samples);
        self.written = self.written.saturating_add(pushed as u64);
        true
    }

    fn is_drained(&self) -> bool {
        self.samples.is_empty()
    }
}

/// Audio-thread half: pops samples wait-free and tracks their media time.
pub(super) struct AudioReceiver {
    samples: HeapCons<f32>,
    markers: HeapCons<PtsMarker>,
    /// Interleaved samples consumed since creation; stream position of the
    /// sample at the ring head.
    read: u64,
    /// Newest marker at or before `read`; media time is extrapolated from
    /// it, `None` until timed samples arrive (drift correction disabled).
    anchor: Option<PtsMarker>,
    /// A marker taken off the ring that still lies ahead of `read`.
    pending: Option<PtsMarker>,
    audio_options: AudioOptions,
    /// Varispeed the samples were converted for: one output frame covers
    /// `playback_rate / sample_rate` media seconds. Constant per ring —
    /// a rate change replaces the whole ring.
    playback_rate: f64,
}

impl AudioReceiver {
    pub(super) const fn channel_count(&self) -> NonZeroUsize {
        self.audio_options.channels_usize()
    }

    fn rate_f64(&self) -> f64 {
        self.audio_options.sample_rate_f64()
    }

    /// Media time of the sample at the ring head: the newest marker at or
    /// before `read`, extrapolated by the frames consumed past it.
    fn next_pts(&mut self) -> Option<f64> {
        loop {
            let marker = match self.pending.take() {
                Some(marker) => marker,
                None => match self.markers.try_pop() {
                    Some(marker) => marker,
                    None => break,
                },
            };
            if marker.position <= self.read {
                self.anchor = Some(marker);
            } else {
                self.pending = Some(marker);
                break;
            }
        }

        self.anchor.map(|marker| {
            let samples_past = (self.read.saturating_sub(marker.position)) as usize;
            let frames_past = samples_past / self.channel_count();
            marker.pts + frames_past as f64 * self.playback_rate / self.rate_f64()
        })
    }

    /// Applies master-clock drift correction.
    pub(super) fn sync_to_clock(&mut self, now: f64) -> ClockSync {
        let Some(pts) = self.next_pts() else {
            return ClockSync::Consume;
        };
        let drift = pts - now;
        if drift > AUDIO_DRIFT_TOLERANCE {
            return ClockSync::EmitSilence;
        }
        if drift < -AUDIO_DRIFT_TOLERANCE {
            let channels_nz = self.channel_count();
            // a late media span occupies span / rate output frames
            let late_frames = (-drift * self.rate_f64() / self.playback_rate) as usize;
            let buffered_frames = self.samples.occupied_len() / channels_nz;
            let drop_samples = late_frames
                .min(buffered_frames)
                .saturating_mul(channels_nz.get());
            let dropped = self.samples.skip(drop_samples);
            self.read = self.read.saturating_add(dropped as u64);
        }
        ClockSync::Consume
    }

    /// Copies whole frames into `out`, zero-fills the rest, and advances
    /// the stream position; returns the number of frames copied.
    pub(super) fn read_into(&mut self, out: &mut [f32]) -> usize {
        let channels_nz = self.channel_count();
        let requested_frames = out.len() / channels_nz;
        let buffered_frames = self.samples.occupied_len() / channels_nz;
        let copied_samples = requested_frames
            .min(buffered_frames)
            .saturating_mul(channels_nz.get());

        let copied = match out.get_mut(..copied_samples) {
            Some(dst) => self.samples.pop_slice(dst),
            None => 0,
        };
        if let Some(rest) = out.get_mut(copied..) {
            rest.fill(0.0);
        }

        self.read = self.read.saturating_add(copied as u64);
        copied / channels_nz
    }
}
