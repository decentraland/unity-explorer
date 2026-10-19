//! The lifecycle of one platform's delta run, as data. The `Run` is the model of the machine in
//! `fsm`; each state holds only what later states need.

use std::fmt;
use std::path::PathBuf;

use anyhow::{Result, ensure};
use serde::{Deserialize, Serialize};

use crate::builds::tree::Build;
use crate::patching::delta::Delta;
use crate::patching::replay::Difference;
use crate::publishing::layout::{UploadLayout, delta_prefix, zip_key};
use crate::publishing::manifest::Manifest;
use crate::publishing::values::{ETag, ZipName, ZipRef};
use crate::releases::bases::Bases;
use crate::releases::tag::{Platform, Tag};

/// The release the patches lead to, and where its files live.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Release {
    pub tag: Tag,
    pub platform: Platform,
    pub zip: ZipName,
    /// Key prefix of all releases in the bucket, e.g. `@dcl/unity-explorer/releases`.
    pub bucket_prefix: String,
    /// Local work folder of this run.
    pub work: PathBuf,
}

impl Release {
    pub fn zip_key(&self) -> String {
        zip_key(&self.bucket_prefix, &self.tag, &self.zip)
    }

    pub fn base_zip_key(&self, base: &Tag) -> String {
        zip_key(&self.bucket_prefix, base, &self.zip)
    }

    /// `<bucket_prefix>/<tag>/delta/<platform>`, where the patches and the manifest go.
    pub fn delta_prefix(&self) -> String {
        delta_prefix(&self.bucket_prefix, &self.tag, self.platform)
    }

    pub fn archive(&self) -> PathBuf {
        self.work.join("release.zip")
    }

    pub fn build_dir(&self) -> PathBuf {
        self.work.join("release")
    }

    pub fn base_archive(&self, base: &Tag) -> PathBuf {
        self.work.join("bases").join(format!("{base}.zip"))
    }

    pub fn base_dir(&self, base: &Tag) -> PathBuf {
        self.work.join("bases").join(base.to_string())
    }

    /// Patches before verification: `staging/from/<base>/<build id>/<path>.hdiff`.
    pub fn staging(&self) -> PathBuf {
        self.work.join("staging")
    }

    pub fn rebuild_dir(&self, base: &Tag) -> PathBuf {
        self.work.join("rebuild").join(base.to_string())
    }

    pub fn upload(&self) -> UploadLayout {
        UploadLayout {
            prefix: self.delta_prefix(),
            patches: self.work.join("upload").join("patches"),
            manifest: self.work.join("upload").join("manifest"),
        }
    }
}

/// What the run was asked for. `Request::new` refuses a run without bases and a release listed
/// as its own base; duplicate bases are ignored.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Request {
    pub release: Release,
    pub bases: Vec<Tag>,
}

impl Request {
    pub fn new(release: Release, bases: Vec<Tag>) -> Result<Request> {
        let mut unique: Vec<Tag> = Vec::new();
        for base in bases {
            ensure!(base != release.tag, "{base} cannot be its own base");
            if !unique.contains(&base) {
                unique.push(base);
            }
        }
        ensure!(!unique.is_empty(), "no base release to make patches from");
        Ok(Request { release, bases: unique })
    }
}

/// The release zip as downloaded; the `ETag` identifies that exact object.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ReleaseZip {
    pub zip: ZipRef,
    pub etag: ETag,
}

/// Zip paths of the release and of every base that has one.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Archives {
    pub release: Release,
    pub zip: ReleaseZip,
    pub bases: Bases<PathBuf>,
}

/// Build roots of the release and of every base that extracted cleanly.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Folders {
    pub release: Release,
    pub zip: ReleaseZip,
    pub root: PathBuf,
    pub bases: Bases<PathBuf>,
}

/// Every build described.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Builds {
    pub release: Release,
    pub zip: ReleaseZip,
    pub build: Build,
    pub bases: Bases<Build>,
}

/// A patch set encoded but not yet verified, with the base build it starts from.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Candidate {
    pub build: Build,
    pub delta: Delta,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Candidates {
    pub release: Release,
    pub zip: ReleaseZip,
    pub build: Build,
    pub bases: Bases<Candidate>,
}

/// The verified manifest and where its files go.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Upload {
    pub release: Release,
    pub zip: ReleaseZip,
    pub manifest: Manifest,
    pub layout: UploadLayout,
}

