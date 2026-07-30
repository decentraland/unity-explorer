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
// doc_markdown: insists on backticks around the word "FFmpeg" in prose,
// same allowance as the core crate
#![allow(
    clippy::missing_errors_doc,
    clippy::must_use_candidate,
    clippy::doc_markdown
)]

pub mod protocol;
pub mod socket;

// single place that pins the zmq version/features for every crate
pub use zmq;
