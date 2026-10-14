//! The manifest: what a launcher needs to move from any listed base to the target release.

use anyhow::{Result, bail, ensure};
use serde::{Deserialize, Serialize};

use crate::builds::tree::{Node, Tree};
use crate::patching::delta::Delta;
use crate::publishing::values::ZipRef;
use crate::releases::bases::Bases;
use crate::releases::tag::{Platform, Tag};

/// `Manifest::new` refuses a target listed as its own base, a deleted path still in `files`, and
/// a rebuilt path that is not a regular file of `files`.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Manifest {
    target: Tag,
    platform: Platform,
    zip: ZipRef,
    files: Tree,
    deltas: Bases<Delta>,
}

impl Manifest {
    pub fn new(target: Tag, platform: Platform, zip: ZipRef, files: Tree, deltas: Bases<Delta>) -> Result<Manifest> {
        ensure!(!deltas.contains(&target), "{target} cannot be its own base");
        for (base, delta) in &deltas {
            for path in delta.deleted() {
                ensure!(
                    files.get(path).is_none(),
                    "{base}: {path} is deleted but listed in files"
                );
            }
            for path in delta.rebuilt() {
                if !matches!(files.get(path), Some(Node::File { .. })) {
                    bail!("{base}: {path} is rebuilt but is not a file of the target build");
                }
            }
        }
        Ok(Manifest {
            target,
            platform,
            zip,
            files,
            deltas,
        })
    }

    pub fn target(&self) -> &Tag {
        &self.target
    }

    pub fn platform(&self) -> Platform {
        self.platform
    }

    pub fn zip(&self) -> &ZipRef {
        &self.zip
    }

    pub fn files(&self) -> &Tree {
        &self.files
    }

    pub fn deltas(&self) -> &Bases<Delta> {
        &self.deltas
    }
}
