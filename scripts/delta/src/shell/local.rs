//! Capabilities backed by local folders and fixed answers, for tests and local runs.

use std::fs;
use std::path::{Path, PathBuf};

use anyhow::{Context, Result, ensure};
use walkdir::{DirEntry, WalkDir};

use crate::builds::disk::{remove, sha256_file};
use crate::capabilities::{Caching, Cdn, Releases, Signer, Storage};
use crate::publishing::values::ETag;
use crate::releases::github::ReleaseDto;
use crate::releases::tag::{Platform, Tag};

/// A bucket kept in a folder: key `a/b` is the file `<root>/a/b`. The `ETag` is the file's sha256.
#[derive(Debug, Clone)]
pub struct FolderStorage {
    pub root: PathBuf,
}

impl Storage for FolderStorage {
    fn remove_prefix(&self, prefix: &str) -> Result<()> {
        remove(&self.root.join(prefix.trim_end_matches('/')))
    }

    fn download(&self, key: &str, to: &Path) -> Result<ETag> {
        let source: PathBuf = self.root.join(key);
        ensure!(source.is_file(), "no object {key}");
        if let Some(parent) = to.parent() {
            fs::create_dir_all(parent)?;
        }
        fs::copy(&source, to).with_context(|| format!("cannot copy {key}"))?;
        self.etag(key)
    }

    fn etag(&self, key: &str) -> Result<ETag> {
        let source: PathBuf = self.root.join(key);
        Ok(ETag::try_new(sha256_file(&source)?.to_string())?)
    }
}

/// A CDN kept in a folder: uploads copy the folder's files under `<root>/<prefix>/`. Records
/// the caching of each upload in `<root>/<prefix>/.caching`.
#[derive(Debug, Clone)]
pub struct FolderCdn {
    pub root: PathBuf,
}

impl Cdn for FolderCdn {
    fn upload(&self, folder: &Path, prefix: &str, caching: Caching) -> Result<()> {
        let destination: PathBuf = self.root.join(prefix);
        for item in WalkDir::new(folder).min_depth(1).follow_links(false) {
            let entry: DirEntry = item?;
            if entry.file_type().is_file() {
                let target: PathBuf = destination.join(entry.path().strip_prefix(folder)?);
                if let Some(parent) = target.parent() {
                    fs::create_dir_all(parent)?;
                }
                fs::copy(entry.path(), &target).with_context(|| format!("cannot upload {}", entry.path().display()))?;
            }
        }
        let line: String = format!("{caching:?}\n");
        let log: PathBuf = destination.join(".caching");
        let mut previous: String = fs::read_to_string(&log).unwrap_or_default();
        previous.push_str(&line);
        fs::create_dir_all(&destination)?;
        fs::write(log, previous)?;
        Ok(())
    }
}

/// Fixed answers about GitHub releases.
#[derive(Debug, Clone, Default)]
pub struct FixedReleases {
    pub published: Vec<Tag>,
    pub listed: Vec<ReleaseDto>,
}

impl Releases for FixedReleases {
    fn is_published(&self, tag: &Tag) -> Result<bool> {
        Ok(self.published.contains(tag))
    }

    fn list(&self) -> Result<Vec<ReleaseDto>> {
        Ok(self.listed.clone())
    }
}

/// Every artifact verifies, except those under `invalid_under`.
#[derive(Debug, Clone, Default)]
pub struct FixedSigner {
    pub invalid_under: Option<PathBuf>,
}

impl Signer for FixedSigner {
    fn verifies(&self, _platform: Platform, artifact: &Path) -> Result<bool> {
        Ok(match &self.invalid_under {
            Some(folder) => !artifact.starts_with(folder),
            None => true,
        })
    }
}
