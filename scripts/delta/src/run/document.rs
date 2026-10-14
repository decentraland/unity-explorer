//! Reading and writing the `Run` document.

use std::fs;
use std::path::{Path, PathBuf};

use anyhow::{Context, Result, ensure};

use crate::run::stage::{Run, SCHEMA};

pub fn load(path: &Path) -> Result<Run> {
    let text: String = fs::read_to_string(path).with_context(|| format!("cannot read {}", path.display()))?;
    let run: Run = serde_json::from_str(&text).with_context(|| format!("invalid run document {}", path.display()))?;
    ensure!(
        run.schema == SCHEMA,
        "run document schema {} is not {SCHEMA}",
        run.schema
    );
    Ok(run)
}

/// Writes through a temporary file and a rename, so the document is never half written.
pub fn save(path: &Path, run: &Run) -> Result<()> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).with_context(|| format!("cannot create {}", parent.display()))?;
    }
    let text: String = serde_json::to_string_pretty(run)?;
    let temporary: PathBuf = path.with_extension("json.tmp");
    fs::write(&temporary, text).with_context(|| format!("cannot write {}", temporary.display()))?;
    fs::rename(&temporary, path).with_context(|| format!("cannot replace {}", path.display()))
}
