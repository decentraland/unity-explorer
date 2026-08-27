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
// doc_markdown: insists on backticks around the word "FFmpeg" in prose;
// borrow_as_ptr: OS out-params are pervasive in the channel FFI — same
// allowances as the core and server crates
#![allow(
    clippy::missing_errors_doc,
    clippy::must_use_candidate,
    clippy::doc_markdown,
    clippy::borrow_as_ptr
)]

pub mod channel;
pub mod protocol;

#[cfg(target_os = "macos")]
pub mod mach_channel;
