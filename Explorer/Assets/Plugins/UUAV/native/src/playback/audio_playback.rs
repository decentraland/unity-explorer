use anyhow::Result;
use parking_lot::Mutex;
use std::os::raw::c_int;
use std::ptr;
use std::sync::Arc;
use std::thread;

use super::audio_ring::{AudioRing, ClockSync, SharedAudioReceiver};
use super::fill_silence;
use super::transport::AtomicTransport;
use super::util::{AtomicSeekSlot, PLAYBACK_POLL, ReadOnlyCancelToken};
use crate::AudioOptionsView;
use crate::audio_decoder::AudioDecoder;
use crate::ffutil::{Decoded, OwnedPacket, Stream};

/// The audio-thread half of a playback's audio: drains the ring
/// [`AudioPlayback`] fills, slaved to the master clock.
pub(super) struct AudioReader {
    /// Receiver half of the decoded-audio ring; `None` until the audio
    /// decoder reports its format. The mutex is uncontended in steady
    /// state: the worker takes it only to swap in a fresh receiver.
    rx: SharedAudioReceiver,
    /// Engine audio configuration; the silence layout when no decoded
    /// format exists yet.
    audio_out: AudioOptionsView,
}

impl AudioReader {
    pub(super) fn new(audio_out: AudioOptionsView) -> Self {
        Self {
            rx: Arc::new(Mutex::new(None)),
            audio_out,
        }
    }

    /// The slot [`AudioPlayback`] publishes its receiver halves into.
    pub(super) fn rx_slot(&self) -> SharedAudioReceiver {
        Arc::clone(&self.rx)
    }

    /// [audio thread] Fills interleaved FLT; pads silence on underrun,
    /// never blocks; returns frames actually copied.
    pub(super) fn read(&self, transport: &AtomicTransport, dst: *mut f32, frames: usize) -> i32 {
        let mut guard = self.rx.lock();
        let Some(ring) = guard.as_mut() else {
            // no decoded format yet: silence in the engine's configured layout
            fill_silence(dst, frames, self.audio_out.current().channels_usize());
            return 0;
        };

        let Some(total) = frames.checked_mul(ring.channel_count().get()) else {
            return 0;
        };
        // the engine guarantees dst holds nb_frames * channels floats
        let out = unsafe { std::slice::from_raw_parts_mut(dst, total) };

        if !transport.is_playing() {
            out.fill(0.0);
            return 0;
        }

        match ring.sync_to_clock(transport.now()) {
            ClockSync::EmitSilence => {
                out.fill(0.0);
                0
            }
            ClockSync::Consume => i32::try_from(ring.read_into(out)).unwrap_or(0),
        }
    }
}

/// The worker-thread half of a playback's audio: decodes the audio stream
/// and feeds the ring the engine's audio thread drains.
pub(super) struct AudioPlayback {
    decoder: AudioDecoder,
    ring: AudioRing,
    audio_out: AudioOptionsView,
    stream_index: c_int,
    /// Varispeed currently applied; new rings must carry it so their
    /// media-time math matches the converted samples.
    playback_rate: f64,
}

impl AudioPlayback {
    pub(super) fn new(
        stream: Stream,
        stream_index: c_int,
        audio_out: AudioOptionsView,
        rx_slot: SharedAudioReceiver,
    ) -> Result<Self> {
        let decoder = AudioDecoder::new(stream, audio_out.current())?;
        let playback_rate = super::unit::DEFAULT_PLAYBACK_RATE;
        let ring = AudioRing::new(decoder.audio_options(), playback_rate, rx_slot);
        Ok(Self {
            decoder,
            ring,
            audio_out,
            stream_index,
            playback_rate,
        })
    }

    /// Applies a new varispeed rate: rebuilds the resampler ratio and
    /// drops already-converted samples (same policy as an audio reconfig).
    pub(super) fn set_rate(&mut self, rate: f64) {
        if self.decoder.set_rate(rate) {
            self.playback_rate = rate;
            self.ring.replace(self.decoder.audio_options(), rate);
        }
    }

    pub(super) const fn handles(&self, stream_index: c_int) -> bool {
        self.stream_index == stream_index
    }

    /// Sends the packet and moves every ready frame into the ring,
    /// following engine audio reconfiguration (`uuav_update_audio_out`).
    pub(super) fn handle_packet(
        &mut self,
        packet: &mut OwnedPacket,
        start_offset: f64,
        cancel: &ReadOnlyCancelToken,
        seek: &AtomicSeekSlot,
        poll_controls: &dyn Fn(),
    ) -> Result<()> {
        let options = self.audio_out.current();
        if self.decoder.set_output(options) {
            self.ring.replace(options, self.playback_rate);
        }
        self.decoder.send(packet.as_mut_ptr())?;
        self.pump(start_offset, cancel, seek, poll_controls)
    }

    /// Sends end-of-stream and moves the remaining samples into the ring.
    pub(super) fn drain(
        &mut self,
        start_offset: f64,
        cancel: &ReadOnlyCancelToken,
        seek: &AtomicSeekSlot,
        poll_controls: &dyn Fn(),
    ) -> Result<()> {
        self.decoder.send(ptr::null())?;
        self.pump(start_offset, cancel, seek, poll_controls)
    }

    /// Discards everything belonging to the pre-seek position.
    pub(super) fn flush_for_seek(&mut self) {
        self.decoder.flush();
        self.ring
            .replace(self.decoder.audio_options(), self.playback_rate);
    }

    /// Whether the audio thread has consumed every pushed sample.
    pub(super) fn is_drained(&self) -> bool {
        self.ring.is_drained()
    }

    /// Moves every frame the decoder has ready into the ring, waiting for
    /// room while staying responsive to stop/seek commands.
    /// The wait also keeps `poll_controls` applied: while not playing the
    /// ring does not drain, so a queued play would otherwise never land.
    fn pump(
        &mut self,
        start_offset: f64,
        cancel: &ReadOnlyCancelToken,
        seek: &AtomicSeekSlot,
        poll_controls: &dyn Fn(),
    ) -> Result<()> {
        loop {
            match self.decoder.receive()? {
                Decoded::Frame(frame) => {
                    let pts = frame.pts().map(|pts| pts - start_offset);
                    loop {
                        if cancel.is_cancelled() || seek.is_pending() {
                            // the frame is obsolete: the ring is about to
                            // be flushed
                            return Ok(());
                        }
                        // after the cancel check: a retired unit must not
                        // consume a command meant for its successor
                        poll_controls();
                        if self.ring.try_extend(pts, frame.samples()) {
                            break;
                        }
                        thread::sleep(PLAYBACK_POLL);
                    }
                }
                Decoded::Again | Decoded::Eof => return Ok(()),
            }
        }
    }
}
