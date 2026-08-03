
use parking_lot::Mutex;
use ringbuf::traits::{Consumer, Observer, Producer, Split};
use ringbuf::{HeapCons, HeapProd, HeapRb};
use std::num::NonZeroUsize;
use std::sync::Arc;

use crate::AudioOptions;

const AUDIO_BUFFER_SECONDS: f64 = 1.0;
const AUDIO_DRIFT_TOLERANCE: f64 = 0.15;
const PTS_MARKERS_CAP: usize = 256;

#[derive(Clone, Copy)]
struct PtsMarker {
    position: u64,
    pts: f64,
}

pub(super) enum ClockSync {
    Consume,
    EmitSilence,
}

pub(super) type SharedAudioReceiver = Arc<Mutex<Option<AudioReceiver>>>;

pub(super) struct AudioRing {
    tx: AudioSender,
    rx_slot: SharedAudioReceiver,
}

impl AudioRing {
    pub(super) fn new(
        audio_options: AudioOptions,
        playback_rate: f64,
        rx_slot: SharedAudioReceiver,
    ) -> Self {
        let (tx, rx) = split(audio_options, playback_rate);
        *rx_slot.lock() = Some(rx);
        Self { tx, rx_slot }
    }

    pub(super) fn replace(&mut self, audio_options: AudioOptions, playback_rate: f64) {
        let (tx, rx) = split(audio_options, playback_rate);
        self.tx = tx;
        *self.rx_slot.lock() = Some(rx);
    }

    pub(super) fn try_extend(&mut self, pts: Option<f64>, samples: &[f32]) -> bool {
        self.tx.try_extend(pts, samples)
    }

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

struct AudioSender {
    samples: HeapProd<f32>,
    markers: HeapProd<PtsMarker>,
    written: u64,
}

impl AudioSender {
    fn try_extend(&mut self, pts: Option<f64>, samples: &[f32]) -> bool {
        if self.samples.vacant_len() < samples.len() && !self.samples.is_empty() {
            return false;
        }
        if let Some(pts) = pts {
            let _ = self.markers.try_push(PtsMarker {
                position: self.written,
                pts,
            });
        }
        let pushed = self.samples.push_slice(samples);
        self.written = self.written.saturating_add(pushed as u64);
        true
    }

    fn is_drained(&self) -> bool {
        self.samples.is_empty()
    }
}

pub(super) struct AudioReceiver {
    samples: HeapCons<f32>,
    markers: HeapCons<PtsMarker>,
    read: u64,
    anchor: Option<PtsMarker>,
    pending: Option<PtsMarker>,
    audio_options: AudioOptions,
    playback_rate: f64,
}

impl AudioReceiver {
    pub(super) const fn channel_count(&self) -> NonZeroUsize {
        self.audio_options.channels_usize()
    }

    fn rate_f64(&self) -> f64 {
        self.audio_options.sample_rate_f64()
    }

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
