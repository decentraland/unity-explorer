
#![allow(dead_code)]
#![allow(clippy::type_complexity)]

use core::ffi::{c_char, c_int, c_void};

include!(concat!(env!("OUT_DIR"), "/guard.rs"));

const _: () = assert!(str_eq(
    env!("UUAV_FRAME_INFO_DECL_SHA256"),
    uuav_abi::FRAME_INFO_DECL_SHA256
));

#[allow(clippy::indexing_slicing, clippy::arithmetic_side_effects)]
const fn str_eq(left: &str, right: &str) -> bool {
    let (left, right) = (left.as_bytes(), right.as_bytes());
    if left.len() != right.len() {
        return false;
    }
    let mut index = 0;
    while index < left.len() {
        if left[index] != right[index] {
            return false;
        }
        index += 1;
    }
    true
}

mod layout {
    use core::ffi::c_char;

    const _: () = assert!(uuav::MEDIA_INFO_NAME_LEN == uuav_abi::MEDIA_INFO_NAME_LEN);

    const fn pin_player_id(value: uuav::PlayerId) -> uuav_abi::PlayerId {
        value
    }

    const fn pin_render_event(value: uuav::UUAVRenderEvent) -> uuav_abi::UUAVRenderEvent {
        value
    }

    uuav_abi::assert_same_layout! {
        abi: uuav_abi::Status,
        core: uuav::Status,
        fields {
            players_count: u64,
            initialized: bool,
            audio_options: uuav::AudioOptionsRaw,
            device_remove_reason: *const c_char,
        }
    }

    uuav_abi::assert_same_layout! {
        abi: uuav_abi::AudioOptionsRaw,
        core: uuav::AudioOptionsRaw,
        fields {
            sample_rate: i32,
            channels: i32,
        }
    }

    uuav_abi::assert_same_layout! {
        abi: uuav_abi::VideoSize,
        core: uuav::VideoSize,
        fields {
            width: u32,
            height: u32,
        }
    }

    uuav_abi::assert_same_layout! {
        abi: uuav_abi::MediaInfo,
        core: uuav::MediaInfo,
        fields {
            duration: f64,
            framerate: f64,
            video_bitrate: i64,
            audio_bitrate: i64,
            width: u32,
            height: u32,
            sample_rate: i32,
            channels: i32,
            video_codec: [c_char; uuav_abi::MEDIA_INFO_NAME_LEN],
            pixel_format: [c_char; uuav_abi::MEDIA_INFO_NAME_LEN],
            audio_codec: [c_char; uuav_abi::MEDIA_INFO_NAME_LEN],
            sample_format: [c_char; uuav_abi::MEDIA_INFO_NAME_LEN],
            has_video: u8,
            has_audio: u8,
        }
    }

    uuav_abi::assert_same_layout! {
        abi: uuav_abi::ControlsState,
        core: uuav::ControlsState,
        fields {
            rate: f64,
            play: u8,
            play_pending: u8,
            looping: u8,
            looping_pending: u8,
            rate_pending: u8,
        }
    }

    uuav_abi::assert_same_layout! {
        abi: uuav_abi::NewPlayerResult,
        core: uuav::NewPlayerResult,
        fields {
            player_id: uuav_abi::PlayerId,
            error_message: *const c_char,
        }
    }

    uuav_abi::assert_same_layout! {
        abi: uuav_abi::ResultFFI,
        core: uuav::ResultFFI,
        fields {
            error_message: *const c_char,
        }
    }

    uuav_abi::assert_enum_discriminants! {
        abi: uuav_abi::UUAVState,
        core: uuav::UUAVState,
        variants {
            UUAV_CLOSED,
            UUAV_OPENING,
            UUAV_READY,
            UUAV_PLAYING,
            UUAV_PAUSED,
            UUAV_ENDED,
            UUAV_ERROR,
            UUAV_UNKNOWN,
        }
    }
}

mod frame_info {
    use core::mem::{align_of, size_of};

    const fn out_param_size<T>(
        _: unsafe extern "C" fn(uuav_abi::PlayerId, *mut T) -> uuav::ResultFFI,
    ) -> usize {
        size_of::<T>()
    }

    const fn out_param_align<T>(
        _: unsafe extern "C" fn(uuav_abi::PlayerId, *mut T) -> uuav::ResultFFI,
    ) -> usize {
        align_of::<T>()
    }

    const _: () = assert!(
        out_param_size(uuav::uuav_player_get_frame_info) == size_of::<uuav_abi::FrameInfo>()
    );
    const _: () = assert!(
        out_param_align(uuav::uuav_player_get_frame_info) == align_of::<uuav_abi::FrameInfo>()
    );

    const _: () = assert!(
        out_param_size(uuav::uuav_player_get_media_info) == size_of::<uuav_abi::MediaInfo>()
    );
    const _: () = assert!(
        out_param_size(uuav::uuav_player_current_controls_state)
            == size_of::<uuav_abi::ControlsState>()
    );
    const _: () = assert!(
        out_param_size(uuav::uuav_player_get_video_size) == size_of::<uuav_abi::VideoSize>()
    );
}

mod signatures {
    use super::{c_char, c_int, c_void};

