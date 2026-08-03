
use std::mem::{align_of, offset_of, size_of};

use uuav_abi::{
    AudioOptionsRaw, ControlsState, FrameInfo, MediaInfo, NewPlayerResult, ResultFFI, Status,
    UUAVState, VideoSize,
};

fn report<const N: usize>(label: &str, size: usize, align: usize, fields: [(&str, usize); N]) {
    println!("{label}: size={size} align={align}");
    for (name, offset) in fields {
        println!("  {offset:>4}  {name}");
    }
}

#[test]
fn pointers_are_64_bit() {
    assert_eq!(size_of::<usize>(), 8, "the ABI is 64-bit only");
}

#[test]
fn frame_info_is_golden() {
    let fields = [
        ("yuv_to_rgb", offset_of!(FrameInfo, yuv_to_rgb)),
        ("uv_transform", offset_of!(FrameInfo, uv_transform)),
        ("visible_width", offset_of!(FrameInfo, visible_width)),
        ("visible_height", offset_of!(FrameInfo, visible_height)),
        ("plane_width", offset_of!(FrameInfo, plane_width)),
        ("plane_height", offset_of!(FrameInfo, plane_height)),
        ("colorspace", offset_of!(FrameInfo, colorspace)),
        ("color_range", offset_of!(FrameInfo, color_range)),
        ("color_primaries", offset_of!(FrameInfo, color_primaries)),
        ("rotation", offset_of!(FrameInfo, rotation)),
        ("bit_depth", offset_of!(FrameInfo, bit_depth)),
        ("frame_index", offset_of!(FrameInfo, frame_index)),
        ("surface_generation", offset_of!(FrameInfo, surface_generation)),
        ("planes", offset_of!(FrameInfo, planes)),
    ];
    report(
        "FrameInfo",
        size_of::<FrameInfo>(),
        align_of::<FrameInfo>(),
        fields,
    );

    assert_eq!(
        fields,
        [
            ("yuv_to_rgb", 0),
            ("uv_transform", 48),
            ("visible_width", 72),
            ("visible_height", 76),
            ("plane_width", 80),
            ("plane_height", 88),
            ("colorspace", 96),
            ("color_range", 100),
            ("color_primaries", 104),
            ("rotation", 108),
            ("bit_depth", 112),
            ("frame_index", 120),
            ("surface_generation", 128),
            ("planes", 136),
        ]
    );
    assert_eq!((size_of::<FrameInfo>(), align_of::<FrameInfo>()), (152, 8));
}

#[test]
fn media_info_is_golden() {
    let fields = [
        ("duration", offset_of!(MediaInfo, duration)),
        ("framerate", offset_of!(MediaInfo, framerate)),
        ("video_bitrate", offset_of!(MediaInfo, video_bitrate)),
        ("audio_bitrate", offset_of!(MediaInfo, audio_bitrate)),
        ("width", offset_of!(MediaInfo, width)),
        ("height", offset_of!(MediaInfo, height)),
        ("sample_rate", offset_of!(MediaInfo, sample_rate)),
        ("channels", offset_of!(MediaInfo, channels)),
        ("video_codec", offset_of!(MediaInfo, video_codec)),
        ("pixel_format", offset_of!(MediaInfo, pixel_format)),
        ("audio_codec", offset_of!(MediaInfo, audio_codec)),
        ("sample_format", offset_of!(MediaInfo, sample_format)),
        ("has_video", offset_of!(MediaInfo, has_video)),
        ("has_audio", offset_of!(MediaInfo, has_audio)),
    ];
    report(
        "MediaInfo",
        size_of::<MediaInfo>(),
        align_of::<MediaInfo>(),
        fields,
    );

    assert_eq!(
        fields,
        [
            ("duration", 0),
            ("framerate", 8),
            ("video_bitrate", 16),
            ("audio_bitrate", 24),
            ("width", 32),
            ("height", 36),
            ("sample_rate", 40),
            ("channels", 44),
            ("video_codec", 48),
            ("pixel_format", 80),
            ("audio_codec", 112),
            ("sample_format", 144),
            ("has_video", 176),
            ("has_audio", 177),
        ]
    );
    assert_eq!((size_of::<MediaInfo>(), align_of::<MediaInfo>()), (184, 8));
}

#[test]
fn status_is_golden() {
    let fields = [
        ("players_count", offset_of!(Status, players_count)),
        ("initialized", offset_of!(Status, initialized)),
        ("audio_options", offset_of!(Status, audio_options)),
        (
            "device_remove_reason",
            offset_of!(Status, device_remove_reason),
        ),
    ];
    report("Status", size_of::<Status>(), align_of::<Status>(), fields);

    assert_eq!(
        fields,
        [
            ("players_count", 0),
            ("initialized", 8),
            ("audio_options", 12),
            ("device_remove_reason", 24),
        ]
    );
    assert_eq!((size_of::<Status>(), align_of::<Status>()), (32, 8));
}

