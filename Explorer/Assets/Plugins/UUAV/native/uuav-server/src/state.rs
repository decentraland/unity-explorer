//! Read-side adaptation: samples the core's C getters into wire snapshots.

use std::ffi::CStr;
use std::os::raw::c_char;
use uuav_core as core;
use uuav_ipc::protocol::{
    ControlsWire, MediaInfoWire, PlayerId, PlayerStateWire, StateUpdateWire,
};

/// Converts a core `ResultFFI` into a plain `Result`, freeing the native
/// error string exactly once as the C contract requires.
#[allow(clippy::needless_pass_by_value)] // by-value = ownership of the native string
pub fn consume_result(result: core::ResultFFI) -> Result<(), String> {
    if result.error_message.is_null() {
        return Ok(());
    }
    let message = unsafe { CStr::from_ptr(result.error_message) }
        .to_string_lossy()
        .into_owned();
    unsafe { core::uuav_string_free(result.error_message.cast_mut()) };
    Err(message)
}

pub fn snapshot(id: PlayerId) -> StateUpdateWire {
    let mut media_time = 0.0_f64;
    let media_time = consume_result(unsafe { core::uuav_player_current_time(id, &mut media_time) })
        .ok()
        .map(|()| media_time);

    let mut duration = 0.0_f64;
    let duration = consume_result(unsafe { core::uuav_player_duration(id, &mut duration) })
        .ok()
        .map(|()| duration);

    let mut controls = core::ControlsState {
        rate: 0.0,
        play: 0,
        play_pending: 0,
        looping: 0,
        looping_pending: 0,
        rate_pending: 0,
    };
    let controls =
        consume_result(unsafe { core::uuav_player_current_controls_state(id, &mut controls) })
            .ok()
            .map_or_else(ControlsWire::default, |()| ControlsWire {
                rate: controls.rate,
                play: controls.play != 0,
                play_pending: controls.play_pending != 0,
                looping: controls.looping != 0,
                looping_pending: controls.looping_pending != 0,
                rate_pending: controls.rate_pending != 0,
            });

    let mut size = core::VideoSize {
        width: 0,
        height: 0,
    };
    let video_size = consume_result(unsafe { core::uuav_player_get_video_size(id, &mut size) })
        .ok()
        .map(|()| (size.width, size.height));

    StateUpdateWire {
        id,
        state: map_state(core::uuav_player_state(id)),
        media_time,
        duration,
        controls,
        video_size,
        looping: core::uuav_player_get_looping(id) != 0,
        rate: core::uuav_player_get_rate(id),
    }
}

/// `None` until the core has media info for the player.
pub fn media_info(id: PlayerId) -> Option<MediaInfoWire> {
    // plain out-param storage; fully overwritten by the getter on success,
    // discarded on failure (MediaInfo is a POD of numbers and name buffers)
    let mut info = unsafe { std::mem::zeroed::<core::MediaInfo>() };
    consume_result(unsafe { core::uuav_player_get_media_info(id, &mut info) }).ok()?;
    Some(MediaInfoWire {
        duration: info.duration,
        framerate: info.framerate,
        video_bitrate: info.video_bitrate,
        audio_bitrate: info.audio_bitrate,
        width: info.width,
        height: info.height,
        sample_rate: info.sample_rate,
        channels: info.channels,
        video_codec: name_field(&info.video_codec),
        pixel_format: name_field(&info.pixel_format),
        audio_codec: name_field(&info.audio_codec),
        sample_format: name_field(&info.sample_format),
        has_video: info.has_video != 0,
        has_audio: info.has_audio != 0,
    })
}

const fn map_state(state: core::UUAVState) -> PlayerStateWire {
    match state {
        core::UUAVState::UUAV_CLOSED => PlayerStateWire::Closed,
        core::UUAVState::UUAV_OPENING => PlayerStateWire::Opening,
        core::UUAVState::UUAV_READY => PlayerStateWire::Ready,
        core::UUAVState::UUAV_PLAYING => PlayerStateWire::Playing,
        core::UUAVState::UUAV_PAUSED => PlayerStateWire::Paused,
        core::UUAVState::UUAV_ENDED => PlayerStateWire::Ended,
        core::UUAVState::UUAV_ERROR => PlayerStateWire::Error,
        core::UUAVState::UUAV_UNKNOWN => PlayerStateWire::Unknown,
    }
}

/// The core guarantees NUL-terminated UTF-8 inside the fixed name buffers.
fn name_field(field: &[c_char; core::MEDIA_INFO_NAME_LEN]) -> String {
    unsafe { CStr::from_ptr(field.as_ptr()) }
        .to_string_lossy()
        .into_owned()
}
