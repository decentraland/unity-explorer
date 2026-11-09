//! Patching: planning a patch set, encoding it with `HDiffPatch`, and replaying it like the launcher.

pub mod delta;
pub mod disk;
pub mod plan;
pub mod replay;