#[test]
fn controls_state_is_golden() {
    let fields = [
        ("rate", offset_of!(ControlsState, rate)),
        ("play", offset_of!(ControlsState, play)),
        ("play_pending", offset_of!(ControlsState, play_pending)),
        ("looping", offset_of!(ControlsState, looping)),
        ("looping_pending", offset_of!(ControlsState, looping_pending)),
        ("rate_pending", offset_of!(ControlsState, rate_pending)),
    ];
    report(
        "ControlsState",
        size_of::<ControlsState>(),
        align_of::<ControlsState>(),
        fields,
    );

    assert_eq!(
        fields,
        [
            ("rate", 0),
            ("play", 8),
            ("play_pending", 9),
            ("looping", 10),
            ("looping_pending", 11),
            ("rate_pending", 12),
        ]
    );
    assert_eq!(
        (size_of::<ControlsState>(), align_of::<ControlsState>()),
        (16, 8)
    );
}

#[test]
fn result_types_are_golden() {
    let new_player = [
        ("player_id", offset_of!(NewPlayerResult, player_id)),
        ("error_message", offset_of!(NewPlayerResult, error_message)),
    ];
    report(
        "NewPlayerResult",
        size_of::<NewPlayerResult>(),
        align_of::<NewPlayerResult>(),
        new_player,
    );
    assert_eq!(new_player, [("player_id", 0), ("error_message", 8)]);
    assert_eq!(
        (size_of::<NewPlayerResult>(), align_of::<NewPlayerResult>()),
        (16, 8)
    );

    assert_eq!(offset_of!(ResultFFI, error_message), 0);
    assert_eq!((size_of::<ResultFFI>(), align_of::<ResultFFI>()), (8, 8));
}

#[test]
fn small_value_types_are_golden() {
    let audio = [
        ("sample_rate", offset_of!(AudioOptionsRaw, sample_rate)),
        ("channels", offset_of!(AudioOptionsRaw, channels)),
    ];
    report(
        "AudioOptionsRaw",
        size_of::<AudioOptionsRaw>(),
        align_of::<AudioOptionsRaw>(),
        audio,
    );
    assert_eq!(audio, [("sample_rate", 0), ("channels", 4)]);
    assert_eq!(
        (size_of::<AudioOptionsRaw>(), align_of::<AudioOptionsRaw>()),
        (8, 4)
    );

    let size = [
        ("width", offset_of!(VideoSize, width)),
        ("height", offset_of!(VideoSize, height)),
    ];
    report(
        "VideoSize",
        size_of::<VideoSize>(),
        align_of::<VideoSize>(),
        size,
    );
    assert_eq!(size, [("width", 0), ("height", 4)]);
    assert_eq!((size_of::<VideoSize>(), align_of::<VideoSize>()), (8, 4));
}

#[test]
fn state_discriminants_are_golden() {
    let states = [
        ("UUAV_CLOSED", UUAVState::UUAV_CLOSED as u32),
        ("UUAV_OPENING", UUAVState::UUAV_OPENING as u32),
        ("UUAV_READY", UUAVState::UUAV_READY as u32),
        ("UUAV_PLAYING", UUAVState::UUAV_PLAYING as u32),
        ("UUAV_PAUSED", UUAVState::UUAV_PAUSED as u32),
        ("UUAV_ENDED", UUAVState::UUAV_ENDED as u32),
        ("UUAV_ERROR", UUAVState::UUAV_ERROR as u32),
        ("UUAV_UNKNOWN", UUAVState::UUAV_UNKNOWN as u32),
    ];
    println!("UUAVState: size={} align={}", size_of::<UUAVState>(), align_of::<UUAVState>());
    for (name, code) in states {
        println!("  {code}  {name}");
    }

    assert_eq!(
        states,
        [
            ("UUAV_CLOSED", 0),
            ("UUAV_OPENING", 1),
            ("UUAV_READY", 2),
            ("UUAV_PLAYING", 3),
            ("UUAV_PAUSED", 4),
            ("UUAV_ENDED", 5),
            ("UUAV_ERROR", 6),
            ("UUAV_UNKNOWN", 7),
        ]
    );
    assert_eq!((size_of::<UUAVState>(), align_of::<UUAVState>()), (4, 4));

    for (_, code) in states {
        assert_eq!(UUAVState::from_code(code) as u32, code);
    }
    assert_eq!(UUAVState::from_code(8), UUAVState::UUAV_UNKNOWN);
    assert_eq!(UUAVState::from_code(u32::MAX), UUAVState::UUAV_UNKNOWN);
}

#[test]
fn empty_media_info_is_the_unknown_contract() {
    let info = MediaInfo::empty();
    assert_eq!(info.duration, -1.0);
    assert_eq!(info.has_video, 0);
    assert_eq!(info.has_audio, 0);
    assert_eq!(info.video_codec, [0; 32]);
    assert_eq!(info.sample_format, [0; 32]);
}

#[test]
fn ok_results_carry_no_message() {
    assert!(ResultFFI::ok().error_message.is_null());
    let new_player = NewPlayerResult::ok(7);
    assert_eq!(new_player.player_id, 7);
    assert!(new_player.error_message.is_null());
}
