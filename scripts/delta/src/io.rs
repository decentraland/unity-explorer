//! The I/O the executors depend on. `shell` implements the traits with real processes (`gh`,
//! `aws`, `@dcl/cdn-uploader`) and with local folders for tests and local runs.
//! `HDiffPatch` is concrete: tests and local runs use the real `hdiffz`/`hpatchz`.

use std::fmt;
use std::path::Path;

use anyhow::Result;

use crate::publishing::values::ETag;
use crate::releases::tag::Tag;
use crate::shell::hdiff::HDiffPatch;

/// GitHub releases of the repository, as far as a run needs them.
pub trait Releases {
    fn is_published(&self, tag: &Tag) -> Result<bool>;
}

/// How the CDN may cache an upload. `@dcl/cdn-uploader` sets the actual `Cache-Control`; see
/// "Immutable once published" in `docs/delta-updates.md`.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Caching {
    /// Never changes once uploaded (patches): cached long.
    Immutable,
    /// Rewritten while the release is unpublished (the manifest): cached briefly.
    ShortLived,
}

/// The bucket holding release zips and delta patches, behind the CDN. Keys are relative to
/// the bucket.
pub trait Bucket {
    fn remove_prefix(&self, prefix: &str) -> Result<()>;
    /// `None` when there is no object at `key`; an error for any other failure.
    fn download(&self, key: &str, to: &Path) -> Result<Option<ETag>>;
    fn etag(&self, key: &str) -> Result<ETag>;
    /// Uploads a folder's files under a key prefix.
    fn upload(&self, folder: &Path, prefix: &str, caching: Caching) -> Result<()>;
}

/// Everything an executor may touch outside the work folder.
pub struct Io {
    pub releases: Box<dyn Releases>,
    pub bucket: Box<dyn Bucket>,
    pub hdiff: HDiffPatch,
}

impl fmt::Debug for Io {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str("Io")
    }
}
