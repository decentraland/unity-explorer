//! Implementations of the I/O: real tools for CI, local folders for tests and local runs.

pub mod hdiff;
pub mod local;
pub mod process;
pub mod remote;

use std::path::PathBuf;

use crate::io::Io;
use crate::shell::hdiff::HDiffPatch;
use crate::shell::local::{FixedReleases, FixedSigner, FolderBucket};
use crate::shell::remote::{GhReleases, S3Bucket, SystemSigner};

/// GitHub through `gh`, the bucket through `aws` and `@dcl/cdn-uploader`, signatures through
/// `osslsigncode` / `codesign`.
pub fn ci(hdiff: HDiffPatch) -> Io {
    Io {
        releases: Box::new(GhReleases),
        bucket: Box::new(S3Bucket),
        signer: Box::new(SystemSigner),
        hdiff,
    }
}

/// `root` as the bucket, nothing published, every signature valid.
pub fn folder(root: PathBuf, hdiff: HDiffPatch) -> Io {
    Io {
        releases: Box::new(FixedReleases::default()),
        bucket: Box::new(FolderBucket { root }),
        signer: Box::new(FixedSigner::default()),
        hdiff,
    }
}
