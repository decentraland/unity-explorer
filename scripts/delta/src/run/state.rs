//! The lifecycle of one platform's delta run, as data. The `Run` document carries the current
//! `State` between YAML steps; each state holds only what later states need.

use std::fmt;
use std::path::PathBuf;

use anyhow::{Result, ensure};
use serde::{Deserialize, Serialize};

use crate::builds::signatures::Signatures;
use crate::builds::tree::Build;
use crate::patching::delta::Delta;
use crate::patching::replay::Difference;
use crate::publishing::layout::{UploadLayout, delta_prefix, zip_key};
use crate::publishing::manifest::Manifest;
use crate::publishing::values::{ETag, ZipName, ZipRef};
use crate::releases::bases::Bases;
use crate::releases::tag::{Platform, Tag};
use crate::run::fsm::Step;

pub const SCHEMA: u32 = 2;

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

/// What the run was asked for. `Request::new` refuses a run without bases and a target listed
/// as its own base; duplicate bases are ignored.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Request {
    pub target: Target,
    pub bases: Vec<Tag>,
}

impl Request {
    pub fn new(target: Target, bases: Vec<Tag>) -> Result<Request> {
        let mut unique: Vec<Tag> = Vec::new();
        for base in bases {
            ensure!(base != target.tag, "{base} cannot be its own base");
            if !unique.contains(&base) {
                unique.push(base);
            }
        }
        ensure!(!unique.is_empty(), "no base release to make patches from");
        Ok(Request { target, bases: unique })
    }
}

/// The target release's zip as downloaded; the `ETag` identifies that exact object.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct TargetZip {
    pub zip: ZipRef,
    pub etag: ETag,
}

/// Zip paths of the target and of every base that has one.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Archives {
    pub target: Target,
    pub zip: TargetZip,
    pub bases: Bases<PathBuf>,
}

/// Build roots of the target and of every base that extracted cleanly.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Folders {
    pub target: Target,
    pub zip: TargetZip,
    pub root: PathBuf,
    pub bases: Bases<PathBuf>,
}

/// Every build described, and how the target's signed artifacts verify.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Builds {
    pub target: Target,
    pub zip: TargetZip,
    pub build: Build,
    pub signatures: Signatures,
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
    pub target: Target,
    pub zip: TargetZip,
    pub build: Build,
    pub signatures: Signatures,
    pub bases: Bases<Candidate>,
}

/// The verified manifest and where its files go.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Upload {
    pub target: Target,
    pub zip: TargetZip,
    pub manifest: Manifest,
    pub layout: UploadLayout,
}

/// ```text
/// Idle -> Unpublished -> Cleared -> Downloaded -> Extracted -> Scanned
///      -> Encoded -> Verified -> Assembled -> Rechecked -> PatchesUploaded -> Published
/// any  -> Failed
/// ```
///
/// `Published` and `Failed` are terminal. The variant's doc says what the state guarantees.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "state")]
pub enum State {
    /// Nothing done yet.
    Idle(Request),
    /// The target release is not published.
    Unpublished(Request),
    /// Patches left by an earlier run of this target are gone from the bucket.
    Cleared(Request),
    Downloaded(Archives),
    Extracted(Folders),
    Scanned(Builds),
    Encoded(Candidates),
    /// Every remaining patch set rebuilt the target build exactly, signatures included; the
    /// manifest is built from them.
    Verified(Upload),
    /// The upload folders hold the verified patches and `manifest.json`.
    Assembled(Upload),
    /// Still unpublished, and the zip in the bucket is the one the patches lead to.
    Rechecked(Upload),
    /// The patches are on the CDN; the manifest is not yet.
    PatchesUploaded(Upload),
    /// The manifest is on the CDN: launchers can use the patches.
    Published(Manifest),
    /// A run-level failure, or every base dropped.
    Failed {
        at: Step,
        reason: String,
    },
}

impl State {
    pub fn name(&self) -> &'static str {
        match self {
            State::Idle(_) => "Idle",
            State::Unpublished(_) => "Unpublished",
            State::Cleared(_) => "Cleared",
            State::Downloaded(_) => "Downloaded",
            State::Extracted(_) => "Extracted",
            State::Scanned(_) => "Scanned",
            State::Encoded(_) => "Encoded",
            State::Verified(_) => "Verified",
            State::Assembled(_) => "Assembled",
            State::Rechecked(_) => "Rechecked",
            State::PatchesUploaded(_) => "PatchesUploaded",
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

/// The document carried between YAML steps: the current state and the bases dropped so far.
/// Written only by this tool.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Run {
    pub schema: u32,
    pub state: State,
    pub dropped: Vec<Dropped>,
}

impl Run {
    pub fn new(request: Request) -> Run {
        Run {
            schema: SCHEMA,
            state: State::Idle(request),
            dropped: Vec::new(),
        }
    }
}

#[cfg(test)]
mod tests {
    use anyhow::{Result, ensure};

    use super::{Request, Target};
    use crate::publishing::values::ZipName;
    use crate::releases::tag::{Platform, Tag};

    fn target() -> Result<Target> {
        Ok(Target {
            tag: "v1.1.0".parse()?,
            platform: Platform::Windows64,
            zip: ZipName::try_new("Decentraland_windows64.zip".to_owned())?,
            releases: "releases".to_owned(),
            work: std::path::PathBuf::from("work"),
        })
    }

    #[test]
    fn a_request_needs_bases_other_than_the_target() -> Result<()> {
        ensure!(Request::new(target()?, vec![]).is_err());
        let own: Tag = "v1.1.0".parse()?;
        ensure!(Request::new(target()?, vec![own]).is_err());
        let base: Tag = "v1.0.0".parse()?;
        ensure!(Request::new(target()?, vec![base.clone(), base])?.bases.len() == 1);
        Ok(())
    }
}
