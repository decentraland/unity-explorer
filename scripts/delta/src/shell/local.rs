//! I/O backed by local folders and fixed answers, for tests and local runs.

use std::fs;
use std::path::{Path, PathBuf};

use anyhow::{Context, Result};
use walkdir::{DirEntry, WalkDir};

use crate::builds::disk::{remove, sha256_file};
use crate::io::{Bucket, Caching, Releases};
use crate::publishing::values::ETag;
use crate::releases::tag::Tag;

/// A bucket kept in a folder: key `a/b` is the file `<root>/a/b`. The `ETag` is the file's
/// sha256. Uploads copy the folder's files under `<root>/<prefix>/` and append the caching of
/// each upload to `<root>/<prefix>/.caching`.
#[derive(Debug, Clone)]
pub struct FolderBucket {
    pub root: PathBuf,
}

impl Bucket for FolderBucket {
    fn remove_prefix(&self, prefix: &str) -> Result<()> {
        remove(&self.root.join(prefix.trim_end_matches('/')))
    }

    fn download(&self, key: &str, to: &Path) -> Result<Option<ETag>> {
        let source: PathBuf = self.root.join(key);
        if !source.is_file() {
            return Ok(None);
        }
        if let Some(parent) = to.parent() {
            fs::create_dir_all(parent)?;
        }
        fs::copy(&source, to).with_context(|| format!("cannot copy {key}"))?;
        Ok(Some(self.etag(key)?))
    }

    fn etag(&self, key: &str) -> Result<ETag> {
        let source: PathBuf = self.root.join(key);
        anyhow::ensure!(source.is_file(), "no object {key}");
        Ok(ETag::try_new(sha256_file(&source)?.to_string())?)
    }

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

/// Only the listed tags are published.
#[derive(Debug, Clone, Default)]
pub struct FixedReleases {
    pub published: Vec<Tag>,
}

impl Releases for FixedReleases {
    fn is_published(&self, tag: &Tag) -> Result<bool> {
        Ok(self.published.contains(tag))
    }
}
