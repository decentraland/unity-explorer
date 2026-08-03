
use std::ffi::c_char;

use uuav_abi::{AudioOptionsRaw, Status, UUAVState};

#[repr(C)]
#[allow(dead_code)]
struct CoreStatus {
    players_count: u64,
    initialized: bool,
    audio_options: AudioOptionsRaw,
    device_remove_reason: *const c_char,
}

uuav_abi::assert_same_layout! {
    abi: Status,
    core: CoreStatus,
    fields {
        players_count: u64,
        initialized: bool,
        audio_options: AudioOptionsRaw,
        device_remove_reason: *const c_char,
    }
}

#[repr(C)]
#[allow(dead_code, non_camel_case_types)]
enum CoreState {
    UUAV_CLOSED = 0,
    UUAV_OPENING = 1,
    UUAV_READY = 2,
    UUAV_PLAYING = 3,
    UUAV_PAUSED = 4,
    UUAV_ENDED = 5,
    UUAV_ERROR = 6,
    UUAV_UNKNOWN = 7,
}

uuav_abi::assert_enum_discriminants! {
    abi: UUAVState,
    core: CoreState,
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

#[test]
fn guards_expand_and_hold() {
    assert_eq!(size_of::<Status>(), size_of::<CoreStatus>());
    assert_eq!(size_of::<UUAVState>(), size_of::<CoreState>());
}