/// ```text
/// Requested -> Unpublished -> Cleared -> Downloaded -> Extracted -> Scanned -> Encoded
///           -> Verified -> Prepared -> Rechecked -> PatchesUploaded -> ReadyToPublish -> Published
/// any       -> Failed
/// ```
///
/// `Published` and `Failed` are terminal.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "state")]
pub enum State {
    /// Nothing done yet.
    Requested(Request),
    /// The release is not published, so its patches may be replaced.
    Unpublished(Request),
    /// Patches left by an earlier run of this release are gone from the bucket.
    Cleared(Request),
    Downloaded(Archives),
    Extracted(Folders),
    Scanned(Builds),
    Encoded(Candidates),
    /// Every remaining patch set rebuilt the release build exactly; the
    /// manifest is built from them.
    Verified(Upload),
    /// The upload folders hold the verified patches and `manifest.json`.
    Prepared(Upload),
    /// Still unpublished, and the zip in the bucket is the one the patches lead to.
    Rechecked(Upload),
    /// The patches are on the CDN; the manifest is not yet.
    PatchesUploaded(Upload),
    /// Rechecked again after the patch upload: still unpublished, zip unchanged.
    ReadyToPublish(Upload),
    /// The manifest is on the CDN: launchers can use the patches.
    Published(Manifest),
    /// A run-level failure, or every base dropped.
    Failed {
        reason: String,
    },
}

impl State {
    /// The states `--until` accepts: every state but `Unpublished`, `Rechecked` and
    /// `ReadyToPublish`, whose next cmd acts on a release check that would be stale when resumed
    /// later.
    pub const PAUSES: [&'static str; 11] = [
        "Requested",
        "Cleared",
        "Downloaded",
        "Extracted",
        "Scanned",
        "Encoded",
        "Verified",
        "Prepared",
        "PatchesUploaded",
        "Published",
        "Failed",
    ];

    /// Every state name, in lifecycle order.
    pub const NAMES: [&'static str; 14] = [
        "Requested",
        "Unpublished",
        "Cleared",
        "Downloaded",
        "Extracted",
        "Scanned",
        "Encoded",
        "Verified",
        "Prepared",
        "Rechecked",
        "PatchesUploaded",
        "ReadyToPublish",
        "Published",
        "Failed",
    ];

    pub fn name(&self) -> &'static str {
        match self {
            State::Requested(_) => "Requested",
            State::Unpublished(_) => "Unpublished",
            State::Cleared(_) => "Cleared",
            State::Downloaded(_) => "Downloaded",
            State::Extracted(_) => "Extracted",
            State::Scanned(_) => "Scanned",
            State::Encoded(_) => "Encoded",
            State::Verified(_) => "Verified",
            State::Prepared(_) => "Prepared",
            State::Rechecked(_) => "Rechecked",
            State::PatchesUploaded(_) => "PatchesUploaded",
            State::ReadyToPublish(_) => "ReadyToPublish",
            State::Published(_) => "Published",
            State::Failed { .. } => "Failed",
        }
    }
}

impl fmt::Display for State {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(self.name())
    }
}

/// Why a base was taken out of the run; the other bases continue.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "reason", content = "detail", rename_all = "kebab-case")]
pub enum DropReason {
    ZipMissing(String),
    Unreadable(String),
    EncodeFailed(String),
    RebuildFailed(String),
    RebuildDiffers(Vec<String>),
}

impl DropReason {
    pub fn rebuild_differs(differences: &[Difference]) -> DropReason {
        DropReason::RebuildDiffers(differences.iter().map(ToString::to_string).collect())
    }
}

impl fmt::Display for DropReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            DropReason::ZipMissing(detail) => write!(f, "zip missing: {detail}"),
            DropReason::Unreadable(detail) => write!(f, "unreadable: {detail}"),
            DropReason::EncodeFailed(detail) => write!(f, "encoding failed: {detail}"),
            DropReason::RebuildFailed(detail) => write!(f, "rebuild failed: {detail}"),
            DropReason::RebuildDiffers(paths) => write!(f, "rebuilt build differs: {}", paths.join(", ")),
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Dropped {
    pub base: Tag,
    pub reason: DropReason,
}

/// The model of the machine: the current state and the bases dropped so far.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Run {
    pub state: State,
    pub dropped: Vec<Dropped>,
}

#[cfg(test)]
mod tests {
    use anyhow::{Result, ensure};

    use super::{Release, Request};
    use crate::publishing::values::ZipName;
    use crate::releases::tag::{Platform, Tag};

    fn release() -> Result<Release> {
        Ok(Release {
            tag: "v1.1.0".parse()?,
            platform: Platform::Windows64,
            zip: ZipName::try_new("Decentraland_windows64.zip".to_owned())?,
            bucket_prefix: "releases".to_owned(),
            work: std::path::PathBuf::from("work"),
        })
    }

    #[test]
    fn a_request_needs_bases_other_than_the_release() -> Result<()> {
        ensure!(Request::new(release()?, vec![]).is_err());
        let own: Tag = "v1.1.0".parse()?;
        ensure!(Request::new(release()?, vec![own]).is_err());
        let base: Tag = "v1.0.0".parse()?;
        ensure!(Request::new(release()?, vec![base.clone(), base])?.bases.len() == 1);
        Ok(())
    }
}
