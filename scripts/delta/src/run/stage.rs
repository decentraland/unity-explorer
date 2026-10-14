//! The lifecycle of one platform's delta run. Each stage holds only what later stages need; the
//! `Run` document carries the current one between workflow invocations.

use std::fmt;
use std::path::PathBuf;

use serde::{Deserialize, Serialize};

use crate::builds::signatures::Signatures;
use crate::builds::tree::{Build, Tree};
use crate::patching::delta::Delta;
use crate::patching::replay::Difference;
use crate::publishing::layout::{UploadLayout, delta_prefix, zip_key};
use crate::publishing::manifest::Manifest;
use crate::publishing::values::{ETag, ZipName, ZipRef};
use crate::releases::bases::Bases;
use crate::releases::tag::{Platform, Tag};

pub const SCHEMA: u32 = 1;

/// The release this run makes patches into, and where its files live.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Target {
    pub tag: Tag,
    pub platform: Platform,
    pub zip: ZipName,
    /// Key prefix of all releases in the bucket, e.g. `@dcl/unity-explorer/releases`.
    pub releases: String,
    /// Local work folder of this run.
    pub work: PathBuf,
}

impl Target {
    pub fn zip_key(&self) -> String {
        zip_key(&self.releases, &self.tag, &self.zip)
    }

    pub fn base_zip_key(&self, base: &Tag) -> String {
        zip_key(&self.releases, base, &self.zip)
    }

    pub fn prefix(&self) -> String {
        delta_prefix(&self.releases, &self.tag, self.platform)
    }

    pub fn archive(&self) -> PathBuf {
        self.work.join("target.zip")
    }

    pub fn build_dir(&self) -> PathBuf {
        self.work.join("target")
    }

    pub fn base_archive(&self, base: &Tag) -> PathBuf {
        self.work.join("bases").join(format!("{base}.zip"))
    }

    pub fn base_dir(&self, base: &Tag) -> PathBuf {
        self.work.join("bases").join(base.to_string())
    }

    /// Patches before verification: `staging/from/<base>/<path>.hdiff`.
    pub fn staging(&self) -> PathBuf {
        self.work.join("staging")
    }

    pub fn rebuild_dir(&self, base: &Tag) -> PathBuf {
        self.work.join("rebuild").join(base.to_string())
    }

    pub fn upload(&self) -> UploadLayout {
        UploadLayout {
            prefix: self.prefix(),
            patches: self.work.join("upload").join("patches"),
            manifest: self.work.join("upload").join("manifest"),
        }
    }
}

/// The target release's zip as downloaded; the `ETag` identifies that exact object.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Fetched {
    pub zip: ZipRef,
    pub etag: ETag,
}

/// A patch set made but not yet verified, with the base build it starts from.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Candidate {
    pub build: Build,
    pub delta: Delta,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Requested {
    pub target: Target,
    pub bases: Vec<Tag>,
}

/// The target release is not published yet.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Confirmed {
    pub target: Target,
    pub bases: Vec<Tag>,
}

/// Patches left by an earlier run of this target were removed from the bucket.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Cleared {
    pub target: Target,
    pub bases: Vec<Tag>,
}

/// Zip paths of the target and of every base that has one.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Downloaded {
    pub target: Target,
    pub fetched: Fetched,
    pub bases: Bases<PathBuf>,
}

/// Build roots of the target and of every base that extracted cleanly.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Extracted {
    pub target: Target,
    pub fetched: Fetched,
    pub root: PathBuf,
    pub bases: Bases<PathBuf>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Inspected {
    pub target: Target,
    pub fetched: Fetched,
    pub build: Build,
    pub signatures: Signatures,
    pub bases: Bases<Build>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Encoded {
    pub target: Target,
    pub fetched: Fetched,
    pub build: Build,
    pub signatures: Signatures,
    pub bases: Bases<Candidate>,
}

/// Every remaining patch set rebuilt the target build exactly, signatures included.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Verified {
    pub target: Target,
    pub fetched: Fetched,
    pub files: Tree,
    pub deltas: Bases<Delta>,
}

/// The upload folders hold the verified patches and `manifest.json`.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Assembled {
    pub target: Target,
    pub fetched: Fetched,
    pub manifest: Manifest,
    pub layout: UploadLayout,
}

/// Still unpublished, and the zip in the bucket is the one the patches lead to.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Rechecked {
    pub target: Target,
    pub manifest: Manifest,
    pub layout: UploadLayout,
}

/// The patches are on the CDN; the manifest is not yet.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Uploaded {
    pub target: Target,
    pub manifest: Manifest,
    pub layout: UploadLayout,
}

/// The manifest is on the CDN: launchers can use the patches.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Published {
    pub target: Target,
    pub manifest: Manifest,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "stage")]
pub enum Stage {
    Requested(Requested),
    Confirmed(Confirmed),
    Cleared(Cleared),
    Downloaded(Downloaded),
    Extracted(Extracted),
    Inspected(Inspected),
    Encoded(Encoded),
    Verified(Verified),
    Assembled(Assembled),
    Rechecked(Rechecked),
    Uploaded(Uploaded),
    Published(Published),
    /// Terminal: a run-level failure, or every base dropped.
    Failed {
        at: String,
        reason: String,
    },
}

impl Stage {
    pub fn name(&self) -> &'static str {
        match self {
            Stage::Requested(_) => "Requested",
            Stage::Confirmed(_) => "Confirmed",
            Stage::Cleared(_) => "Cleared",
            Stage::Downloaded(_) => "Downloaded",
            Stage::Extracted(_) => "Extracted",
            Stage::Inspected(_) => "Inspected",
            Stage::Encoded(_) => "Encoded",
            Stage::Verified(_) => "Verified",
            Stage::Assembled(_) => "Assembled",
            Stage::Rechecked(_) => "Rechecked",
            Stage::Uploaded(_) => "Uploaded",
            Stage::Published(_) => "Published",
            Stage::Failed { .. } => "Failed",
        }
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
    SignaturesChanged(Vec<String>),
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
            DropReason::SignaturesChanged(paths) => write!(f, "signatures changed: {}", paths.join(", ")),
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Dropped {
    pub base: Tag,
    pub reason: DropReason,
}

/// The document carried between workflow invocations: the current stage and the bases dropped
/// so far. Written only by this tool.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Run {
    pub schema: u32,
    pub stage: Stage,
    pub drops: Vec<Dropped>,
}

impl Run {
    pub fn start(requested: Requested) -> Run {
        Run {
            schema: SCHEMA,
            stage: Stage::Requested(requested),
            drops: Vec::new(),
        }
    }
}
