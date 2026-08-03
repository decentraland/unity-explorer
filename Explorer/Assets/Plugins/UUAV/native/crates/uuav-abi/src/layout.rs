
use core::mem::offset_of;

use crate::{
    AudioOptionsRaw, ControlsState, FrameInfo, MediaInfo, NewPlayerResult, ResultFFI, Status,
    UUAVState, VideoSize,
};

macro_rules! golden {
    (
        $ty:ty, size = $size:literal, align = $align:literal
        $(, $field:ident = $offset:literal)* $(,)?
    ) => {
        const _: () = assert!(size_of::<$ty>() == $size);
        const _: () = assert!(align_of::<$ty>() == $align);
        $(const _: () = assert!(offset_of!($ty, $field) == $offset);)*
    };
}

const _: () = assert!(size_of::<usize>() == 8);

golden!(
    FrameInfo,
    size = 152,
    align = 8,
    yuv_to_rgb = 0,
    uv_transform = 48,
    visible_width = 72,
    visible_height = 76,
    plane_width = 80,
    plane_height = 88,
    colorspace = 96,
    color_range = 100,
    color_primaries = 104,
    rotation = 108,
    bit_depth = 112,
    frame_index = 120,
    surface_generation = 128,
    planes = 136,
);

golden!(
    MediaInfo,
    size = 184,
    align = 8,
    duration = 0,
    framerate = 8,
    video_bitrate = 16,
    audio_bitrate = 24,
    width = 32,
    height = 36,
    sample_rate = 40,
    channels = 44,
    video_codec = 48,
    pixel_format = 80,
    audio_codec = 112,
    sample_format = 144,
    has_video = 176,
    has_audio = 177,
);

golden!(
    Status,
    size = 32,
    align = 8,
    players_count = 0,
    initialized = 8,
    audio_options = 12,
    device_remove_reason = 24,
);

golden!(
    ControlsState,
    size = 16,
    align = 8,
    rate = 0,
    play = 8,
    play_pending = 9,
    looping = 10,
    looping_pending = 11,
    rate_pending = 12,
);

golden!(
    NewPlayerResult,
    size = 16,
    align = 8,
    player_id = 0,
    error_message = 8,
);

golden!(ResultFFI, size = 8, align = 8, error_message = 0);

golden!(
    AudioOptionsRaw,
    size = 8,
    align = 4,
    sample_rate = 0,
    channels = 4,
);

golden!(VideoSize, size = 8, align = 4, width = 0, height = 4);

golden!(UUAVState, size = 4, align = 4);

const _: () = assert!(UUAVState::UUAV_CLOSED as u32 == 0);
const _: () = assert!(UUAVState::UUAV_OPENING as u32 == 1);
const _: () = assert!(UUAVState::UUAV_READY as u32 == 2);
const _: () = assert!(UUAVState::UUAV_PLAYING as u32 == 3);
const _: () = assert!(UUAVState::UUAV_PAUSED as u32 == 4);
const _: () = assert!(UUAVState::UUAV_ENDED as u32 == 5);
const _: () = assert!(UUAVState::UUAV_ERROR as u32 == 6);
const _: () = assert!(UUAVState::UUAV_UNKNOWN as u32 == 7);
