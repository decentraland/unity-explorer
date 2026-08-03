
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

pub mod protocol;

pub mod present_core;

pub mod audio;

pub mod controls;

pub mod fetch;

#[cfg(target_os = "macos")]
pub mod mach_ipc;

pub mod win;

#[cfg(target_os = "macos")]
pub mod session;
#[cfg(target_os = "macos")]
pub mod shm;
#[cfg(target_os = "macos")]
pub mod spawn;

#[cfg(target_os = "macos")]
pub mod registry;

pub use uuav_abi::{
    AudioOptionsRaw, ControlsState, FrameInfo, MEDIA_INFO_NAME_LEN, MediaInfo, NewPlayerResult,
    PlayerId, RawLogCallback, ResultFFI, Status, UUAVRenderEvent, UUAVState, VideoSize,
};

#[cfg(target_os = "macos")]
pub mod sandbox;

#[cfg(target_os = "macos")]
pub mod host;
