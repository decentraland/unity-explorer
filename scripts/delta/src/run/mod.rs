//! One platform's delta run. Each YAML step runs `delta step <step> run.json`: the `Run`
//! document is read, `update` turns the step into the one `Command` it needs (or fails the run
//! when the step does not fit the state), `execute` runs that command and reports, `update` folds
//! the report into the next state, and the document is written back.
//!
//! ```text
//! Idle -> Unpublished -> Cleared -> Downloaded -> Extracted -> Scanned
//!      -> Encoded -> Verified -> Assembled -> Rechecked -> PatchesUploaded -> Published
//! any  -> Failed (run-level error, step or report out of order, or every base dropped)
//! ```
//!
//! `fsm` is pure: no file system, processes or environment. `execute` is the only impure part
//! and never sees the state.

pub mod document;
pub mod execute;
pub mod fsm;
pub mod state;
pub mod summary;
