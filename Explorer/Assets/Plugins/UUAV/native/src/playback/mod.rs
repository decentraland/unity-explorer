//! Playback of a single url: the blocking open/run loop the player drives
//! on a dedicated thread, and the resources the engine-facing threads
//! consume.
//!
//! Everything here is internal to the module except what the player
//! orchestrates: [`PlaybackUnit`] with its demux/decode pipeline, the
//! [`CancelToken`] that stops it, and the [`fill_silence`]/[`report`]
//! helpers.

mod audio_playback;
mod audio_ring;
mod clock;
mod control;
mod input;
mod telemetry;
mod transport;
mod unit;
mod util;
mod video_playback;

pub(crate) use control::ControlPush;
pub(crate) use telemetry::{AudioTelemetry, SharedAudioTelemetry};
pub(crate) use unit::{DEFAULT_PLAYBACK_RATE, PlaybackUnit, UnitControls};
pub(crate) use util::CancelToken;
pub(crate) use util::ReadOnlyCancelToken;

use std::num::NonZeroUsize;

/// Zero-fills an engine-provided interleaved sample buffer.
pub(crate) fn fill_silence(dst: *mut f32, frames: usize, channels: NonZeroUsize) {
    let Some(total) = frames.checked_mul(channels.get()) else {
        return;
    };
    // the engine guarantees dst holds nb_frames * channels floats
    let out = unsafe { std::slice::from_raw_parts_mut(dst, total) };
    out.fill(0.0);
}
