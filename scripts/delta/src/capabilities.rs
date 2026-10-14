//! The I/O the workflows depend on, passed in as parameters. `shell` implements each with real
//! processes (`gh`, `aws`, `npx`, `hdiffz`/`hpatchz`, `osslsigncode`, `codesign`) and with local
//! folders for tests and local runs.

use std::path::Path;

use anyhow::Result;

use crate::publishing::values::ETag;
use crate::releases::github::ReleaseDto;
use crate::releases::tag::{Platform, Tag};

/// GitHub releases of the repository.
pub trait Releases {
    fn is_published(&self, tag: &Tag) -> Result<bool>;
    fn list(&self) -> Result<Vec<ReleaseDto>>;
}

/// The bucket holding release zips and delta patches. Keys are relative to the bucket.
pub trait Storage {
    fn remove_prefix(&self, prefix: &str) -> Result<()>;
    fn download(&self, key: &str, to: &Path) -> Result<ETag>;
    fn etag(&self, key: &str) -> Result<ETag>;
}

/// How the CDN may cache an upload.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Caching {
    /// Never changes once uploaded (patches).
    Immutable,
    /// Rewritten while the release is unpublished (the manifest).
    Revalidated,
}

/// Uploads a folder's files under a key prefix.
pub trait Cdn {
    fn upload(&self, folder: &Path, prefix: &str, caching: Caching) -> Result<()>;
}

/// `HDiffPatch`: `diff` writes a patch turning `old` into `new`; `patch` applies one.
pub trait Differ {
    fn diff(&self, old: &Path, new: &Path, patch: &Path) -> Result<()>;
    fn patch(&self, old: &Path, patch: &Path, out: &Path) -> Result<()>;
}

/// Code-signature verification of one signed artifact.
pub trait Signer {
    fn verifies(&self, platform: Platform, artifact: &Path) -> Result<bool>;
}