    const UUAV_ABI_VERSION: extern "C" fn() -> *const c_char = uuav::uuav_abi_version;
    const UUAV_STRING_FREE: unsafe extern "C" fn(*mut c_char) = uuav::uuav_string_free;
    const UUAV_SET_LOG_LEVEL: extern "C" fn(c_int) = uuav::uuav_set_log_level;

    const UUAV_INIT: unsafe extern "C" fn(
        *const c_void,
        uuav::AudioOptionsRaw,
        Option<uuav_abi::RawLogCallback>,
        Option<uuav_abi::RawLogCallback>,
        Option<uuav_abi::RawLogCallback>,
        *const c_char,
        c_int,
    ) -> uuav::ResultFFI = uuav::uuav_init;

    const UUAV_DEINIT: extern "C" fn() = uuav::uuav_deinit;
    const UUAV_UPDATE_AUDIO_OUT: extern "C" fn(uuav::AudioOptionsRaw) -> uuav::ResultFFI =
        uuav::uuav_update_audio_out;
    const UUAV_STATUS: extern "C" fn() -> uuav::Status = uuav::uuav_status;

    const UUAV_PLAYER_NEW: extern "C" fn() -> uuav::NewPlayerResult = uuav::uuav_player_new;
    const UUAV_PLAYER_FREE: extern "C" fn(uuav_abi::PlayerId) = uuav::uuav_player_free;
    const UUAV_PLAYER_PLAY: extern "C" fn(uuav_abi::PlayerId) -> uuav::ResultFFI =
        uuav::uuav_player_play;
    const UUAV_PLAYER_PAUSE: extern "C" fn(uuav_abi::PlayerId) -> uuav::ResultFFI =
        uuav::uuav_player_pause;
    const UUAV_PLAYER_OPEN_MEDIA_ASYNC: unsafe extern "C" fn(
        uuav_abi::PlayerId,
        *const c_char,
    ) -> uuav::ResultFFI = uuav::uuav_player_open_media_async;
    const UUAV_PLAYER_CLOSE_MEDIA: extern "C" fn(uuav_abi::PlayerId) -> uuav::ResultFFI =
        uuav::uuav_player_close_media;
    const UUAV_PLAYER_STATE: extern "C" fn(uuav_abi::PlayerId) -> uuav::UUAVState =
        uuav::uuav_player_state;

    const UUAV_PLAYER_DURATION: unsafe extern "C" fn(
        uuav_abi::PlayerId,
        *mut f64,
    ) -> uuav::ResultFFI = uuav::uuav_player_duration;
    const UUAV_PLAYER_CURRENT_CONTROLS_STATE: unsafe extern "C" fn(
        uuav_abi::PlayerId,
        *mut uuav::ControlsState,
    ) -> uuav::ResultFFI = uuav::uuav_player_current_controls_state;
    const UUAV_PLAYER_CURRENT_TIME: unsafe extern "C" fn(
        uuav_abi::PlayerId,
        *mut f64,
    ) -> uuav::ResultFFI = uuav::uuav_player_current_time;
    const UUAV_PLAYER_GET_VIDEO_SIZE: unsafe extern "C" fn(
        uuav_abi::PlayerId,
        *mut uuav::VideoSize,
    ) -> uuav::ResultFFI = uuav::uuav_player_get_video_size;
    const UUAV_PLAYER_GET_MEDIA_INFO: unsafe extern "C" fn(
        uuav_abi::PlayerId,
        *mut uuav::MediaInfo,
    ) -> uuav::ResultFFI = uuav::uuav_player_get_media_info;


    const UUAV_PLAYER_ASSIGN_MASTER_CLOCK: extern "C" fn(
        uuav_abi::PlayerId,
        f64,
    ) -> uuav::ResultFFI = uuav::uuav_player_assign_master_clock;
    const UUAV_PLAYER_SEEK_ASYNC: extern "C" fn(uuav_abi::PlayerId, f64) -> uuav::ResultFFI =
        uuav::uuav_player_seek_async;
    const UUAV_PLAYER_SET_LOOPING: extern "C" fn(uuav_abi::PlayerId, u8) -> uuav::ResultFFI =
        uuav::uuav_player_set_looping;
    const UUAV_PLAYER_GET_LOOPING: extern "C" fn(uuav_abi::PlayerId) -> u8 =
        uuav::uuav_player_get_looping;
    const UUAV_PLAYER_SET_RATE: extern "C" fn(uuav_abi::PlayerId, f64) -> uuav::ResultFFI =
        uuav::uuav_player_set_rate;
    const UUAV_PLAYER_GET_RATE: extern "C" fn(uuav_abi::PlayerId) -> f64 =
        uuav::uuav_player_get_rate;

    const UUAV_GET_RENDER_CALLBACK: extern "C" fn() -> uuav_abi::UUAVRenderEvent =
        uuav::uuav_get_render_callback;
    const UUAV_PLAYER_GET_VIDEO_TEXTURE: unsafe extern "C" fn(
        uuav_abi::PlayerId,
        i32,
        *mut *const c_void,
    ) -> uuav::ResultFFI = uuav::uuav_player_get_video_texture;
    const UUAV_PLAYER_READ_AUDIO: unsafe extern "C" fn(uuav_abi::PlayerId, *mut f32, i32) -> i32 =
        uuav::uuav_player_read_audio;
}
