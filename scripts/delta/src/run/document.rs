//! Reading and writing the run document: the model and the cmds it waits on, as a pair.

use std::fs;
use std::path::{Path, PathBuf};

use anyhow::{Context, Result, ensure};
use serde::{Deserialize, Serialize};

use crate::run::fsm::Cmd;
use crate::run::state::Run;

pub const SCHEMA: u32 = 3;

#[derive(Debug, Serialize, Deserialize)]
struct Document {
    schema: u32,
    run: Run,
    cmds: Vec<Cmd>,
}

pub fn load(path: &Path) -> Result<(Run, Vec<Cmd>)> {
    let text: String = fs::read_to_string(path).with_context(|| format!("cannot read {}", path.display()))?;
    let document: Document =
        serde_json::from_str(&text).with_context(|| format!("invalid run document {}", path.display()))?;
    ensure!(
        document.schema == SCHEMA,
        "run document schema {} is not {SCHEMA}",
        document.schema
    );
    Ok((document.run, document.cmds))
}

/// Writes through a temporary file and a rename, so the document is never half written.
pub fn save(path: &Path, run: &Run, cmds: &[Cmd]) -> Result<()> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).with_context(|| format!("cannot create {}", parent.display()))?;
    }
    let document: Document = Document {
        schema: SCHEMA,
        run: run.clone(),
        cmds: cmds.to_vec(),
    };
    let text: String = serde_json::to_string_pretty(&document)?;
    let temporary: PathBuf = path.with_extension("json.tmp");
    fs::write(&temporary, text).with_context(|| format!("cannot write {}", temporary.display()))?;
    fs::rename(&temporary, path).with_context(|| format!("cannot replace {}", path.display()))
}
