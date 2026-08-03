
#![no_std]
#![warn(clippy::all, clippy::pedantic, clippy::nursery)]
#![deny(
    clippy::unwrap_used,
    clippy::expect_used,
    clippy::panic,
    clippy::indexing_slicing,
    clippy::arithmetic_side_effects,
    clippy::todo,
    clippy::dbg_macro
)]
#![allow(
    clippy::uninlined_format_args,
    clippy::missing_errors_doc,
    clippy::option_if_let_else,
    clippy::single_match_else,
    clippy::must_use_candidate,
    clippy::future_not_send,
    clippy::enum_glob_use
)]
#![allow(
    clippy::borrow_as_ptr,
    clippy::redundant_pub_crate,
    clippy::cast_precision_loss,
    clippy::cast_sign_loss,
    clippy::cast_possible_truncation,
    clippy::cast_ptr_alignment,
    clippy::missing_safety_doc,
    clippy::doc_markdown
)]

mod guard;
mod layout;

use core::ffi::{CStr, c_char};

pub const ABI_VERSION: &CStr = c"0.2.0";

pub const FRAME_INFO_DECL_SHA256: &str =
    "75cb6fbb5f240967f06bd645da00dbbf678b8fe9e72a3087c48e3bf17d7e0ca1";

pub const MEDIA_INFO_NAME_LEN: usize = 32;

pub type PlayerId = u64;

pub type UUAVRenderEvent = extern "C" fn(event_id: i32);

pub type RawLogCallback = extern "C" fn(*const c_char);

#[allow(non_camel_case_types)]
#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum UUAVState {
    UUAV_CLOSED = 0,
    UUAV_OPENING = 1,
    UUAV_READY = 2,
    UUAV_PLAYING = 3,
    UUAV_PAUSED = 4,
    UUAV_ENDED = 5,
    UUAV_ERROR = 6,
    UUAV_UNKNOWN = 7,
}

impl UUAVState {
    pub const fn from_code(code: u32) -> Self {
        match code {
            0 => Self::UUAV_CLOSED,
            1 => Self::UUAV_OPENING,
            2 => Self::UUAV_READY,
            3 => Self::UUAV_PLAYING,
            4 => Self::UUAV_PAUSED,
            5 => Self::UUAV_ENDED,
            6 => Self::UUAV_ERROR,
            _ => Self::UUAV_UNKNOWN,
        }
    }
}

#[repr(C)]
#[derive(Default)]
pub struct Status {
    pub players_count: u64,
    pub initialized: bool,
    pub audio_options: AudioOptionsRaw,
    pub device_remove_reason: *const c_char,
}

#[repr(C)]
#[derive(Default, Clone, Copy, PartialEq, Eq, Debug)]
pub struct AudioOptionsRaw {
    pub sample_rate: i32,
    pub channels: i32,
}

#[repr(C)]
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub struct VideoSize {
    pub width: u32,
    pub height: u32,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct MediaInfo {
    pub duration: f64,
    pub framerate: f64,
    pub video_bitrate: i64,
    pub audio_bitrate: i64,
    pub width: u32,
    pub height: u32,
    pub sample_rate: i32,
    pub channels: i32,
    pub video_codec: [c_char; MEDIA_INFO_NAME_LEN],
    pub pixel_format: [c_char; MEDIA_INFO_NAME_LEN],
    pub audio_codec: [c_char; MEDIA_INFO_NAME_LEN],
    pub sample_format: [c_char; MEDIA_INFO_NAME_LEN],
    pub has_video: u8,
    pub has_audio: u8,
}

impl MediaInfo {
    pub const fn empty() -> Self {
        Self {
            duration: -1.0,
            framerate: 0.0,
            video_bitrate: 0,
            audio_bitrate: 0,
            width: 0,
            height: 0,
            sample_rate: 0,
            channels: 0,
            video_codec: [0; MEDIA_INFO_NAME_LEN],
            pixel_format: [0; MEDIA_INFO_NAME_LEN],
            audio_codec: [0; MEDIA_INFO_NAME_LEN],
            sample_format: [0; MEDIA_INFO_NAME_LEN],
            has_video: 0,
            has_audio: 0,
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct ControlsState {
    pub rate: f64,
    pub play: u8,
    pub play_pending: u8,
    pub looping: u8,
    pub looping_pending: u8,
    pub rate_pending: u8,
}

#[repr(C)]
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct FrameInfo {
    pub yuv_to_rgb: [f32; 12],
    pub uv_transform: [f32; 6],
    pub visible_width: u32,
    pub visible_height: u32,
    pub plane_width: [u32; 2],
    pub plane_height: [u32; 2],
    pub colorspace: i32,
    pub color_range: i32,
    pub color_primaries: i32,
    pub rotation: i32,
    pub bit_depth: u32,
    pub frame_index: u64,
    pub surface_generation: u64,
    pub planes: [usize; 2],
}

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct AudioSyncFFI {
    pub media_time: f64,
    pub base_pts: f64,
    pub rate: f64,
    pub generation: u64,
    pub frames_consumed: u64,
    pub base_frames: u64,
    pub silence_calls: u64,
    pub sample_rate: u32,
    pub priming: u32,
    pub has_basis: u32,
    pub reserved: u32,
}

#[repr(C)]
pub struct NewPlayerResult {
    pub player_id: PlayerId,
    pub error_message: *const c_char,
}

impl NewPlayerResult {
    pub const fn ok(player_id: PlayerId) -> Self {
        Self {
            player_id,
            error_message: core::ptr::null(),
        }
    }
}

#[repr(C)]
pub struct ResultFFI {
    pub error_message: *const c_char,
}

impl ResultFFI {
    pub const fn ok() -> Self {
        Self {
            error_message: core::ptr::null(),
        }
    }
}

pub mod errors {
    pub const NO_RUNTIME: &str = "Runtime is not found";
    pub const NO_PLAYER: &str = "player with specific id not found";
    pub const OUT_POINTER_NULL: &str = "out pointer is null";
    pub const ALREADY_INITIALIZED: &str = "Already initialized";
    pub const NOT_INITIALIZED: &str = "Not initialized";
    pub const ERROR_CALLBACK_NULL: &str = "Error callback is null";
    pub const WARNING_CALLBACK_NULL: &str = "Warning callback is null";
    pub const LOG_CALLBACK_NULL: &str = "Log callback is null";
    pub const NO_TEXTURE: &str = "Texture to capture the HwDevice from is not provided";
    pub const TEXTURE_NOT_COM: &str = "texture is not a COM pointer";
    pub const TEXTURE_NOT_D3D11: &str =
        "texture is not an ID3D11Texture2D (is the engine running D3D11?)";
    pub const URL_NULL: &str = "url is null";
    pub const URL_NOT_UTF8: &str = "url is not valid UTF-8";
    pub const PROTOCOL_WHITELIST_NULL: &str = "protocol_whitelist is null";
    pub const PROTOCOL_WHITELIST_EMPTY: &str = "protocol_whitelist is empty";
}
