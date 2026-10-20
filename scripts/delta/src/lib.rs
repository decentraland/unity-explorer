//! Per-file `HDiffPatch` delta updates for Explorer releases. Design: `docs/delta-updates.md`.
//!
//! Bounded contexts: `releases`, `builds`, `patching`, `publishing`. `run` is the state machine
//! of one platform's run: a pure `update` over the `Run` document, and `execute` for the I/O it
//! asks for through `io`, implemented in `shell`.

pub mod builds;
pub mod io;
pub mod patching;
pub mod publishing;
pub mod releases;
pub mod run;
pub mod shell;
