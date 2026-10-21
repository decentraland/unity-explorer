//! One platform's delta run as an Elm-style machine. `create-run` writes `init(request)`: the
//! `Requested` state and the first cmd. `run` drains: execute the pending cmd, feed the fact it
//! produced to `update`, get the next state with its cmds, save, repeat, until the machine asks
//! to stop with `Cmd::Complete` or the state named by `--until` is reached. The lifecycle is
//! on `state::State`.
//!
//! `fsm` is pure: no file system, processes or environment. `execute` is the only impure part
//! and never sees the state. `runtime` is the loop between them.

pub mod document;
pub mod execute;
pub mod fsm;
pub mod runtime;
pub mod state;
pub mod summary;
