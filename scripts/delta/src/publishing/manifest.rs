//! The manifest: what a launcher needs to move from any listed base to the release.

use anyhow::{Result, bail, ensure};
use serde::{Deserialize, Serialize};

use crate::builds::tree::{Node, Tree};
use crate::patching::delta::Delta;
use crate::publishing::values::ZipRef;
use crate::releases::bases::Bases;
use crate::releases::tag::{Platform, Tag};

/// `Manifest::new` refuses a release listed as its own base, a deleted path still in `files`, and
/// a rebuilt path that is not a regular file of `files`.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(try_from = "ManifestParts")]
pub struct Manifest {
    release: Tag,
    platform: Platform,
    zip: ZipRef,
    files: Tree,
    deltas: Bases<Delta>,
}

/// The serialized shape of a `Manifest`, before its rules are applied.
#[derive(Debug, Deserialize)]
struct ManifestParts {
    release: Tag,
    platform: Platform,
    zip: ZipRef,
    files: Tree,
    deltas: Bases<Delta>,
}

impl TryFrom<ManifestParts> for Manifest {
    type Error = anyhow::Error;

    fn try_from(parts: ManifestParts) -> Result<Manifest> {
        Manifest::new(parts.release, parts.platform, parts.zip, parts.files, parts.deltas)
    }
}

impl Manifest {
    pub fn new(release: Tag, platform: Platform, zip: ZipRef, files: Tree, deltas: Bases<Delta>) -> Result<Manifest> {
        ensure!(!deltas.contains(&release), "{release} cannot be its own base");
        for (base, delta) in &deltas {
            for path in delta.deleted() {
                ensure!(
                    files.get(path).is_none(),
                    "{base}: {path} is deleted but listed in files"
                );
            }
            for path in delta.rebuilt() {
                if !matches!(files.get(path), Some(Node::File { .. })) {
                    bail!("{base}: {path} is rebuilt but is not a file of the release build");
                }
            }
        }
        Ok(Manifest {
            release,
            platform,
            zip,
            files,
            deltas,
        })
    }

    pub fn release(&self) -> &Tag {
        &self.release
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
