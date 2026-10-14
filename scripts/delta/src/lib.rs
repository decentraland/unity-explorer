//! Per-file `HDiffPatch` delta updates for Explorer releases. Design: `docs/delta-updates.md`.
//!
//! Bounded contexts: `releases`, `builds`, `patching`, `publishing`. The `run` workflows move a
//! platform's `Run` document through its stages; `capabilities` are the I/O they depend on and
//! `shell` implements them with processes and folders.

pub mod builds;
pub mod capabilities;
pub mod patching;
pub mod publishing;
pub mod releases;
pub mod run;
pub mod shell;
