//! The state machine of a run, in the shape of Elm: `init` and `update` return the next state
//! and the cmds that state needs, as a pair. A `Msg` is a fact from I/O; a `Cmd` is what the
//! machine wants done, and `Cmd::Complete` asks the runtime to stop. Every decision lives here:
//! keys and paths, the patch plans, the replay order, the verdict on a rebuilt build, the
//! manifest, the recheck. Nothing here touches the file system, processes or the environment.

use std::collections::BTreeMap;
use std::fmt;
use std::path::PathBuf;

use serde::{Deserialize, Serialize};

use crate::builds::signatures::{Signatures, signature_changes};
use crate::builds::tree::{Build, Tree};
use crate::builds::values::{RelPath, Sha256};
use crate::patching::delta::Delta;
use crate::patching::plan::{Plan, plan};
use crate::patching::replay::{Action, Difference, differences, replay_actions};
use crate::publishing::contract::ManifestDto;
use crate::publishing::layout::{UploadLayout, base_folder};
use crate::publishing::manifest::Manifest;
use crate::publishing::values::{ETag, ZipRef};
use crate::releases::bases::Bases;
use crate::releases::tag::{Platform, Tag};
use crate::run::state::{
    Archives, Builds, Candidate, Candidates, DropReason, Dropped, Folders, ReleaseZip, Request, Run, State, Upload,
};

/// A rebuilt build as the launcher would produce it.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Rebuilt {
    pub tree: Tree,
    pub signatures: Signatures,
}

/// A fact from I/O, past tense. Success carries what was produced; a base that failed is listed
/// with its reason.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Msg {
    StalePatchesRemoved,
    ArchivesDownloaded {
        zip_etag: ETag,
        zip_size: u64,
        zip_sha256: Sha256,
        bases: Vec<(Tag, PathBuf)>,
        missing: Vec<(Tag, String)>,
    },
    ArchivesExtracted {
        release_root: PathBuf,
        bases: Vec<(Tag, PathBuf)>,
        unreadable: Vec<(Tag, String)>,
    },
    BuildsScanned {
        release: Build,
        signatures: Signatures,
        bases: Vec<(Tag, Build)>,
        unreadable: Vec<(Tag, String)>,
    },
    PatchesEncoded {
        encoded: Vec<(Tag, Delta)>,
        failed: Vec<(Tag, String)>,
    },
    PatchesReplayed {
        rebuilt: Vec<(Tag, Rebuilt)>,
        failed: Vec<(Tag, String)>,
    },
    UploadFolderPrepared,
    ReleaseRechecked {
        published: bool,
        zip_etag: ETag,
    },
    PatchesUploaded,
    ManifestPublished,
    /// The cmd could not be carried out at all (credentials, network, a corrupt disk). Nothing
    /// after it can be trusted.
    IntegrityFailure {
        reason: String,
    },
}

impl fmt::Display for Msg {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Msg::StalePatchesRemoved => f.write_str("stale patches removed"),
            Msg::ArchivesDownloaded {
                zip_size,
                zip_etag,
                bases,
                missing,
                ..
            } => write!(
                f,
                "archives downloaded: release zip {zip_size} bytes (ETag {zip_etag}), {} bases, {} missing",
                bases.len(),
                missing.len()
            ),
            Msg::ArchivesExtracted { bases, unreadable, .. } => write!(
                f,
                "archives extracted: {} bases, {} unreadable",
                bases.len(),
                unreadable.len()
            ),
            Msg::BuildsScanned {
                release,
                signatures,
                bases,
                unreadable,
            } => write!(
                f,
                "builds scanned: release has {} entries and {} signed artifacts, {} bases, {} unreadable",
                release.tree.entries().len(),
                signatures.len(),
                bases.len(),
                unreadable.len()
            ),
            Msg::PatchesEncoded { encoded, failed } => {
                write!(f, "patches encoded: {} bases, {} failed", encoded.len(), failed.len())
            }
            Msg::PatchesReplayed { rebuilt, failed } => {
                write!(f, "patches replayed: {} bases, {} failed", rebuilt.len(), failed.len())
            }
            Msg::UploadFolderPrepared => f.write_str("upload folder prepared"),
            Msg::ReleaseRechecked { published, zip_etag } => write!(
                f,
                "release rechecked: {}, zip ETag {zip_etag}",
                if *published { "published" } else { "not published" }
            ),
            Msg::PatchesUploaded => f.write_str("patches uploaded"),
            Msg::ManifestPublished => f.write_str("manifest published"),
            Msg::IntegrityFailure { reason } => write!(f, "integrity failure: {reason}"),
        }
    }
}

/// An object to download: the bucket key and the local path.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Fetch {
    pub key: String,
    pub to: PathBuf,
}

/// An archive to extract into an empty folder.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Unpack {
    pub archive: PathBuf,
    pub into: PathBuf,
}

/// A patch set to apply to a copy of its base build, in the launcher's order.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Replay {
    pub base: PathBuf,
    pub actions: Vec<Action>,
    pub rebuild: PathBuf,
}

/// What the machine wants done.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "cmd", rename_all = "kebab-case")]
pub enum Cmd {
    RemoveStalePatches {
        prefix: String,
    },
    DownloadArchives {
        release: Fetch,
        bases: Vec<(Tag, Fetch)>,
    },
    /// Extraction consumes each archive.
    ExtractArchives {
        release: Unpack,
        bases: Vec<(Tag, Unpack)>,
    },
    ScanBuilds {
        platform: Platform,
        release_root: PathBuf,
        bases: Vec<(Tag, PathBuf)>,
    },
    /// Patches go to `staging/from/<base>/<build_id>/<path>.hdiff`. A base that fails leaves
    /// nothing there.
    EncodePatches {
        staging: PathBuf,
        /// The first hex digits of the release zip's sha256 and the encoding generation
        /// (`3f9a12bc-e1`): a rebuild or a re-encode never reuses a patch key.
        build_id: String,
        release: Build,
        bases: Vec<(Tag, Build, Plan)>,
    },
    /// Replay consumes the base build and its rebuild folder.
    ReplayPatches {
        platform: Platform,
        staging: PathBuf,
        artifacts: Vec<RelPath>,
        bases: Vec<(Tag, Replay)>,
    },
    /// Moves each `(from, to)` into the upload folders and writes `manifest.json`.
    PrepareUploadFolder {
        manifest_json: String,
        layout: UploadLayout,
        moves: Vec<(PathBuf, PathBuf)>,
    },
    RecheckRelease {
        tag: Tag,
        zip_key: String,
    },
    /// Immutable: a patch never changes once uploaded.
    UploadPatches {
        folder: PathBuf,
        prefix: String,
    },
    /// Revalidated: a rebuild of this unpublished release rewrites the manifest.
    PublishManifest {
        folder: PathBuf,
        prefix: String,
    },
    /// Asks the runtime to stop.
    Complete,
}

impl fmt::Display for Cmd {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let name: &str = match self {
            Cmd::RemoveStalePatches { .. } => "remove-stale-patches",
            Cmd::DownloadArchives { .. } => "download-archives",
            Cmd::ExtractArchives { .. } => "extract-archives",
            Cmd::ScanBuilds { .. } => "scan-builds",
            Cmd::EncodePatches { .. } => "encode-patches",
            Cmd::ReplayPatches { .. } => "replay-patches",
            Cmd::PrepareUploadFolder { .. } => "prepare-upload-folder",
            Cmd::RecheckRelease { .. } => "recheck-release",
            Cmd::UploadPatches { .. } => "upload-patches",
            Cmd::PublishManifest { .. } => "publish-manifest",
            Cmd::Complete => "complete",
        };
        f.write_str(name)
    }
}

/// The request becomes the first state and the first cmd: nothing is removed from the bucket
/// before the release is known to be unpublished.
pub fn init(request: Request) -> (Run, Vec<Cmd>) {
    let cmd: Cmd = Cmd::RecheckRelease {
        tag: request.release.tag.clone(),
        zip_key: request.release.zip_key(),
    };
    let run: Run = Run {
        state: State::Requested(request),
        dropped: Vec::new(),
    };
    (run, vec![cmd])
}

/// Folds a fact into the next state, with the cmds that state needs. A reason ends the run:
/// `Failed`, asking to stop.
pub fn update(run: Run, msg: Msg) -> (Run, Vec<Cmd>) {
    let Run { state, mut dropped } = run;
    let (state, cmds): (State, Vec<Cmd>) = match transition(state, msg, &mut dropped) {
        Ok(next) => next,
        Err(reason) => (State::Failed { reason }, vec![Cmd::Complete]),
    };
    (Run { state, dropped }, cmds)
}

/// The transition table: the next state and its cmds for every fact a state accepts.
#[allow(clippy::too_many_lines)] // one arm per fact: the table reads best whole
fn transition(state: State, msg: Msg, dropped: &mut Vec<Dropped>) -> Result<(State, Vec<Cmd>), String> {
    match (state, msg) {
        (State::Failed { reason }, _) => Ok((State::Failed { reason }, vec![Cmd::Complete])),
        (state, Msg::IntegrityFailure { reason }) => Err(format!("{reason} (the run was {state})")),
        (State::Requested(request), Msg::ReleaseRechecked { published, .. }) => {
            if published {
                return Err(format!(
                    "{} is published; its delta patches are immutable",
                    request.release.tag
                ));
            }
            let cmd: Cmd = Cmd::RemoveStalePatches {
                prefix: format!("{}/", request.release.delta_prefix()),
            };
            Ok((State::Unpublished(request), vec![cmd]))
        }
        (State::Unpublished(request), Msg::StalePatchesRemoved) => {
            let cmd: Cmd = download_archives(&request);
            Ok((State::Cleared(request), vec![cmd]))
        }
        (
            State::Cleared(request),
            Msg::ArchivesDownloaded {
                zip_etag,
                zip_size,
                zip_sha256,
                bases,
                missing,
            },
        ) => {
            let Request { release, bases: held } = request;
            let held: Vec<(Tag, ())> = held.into_iter().map(|base: Tag| (base, ())).collect();
            let kept: BTreeMap<Tag, PathBuf> = settle(held, bases, missing, DropReason::ZipMissing, dropped)
                .into_iter()
                .map(|(base, ((), path)): (Tag, ((), PathBuf))| (base, path))
                .collect();
            let zip: ReleaseZip = ReleaseZip {
                zip: ZipRef {
                    name: release.zip.clone(),
                    size: zip_size,
                    sha256: zip_sha256,
                },
                etag: zip_etag,
            };
            let archives: Archives = Archives {
                release,
                zip,
                bases: remaining(kept, dropped)?,
            };
            let cmd: Cmd = extract_archives(&archives);
            Ok((State::Downloaded(archives), vec![cmd]))
        }
        (
            State::Downloaded(archives),
            Msg::ArchivesExtracted {
                release_root,
                bases,
                unreadable,
            },
        ) => {
            let Archives {
                release,
                zip,
                bases: held,
            } = archives;
            let kept: BTreeMap<Tag, PathBuf> = settle(held, bases, unreadable, DropReason::Unreadable, dropped)
                .into_iter()
                .map(|(base, (_, root)): (Tag, (PathBuf, PathBuf))| (base, root))
                .collect();
            let folders: Folders = Folders {
                release,
                zip,
                root: release_root,
                bases: remaining(kept, dropped)?,
            };
            let cmd: Cmd = scan_builds(&folders);
            Ok((State::Extracted(folders), vec![cmd]))
        }
        (
            State::Extracted(folders),
            Msg::BuildsScanned {
                release: build,
                signatures,
                bases,
                unreadable,
            },
        ) => {
            let Folders {
                release,
                zip,
                bases: held,
                ..
            } = folders;
            let kept: BTreeMap<Tag, Build> = settle(held, bases, unreadable, DropReason::Unreadable, dropped)
                .into_iter()
                .map(|(base, (_, base_build)): (Tag, (PathBuf, Build))| (base, base_build))
                .collect();
            let builds: Builds = Builds {
                release,
                zip,
                build,
                signatures,
                bases: remaining(kept, dropped)?,
            };
            let cmd: Cmd = encode_patches(&builds);
            Ok((State::Scanned(builds), vec![cmd]))
        }
        (State::Scanned(builds), Msg::PatchesEncoded { encoded, failed }) => {
            let Builds {
                release,
                zip,
                build,
                signatures,
                bases: held,
            } = builds;
            let kept: BTreeMap<Tag, Candidate> = settle(held, encoded, failed, DropReason::EncodeFailed, dropped)
                .into_iter()
                .map(|(base, (base_build, delta)): (Tag, (Build, Delta))| {
                    let candidate: Candidate = Candidate {
                        build: base_build,
                        delta,
                    };
                    (base, candidate)
                })
                .collect();
            let candidates: Candidates = Candidates {
                release,
                zip,
                build,
                signatures,
                bases: remaining(kept, dropped)?,
            };
            let cmd: Cmd = replay_patches(&candidates);
            Ok((State::Encoded(candidates), vec![cmd]))
        }
        (State::Encoded(candidates), Msg::PatchesReplayed { rebuilt, failed }) => {
            let Candidates {
                release,
                zip,
                build,
                signatures,
                bases: held,
            } = candidates;
            let mut kept: BTreeMap<Tag, Delta> = BTreeMap::new();
            for (base, (candidate, rebuilt)) in settle(held, rebuilt, failed, DropReason::RebuildFailed, dropped) {
                match judge(&build.tree, &signatures, &rebuilt.tree, &rebuilt.signatures, cfg!(unix)) {
                    None => {
                        kept.insert(base, candidate.delta);
                    }
                    Some(reason) => drop_base(base, reason, dropped),
                }
            }
            let deltas: Bases<Delta> = remaining(kept, dropped)?;
            let manifest: Manifest = Manifest::new(
                release.tag.clone(),
                release.platform,
                zip.zip.clone(),
                build.tree,
                deltas,
            )
            .map_err(|error: anyhow::Error| format!("{error:#}"))?;
            let layout: UploadLayout = release.upload();
            let upload: Upload = Upload {
                release,
                zip,
                manifest,
                layout,
            };
            let cmd: Cmd = prepare_upload_folder(&upload)?;
            Ok((State::Verified(upload), vec![cmd]))
        }
        (State::Verified(upload), Msg::UploadFolderPrepared) => {
            let cmd: Cmd = recheck_release(&upload);
            Ok((State::Prepared(upload), vec![cmd]))
        }
        (State::Prepared(upload), Msg::ReleaseRechecked { published, zip_etag }) => {
            still_the_same_release(&upload, published, &zip_etag)?;
            let cmd: Cmd = Cmd::UploadPatches {
                folder: upload.layout.patches.clone(),
                prefix: upload.layout.prefix.clone(),
            };
            Ok((State::Rechecked(upload), vec![cmd]))
        }
        (State::Rechecked(upload), Msg::PatchesUploaded) => {
            let cmd: Cmd = recheck_release(&upload);
            Ok((State::PatchesUploaded(upload), vec![cmd]))
        }
        (State::PatchesUploaded(upload), Msg::ReleaseRechecked { published, zip_etag }) => {
            still_the_same_release(&upload, published, &zip_etag)?;
            let cmd: Cmd = Cmd::PublishManifest {
                folder: upload.layout.manifest.clone(),
                prefix: upload.layout.prefix.clone(),
            };
            Ok((State::ReadyToPublish(upload), vec![cmd]))
        }
        (State::ReadyToPublish(upload), Msg::ManifestPublished) => {
            Ok((State::Published(upload.manifest), vec![Cmd::Complete]))
        }
        (state, msg) => Err(format!("'{msg}' does not fit a {state} run")),
    }
}

fn download_archives(request: &Request) -> Cmd {
    let release = &request.release;
    Cmd::DownloadArchives {
        release: Fetch {
            key: release.zip_key(),
            to: release.archive(),
        },
        bases: request
            .bases
            .iter()
            .map(|base: &Tag| {
                let fetch: Fetch = Fetch {
                    key: release.base_zip_key(base),
                    to: release.base_archive(base),
                };
                (base.clone(), fetch)
            })
            .collect(),
    }
}

fn extract_archives(archives: &Archives) -> Cmd {
    let release = &archives.release;
    Cmd::ExtractArchives {
        release: Unpack {
            archive: release.archive(),
            into: release.build_dir(),
        },
        bases: archives
            .bases
            .iter()
            .map(|(base, archive): (&Tag, &PathBuf)| {
                let unpack: Unpack = Unpack {
                    archive: archive.clone(),
                    into: release.base_dir(base),
                };
                (base.clone(), unpack)
            })
            .collect(),
    }
}

fn scan_builds(folders: &Folders) -> Cmd {
    Cmd::ScanBuilds {
        platform: folders.release.platform,
        release_root: folders.root.clone(),
        bases: folders
            .bases
            .iter()
            .map(|(base, root): (&Tag, &PathBuf)| (base.clone(), root.clone()))
            .collect(),
    }
}

/// Hex digits of the release zip's sha256 in every patch key (docs/delta-updates.md, S3 layout).
const BUILD_ID_DIGITS: usize = 8;

/// Bumped whenever `hdiff::VERSION`, `hdiff::HDIFFZ_ARGS`, the whole-file rule or the bundle
/// pairing changes the bytes a zip encodes to (pinned by `encoding_generation_pins_the_encoder`), so a re-encode of the same zip never reuses a patch key.
const ENCODING_GENERATION: u32 = 1;

/// The first hex digits of the release zip's sha256 and the encoding generation, part of every
/// patch key.
fn build_id(zip: &ReleaseZip) -> String {
    let digest: String = zip.zip.sha256.as_ref().chars().take(BUILD_ID_DIGITS).collect();
    format!("{digest}-e{ENCODING_GENERATION}")
}

fn encode_patches(builds: &Builds) -> Cmd {
    Cmd::EncodePatches {
        staging: builds.release.staging(),
        build_id: build_id(&builds.zip),
        release: builds.build.clone(),
        bases: builds
            .bases
            .iter()
            .map(|(base, build): (&Tag, &Build)| (base.clone(), build.clone(), plan(&build.tree, &builds.build.tree)))
            .collect(),
    }
}

fn replay_patches(candidates: &Candidates) -> Cmd {
    let release = &candidates.release;
    Cmd::ReplayPatches {
        platform: release.platform,
        staging: release.staging(),
        artifacts: candidates.signatures.keys().cloned().collect(),
        bases: candidates
            .bases
            .iter()
            .map(|(base, candidate): (&Tag, &Candidate)| {
                let replay: Replay = Replay {
                    base: candidate.build.root.clone(),
                    actions: replay_actions(&candidate.delta, &candidates.build.tree),
                    rebuild: release.rebuild_dir(base),
                };
                (base.clone(), replay)
            })
            .collect(),
    }
}

fn recheck_release(upload: &Upload) -> Cmd {
    Cmd::RecheckRelease {
        tag: upload.release.tag.clone(),
        zip_key: upload.release.zip_key(),
    }
}

/// The release must still be unpublished, and its zip in the bucket must still be the one the
/// patches lead to.
fn still_the_same_release(upload: &Upload, published: bool, zip_etag: &ETag) -> Result<(), String> {
    if published {
        return Err(format!(
            "{} was published while its patches were made",
            upload.release.tag
        ));
    }
    if *zip_etag != upload.zip.etag {
        return Err(format!(
            "{} changed in the bucket during the run (ETag {} -> {zip_etag})",
            upload.release.zip, upload.zip.etag
        ));
    }
    Ok(())
}

fn prepare_upload_folder(upload: &Upload) -> Result<Cmd, String> {
    let manifest_json: String = serde_json::to_string_pretty(&ManifestDto::from(&upload.manifest))
        .map_err(|error: serde_json::Error| error.to_string())?;
    let staging: PathBuf = upload.release.staging();
    let moves: Vec<(PathBuf, PathBuf)> = upload
        .manifest
        .deltas()
        .keys()
        .map(|base: &Tag| {
            let folder: String = base_folder(base);
            (staging.join(&folder), upload.layout.patches.join(&folder))
        })
        .collect();
    Ok(Cmd::PrepareUploadFolder {
        manifest_json,
        layout: upload.layout.clone(),
        moves,
    })
}

/// Whether a rebuilt build is the release build: same entries, and every signed artifact
/// verifies the same way.
pub fn judge(
    release: &Tree,
    signatures: &Signatures,
    rebuilt: &Tree,
    rebuilt_signatures: &Signatures,
    compare_modes: bool,
) -> Option<DropReason> {
    let found: Vec<Difference> = differences(release, rebuilt, compare_modes);
    if !found.is_empty() {
        return Some(DropReason::rebuild_differs(&found));
    }
    let changed: Vec<RelPath> = signature_changes(signatures, rebuilt_signatures);
    if !changed.is_empty() {
        return Some(DropReason::SignaturesChanged(
            changed.iter().map(ToString::to_string).collect(),
        ));
    }
    None
}

fn drop_base(base: Tag, reason: DropReason, dropped: &mut Vec<Dropped>) {
    dropped.push(Dropped { base, reason });
}

/// Pairs each base the run holds with what the executor produced for it. A base listed as
/// failed, or listed nowhere, is dropped.
fn settle<H, R>(
    held: impl IntoIterator<Item = (Tag, H)>,
    produced: Vec<(Tag, R)>,
    failed: Vec<(Tag, String)>,
    reason: fn(String) -> DropReason,
    dropped: &mut Vec<Dropped>,
) -> BTreeMap<Tag, (H, R)> {
    let mut produced: BTreeMap<Tag, R> = produced.into_iter().collect();
    let mut failed: BTreeMap<Tag, String> = failed.into_iter().collect();
    let mut kept: BTreeMap<Tag, (H, R)> = BTreeMap::new();
    for (base, value) in held {
        if let Some(result) = produced.remove(&base) {
            kept.insert(base, (value, result));
        } else {
            let detail: String = failed.remove(&base).unwrap_or_else(|| "no result reported".to_owned());
            drop_base(base, reason(detail), dropped);
        }
    }
    kept
}

/// The bases still in the run; a reason naming every drop when none is left.
fn remaining<T>(kept: BTreeMap<Tag, T>, dropped: &[Dropped]) -> Result<Bases<T>, String> {
    if kept.is_empty() {
        let reasons: Vec<String> = dropped
            .iter()
            .map(|d: &Dropped| format!("{}: {}", d.base, d.reason))
            .collect();
        return Err(format!("no base left ({})", reasons.join("; ")));
    }
    Bases::new(kept).map_err(|error: anyhow::Error| format!("{error:#}"))
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeMap;
    use std::path::PathBuf;

    use anyhow::{Context, Result, bail, ensure};

    use super::{Cmd, ENCODING_GENERATION, Msg, Rebuilt, init, judge, update};
    use crate::builds::signatures::Signatures;
    use crate::builds::tree::{Build, Node, Tree};
    use crate::builds::values::{Mode, RelPath, Sha256};
    use crate::patching::delta::{Delta, Whole};
    use crate::patching::plan::Job;
    use crate::publishing::values::{ETag, ZipName};
    use crate::releases::tag::{Platform, Tag};
    use crate::run::state::{DropReason, Dropped, Release, Request, Run, State};
    use crate::shell::hdiff::{HDIFFZ_ARGS, VERSION};

    const RELEASE: &str = "v1.1.0";
    const BASE: &str = "v1.0.0";
    const OTHER: &str = "v0.9.0";
    const EXE: &str = "Decentraland.exe";

    fn tag(text: &str) -> Result<Tag> {
        text.parse()
    }

    fn path(text: &str) -> Result<RelPath> {
        Ok(RelPath::try_new(text.to_owned())?)
    }

    fn sha(digit: char) -> Result<Sha256> {
        Ok(Sha256::try_new(digit.to_string().repeat(64))?)
    }

    fn etag(text: &str) -> Result<ETag> {
        Ok(ETag::try_new(text.to_owned())?)
    }

    /// One file, `Decentraland.exe`, whose content is `digit`.
    fn tree(digit: char) -> Result<Tree> {
        let mut entries: BTreeMap<RelPath, Node> = BTreeMap::new();
        entries.insert(
            path(EXE)?,
            Node::File {
                size: 1,
                sha256: sha(digit)?,
                mode: Mode::try_new("644".to_owned())?,
            },
        );
        Tree::new(entries)
    }

    fn build(root: &str, digit: char) -> Result<Build> {
        Ok(Build {
            root: PathBuf::from(root),
            tree: tree(digit)?,
        })
    }

    fn signatures(valid: bool) -> Result<Signatures> {
        let mut signatures: Signatures = Signatures::new();
        signatures.insert(path(EXE)?, valid);
        Ok(signatures)
    }

    fn delta(base: &str) -> Result<Delta> {
        let whole: Whole = Whole {
            path: path(EXE)?,
            file: path(&format!("from/{base}/{EXE}.hdiff"))?,
            size: 3,
            sha256: sha('9')?,
        };
        // A deleted path, so the replay actions carry a `Delete` through serde.
        Delta::new(vec![path("gone.txt")?], vec![], vec![whole])
    }

    fn start(bases: &[&str]) -> Result<(Run, Vec<Cmd>)> {
        let release: Release = Release {
            tag: tag(RELEASE)?,
            platform: Platform::Windows64,
            zip: ZipName::try_new("Decentraland_windows64.zip".to_owned())?,
            bucket_prefix: "releases".to_owned(),
            work: PathBuf::from("work"),
        };
        let mut tags: Vec<Tag> = Vec::new();
        for base in bases {
            tags.push(tag(base)?);
        }
        Ok(init(Request::new(release, tags)?))
    }

    /// Sends a fact and expects exactly one cmd back.
    fn send(run: Run, msg: Msg) -> Result<(Run, Cmd)> {
        let (run, mut cmds): (Run, Vec<Cmd>) = update(run, msg);
        ensure!(cmds.len() == 1, "{} cmds after {}", cmds.len(), run.state);
        let cmd: Cmd = cmds.pop().context("no cmd")?;
        Ok((run, cmd))
    }

    fn failure(run: &Run) -> Option<&str> {
        match &run.state {
            State::Failed { reason } => Some(reason.as_str()),
            _ => None,
        }
    }

    fn downloaded(bases: &[&str], missing: &[(&str, &str)]) -> Result<Msg> {
        let mut found: Vec<(Tag, PathBuf)> = Vec::new();
        for base in bases {
            found.push((tag(base)?, PathBuf::from(format!("work/bases/{base}.zip"))));
        }
        let mut lost: Vec<(Tag, String)> = Vec::new();
        for (base, reason) in missing {
            lost.push((tag(base)?, (*reason).to_owned()));
        }
        Ok(Msg::ArchivesDownloaded {
            zip_etag: etag("e1")?,
            zip_size: 10,
            zip_sha256: sha('a')?,
            bases: found,
            missing: lost,
        })
    }

    fn extracted(bases: &[&str]) -> Result<Msg> {
        let mut found: Vec<(Tag, PathBuf)> = Vec::new();
        for base in bases {
            found.push((tag(base)?, PathBuf::from(format!("work/bases/{base}"))));
        }
        Ok(Msg::ArchivesExtracted {
            release_root: PathBuf::from("work/release"),
            bases: found,
            unreadable: vec![],
        })
    }

    fn scanned(bases: &[&str]) -> Result<Msg> {
        let mut found: Vec<(Tag, Build)> = Vec::new();
        for base in bases {
            found.push((tag(base)?, build(&format!("work/bases/{base}"), '2')?));
        }
        Ok(Msg::BuildsScanned {
            release: build("work/release", '1')?,
            signatures: signatures(true)?,
            bases: found,
            unreadable: vec![],
        })
    }

    fn encoded(bases: &[&str]) -> Result<Msg> {
        let mut found: Vec<(Tag, Delta)> = Vec::new();
        for base in bases {
            found.push((tag(base)?, delta(base)?));
        }
        Ok(Msg::PatchesEncoded {
            encoded: found,
            failed: vec![],
        })
    }

    fn replayed(base: &str, digit: char, valid: bool) -> Result<Msg> {
        Ok(Msg::PatchesReplayed {
            rebuilt: vec![(
                tag(base)?,
                Rebuilt {
                    tree: tree(digit)?,
                    signatures: signatures(valid)?,
                },
            )],
            failed: vec![],
        })
    }

    fn rechecked(published: bool, tag_text: &str) -> Result<Msg> {
        Ok(Msg::ReleaseRechecked {
            published,
            zip_etag: etag(tag_text)?,
        })
    }

    /// The facts of the happy path, in order.
    fn happy_path() -> Result<Vec<Msg>> {
        Ok(vec![
            rechecked(false, "e1")?,
            Msg::StalePatchesRemoved,
            downloaded(&[BASE], &[])?,
            extracted(&[BASE])?,
            scanned(&[BASE])?,
            encoded(&[BASE])?,
            replayed(BASE, '1', true)?,
            Msg::UploadFolderPrepared,
            rechecked(false, "e1")?,
            Msg::PatchesUploaded,
            rechecked(false, "e1")?,
            Msg::ManifestPublished,
        ])
    }

    /// A run with one base, taken to `state_name` along the happy path, with the cmd it waits on.
    fn run_at(state_name: &str) -> Result<(Run, Cmd)> {
        let (mut run, mut cmds): (Run, Vec<Cmd>) = start(&[BASE])?;
        for msg in happy_path()? {
            if run.state.name() == state_name {
                break;
            }
            let (next, cmd): (Run, Cmd) = send(run, msg)?;
            ensure!(failure(&next).is_none(), "{:?}", failure(&next));
            run = next;
            cmds = vec![cmd];
        }
        ensure!(run.state.name() == state_name, "never reached {state_name}");
        let cmd: Cmd = cmds.pop().context("no cmd")?;
        Ok((run, cmd))
    }

    /// Changing the encoder changes the patch bytes under the same key: bump
    /// `ENCODING_GENERATION`, then this pin.
    #[test]
    fn encoding_generation_pins_the_encoder() -> Result<()> {
        ensure!(
            (ENCODING_GENERATION, VERSION, HDIFFZ_ARGS) == (1, "v5.1.3", ["-m-6", "-c-lzma2-9-16m"]),
            "the encoder changed: bump ENCODING_GENERATION and this pin"
        );
        Ok(())
    }

    #[test]
    fn init_requests_the_run_and_asks_whether_the_release_is_published() -> Result<()> {
        let (run, cmds): (Run, Vec<Cmd>) = start(&[BASE])?;
        ensure!(run.state.name() == "Requested");
        ensure!(
            cmds == vec![Cmd::RecheckRelease {
                tag: tag(RELEASE)?,
                zip_key: "releases/v1.1.0/Decentraland_windows64.zip".to_owned()
            }]
        );
        Ok(())
    }

    #[test]
    fn a_published_release_is_refused_before_anything_is_removed() -> Result<()> {
        let (run, _): (Run, Vec<Cmd>) = start(&[BASE])?;
        let (run, cmd): (Run, Cmd) = send(run, rechecked(true, "e1")?)?;
        ensure!(cmd == Cmd::Complete);
        ensure!(failure(&run) == Some("v1.1.0 is published; its delta patches are immutable"));
        Ok(())
    }

    #[test]
    fn the_lifecycle_runs_from_requested_to_verified() -> Result<()> {
        let (run, _): (Run, Vec<Cmd>) = start(&[BASE])?;

        let (run, cmd): (Run, Cmd) = send(run, rechecked(false, "e1")?)?;
        ensure!(run.state.name() == "Unpublished");
        ensure!(
            cmd == Cmd::RemoveStalePatches {
                prefix: "releases/v1.1.0/delta/windows64/".to_owned()
            }
        );

        let (run, cmd): (Run, Cmd) = send(run, Msg::StalePatchesRemoved)?;
        ensure!(run.state.name() == "Cleared");
        let Cmd::DownloadArchives { release, bases } = cmd else {
            bail!("not a download: {cmd}")
        };
        ensure!(release.key == "releases/v1.1.0/Decentraland_windows64.zip");
        ensure!(release.to == PathBuf::from("work").join("release.zip"));
        ensure!(bases.first().map(|(_, f)| f.key.as_str()) == Some("releases/v1.0.0/Decentraland_windows64.zip"));

        let (run, cmd): (Run, Cmd) = send(run, downloaded(&[BASE], &[])?)?;
        let State::Downloaded(archives) = &run.state else {
            bail!("not downloaded: {:?}", failure(&run))
        };
        ensure!(archives.zip.etag == etag("e1")?);
        ensure!(archives.zip.zip.size == 10 && archives.zip.zip.name.to_string() == "Decentraland_windows64.zip");
        let Cmd::ExtractArchives { release, bases } = cmd else {
            bail!("not an extraction: {cmd}")
        };
        ensure!(release.into == PathBuf::from("work").join("release"));
        ensure!(bases.first().map(|(_, u)| u.into.clone()) == Some(PathBuf::from("work/bases").join(BASE)));

        let (run, cmd): (Run, Cmd) = send(run, extracted(&[BASE])?)?;
        ensure!(run.state.name() == "Extracted");
        ensure!(matches!(
            cmd,
            Cmd::ScanBuilds {
                platform: Platform::Windows64,
                ..
            }
        ));

        let (run, cmd): (Run, Cmd) = send(run, scanned(&[BASE])?)?;
        ensure!(run.state.name() == "Scanned");
        let Cmd::EncodePatches {
            staging,
            build_id,
            bases,
            ..
        } = cmd
        else {
            bail!("not an encoding: {cmd}")
        };
        ensure!(staging == PathBuf::from("work").join("staging"));
        ensure!(
            build_id == "aaaaaaaa-e1",
            "the first hex digits of the zip's sha256 and the encoding generation: {build_id}"
        );
        let jobs: Vec<Job> = bases.first().map(|(_, _, plan)| plan.jobs.clone()).unwrap_or_default();
        ensure!(
            jobs == vec![Job::Diff {
                path: path(EXE)?,
                from: path(EXE)?
            }],
            "a changed file is planned as a diff: {jobs:?}"
        );

        let (run, cmd): (Run, Cmd) = send(run, encoded(&[BASE])?)?;
        ensure!(run.state.name() == "Encoded");
        let Cmd::ReplayPatches { artifacts, bases, .. } = cmd else {
            bail!("not a replay: {cmd}")
        };
        ensure!(artifacts == vec![path(EXE)?]);
        ensure!(bases.first().is_some_and(|(_, r)| !r.actions.is_empty()));

        let (run, cmd): (Run, Cmd) = send(run, replayed(BASE, '1', true)?)?;
        let State::Verified(upload) = &run.state else {
            bail!("not verified: {:?}", failure(&run))
        };
        ensure!(upload.manifest.deltas().keys().count() == 1);
        let Cmd::PrepareUploadFolder {
            manifest_json, moves, ..
        } = cmd
        else {
            bail!("not a preparation: {cmd}")
        };
        ensure!(manifest_json.contains("\"version\": \"v1.1.0\""));
        ensure!(moves.len() == 1);
        Ok(())
    }

    #[test]
    fn the_lifecycle_runs_from_verified_to_published() -> Result<()> {
        let (run, _): (Run, Cmd) = run_at("Verified")?;

        let (run, cmd): (Run, Cmd) = send(run, Msg::UploadFolderPrepared)?;
        ensure!(run.state.name() == "Prepared");
        ensure!(matches!(cmd, Cmd::RecheckRelease { .. }));

        let (run, cmd): (Run, Cmd) = send(run, rechecked(false, "e1")?)?;
        ensure!(run.state.name() == "Rechecked");
        ensure!(matches!(cmd, Cmd::UploadPatches { ref prefix, .. } if prefix == "releases/v1.1.0/delta/windows64"));

        let (run, cmd): (Run, Cmd) = send(run, Msg::PatchesUploaded)?;
        ensure!(run.state.name() == "PatchesUploaded");
        ensure!(
            matches!(cmd, Cmd::RecheckRelease { .. }),
            "rechecked again before the manifest"
        );

        let (run, cmd): (Run, Cmd) = send(run, rechecked(false, "e1")?)?;
        ensure!(run.state.name() == "ReadyToPublish");
        ensure!(matches!(cmd, Cmd::PublishManifest { .. }));

        let (run, cmd): (Run, Cmd) = send(run, Msg::ManifestPublished)?;
        ensure!(run.state.name() == "Published");
        ensure!(cmd == Cmd::Complete);
        ensure!(run.dropped.is_empty());

        let (finished, cmd): (Run, Cmd) = send(run, Msg::StalePatchesRemoved)?;
        ensure!(
            failure(&finished).is_some() && cmd == Cmd::Complete,
            "a fact after the end fails"
        );
        Ok(())
    }

    #[test]
    fn the_run_and_its_cmd_round_trip_through_json_at_every_state() -> Result<()> {
        for name in State::NAMES.iter().filter(|n: &&&str| **n != "Failed") {
            let (run, cmd): (Run, Cmd) = run_at(name)?;
            let read_run: Run = serde_json::from_str(&serde_json::to_string(&run)?)?;
            let read_cmd: Cmd = serde_json::from_str(&serde_json::to_string(&cmd)?)?;
            ensure!(read_run == run && read_cmd == cmd, "{name}");
        }
        Ok(())
    }

    #[test]
    fn a_fact_for_another_state_fails_and_a_failed_run_stays_failed() -> Result<()> {
        let (run, _): (Run, Vec<Cmd>) = start(&[BASE])?;
        let (run, cmd): (Run, Cmd) = send(run, Msg::PatchesUploaded)?;
        ensure!(cmd == Cmd::Complete);
        let reason: String = failure(&run).context("should fail")?.to_owned();
        ensure!(reason == "'patches uploaded' does not fit a Requested run", "{reason}");

        let (again, cmd): (Run, Cmd) = send(run, Msg::StalePatchesRemoved)?;
        ensure!(cmd == Cmd::Complete);
        ensure!(failure(&again) == Some(reason.as_str()), "the first failure is kept");
        Ok(())
    }

    #[test]
    fn an_integrity_failure_fails_the_run_with_its_reason() -> Result<()> {
        let (run, _): (Run, Cmd) = run_at("Cleared")?;
        let (run, cmd): (Run, Cmd) = send(
            run,
            Msg::IntegrityFailure {
                reason: "cannot download the release zip: no object".to_owned(),
            },
        )?;
        ensure!(cmd == Cmd::Complete);
        ensure!(
            failure(&run) == Some("cannot download the release zip: no object (the run was Cleared)"),
            "{:?}",
            failure(&run)
        );
        Ok(())
    }

    #[test]
    fn a_published_release_or_a_replaced_zip_fails_both_rechecks() -> Result<()> {
        for state in ["Prepared", "PatchesUploaded"] {
            let (run, _): (Run, Cmd) = run_at(state)?;
            let (run, cmd): (Run, Cmd) = send(run, rechecked(true, "e1")?)?;
            ensure!(cmd == Cmd::Complete);
            ensure!(
                failure(&run).is_some_and(|r| r.contains("was published")),
                "{state}: {:?}",
                failure(&run)
            );

            let (run, _): (Run, Cmd) = run_at(state)?;
            let (run, _): (Run, Cmd) = send(run, rechecked(false, "e2")?)?;
            ensure!(
                failure(&run).is_some_and(|r| r.contains("changed in the bucket")),
                "{state}: {:?}",
                failure(&run)
            );
        }
        Ok(())
    }

    #[test]
    fn a_failing_base_is_dropped_and_the_others_continue() -> Result<()> {
        let (run, _): (Run, Vec<Cmd>) = start(&[BASE, OTHER])?;
        let (run, _): (Run, Cmd) = send(run, rechecked(false, "e1")?)?;
        let (run, cmd): (Run, Cmd) = send(run, Msg::StalePatchesRemoved)?;
        ensure!(matches!(cmd, Cmd::DownloadArchives { ref bases, .. } if bases.len() == 2));
        let (run, cmd): (Run, Cmd) = send(run, downloaded(&[BASE], &[(OTHER, "no object")])?)?;
        let State::Downloaded(archives) = &run.state else {
            bail!("not downloaded: {:?}", failure(&run))
        };
        ensure!(archives.bases.keys().count() == 1 && archives.bases.contains(&tag(BASE)?));
        ensure!(matches!(cmd, Cmd::ExtractArchives { ref bases, .. } if bases.len() == 1));
        ensure!(
            run.dropped
                == vec![Dropped {
                    base: tag(OTHER)?,
                    reason: DropReason::ZipMissing("no object".to_owned()),
                }]
        );
        Ok(())
    }

    #[test]
    fn a_base_listed_nowhere_is_dropped() -> Result<()> {
        let (run, _): (Run, Cmd) = run_at("Scanned")?;
        let (run, _): (Run, Cmd) = send(
            run,
            Msg::PatchesEncoded {
                encoded: vec![],
                failed: vec![],
            },
        )?;
        let reason: &str = failure(&run).context("no base left")?;
        ensure!(reason.contains("no result reported"), "{reason}");
        ensure!(matches!(
            run.dropped.first().map(|d| &d.reason),
            Some(DropReason::EncodeFailed(_))
        ));
        Ok(())
    }

    #[test]
    fn every_base_dropped_fails_the_run_naming_each_reason() -> Result<()> {
        let (run, _): (Run, Cmd) = run_at("Cleared")?;
        let (run, cmd): (Run, Cmd) = send(run, downloaded(&[], &[(BASE, "no object")])?)?;
        ensure!(cmd == Cmd::Complete);
        ensure!(failure(&run) == Some("no base left (v1.0.0: zip missing: no object)"));
        Ok(())
    }

    #[test]
    fn a_rebuild_that_differs_or_fails_or_changes_signatures_drops_the_base() -> Result<()> {
        let failed_replay: Msg = Msg::PatchesReplayed {
            rebuilt: vec![],
            failed: vec![(tag(BASE)?, "hpatchz failed".to_owned())],
        };
        let cases: [(Msg, &str); 3] = [
            (replayed(BASE, '2', true)?, "rebuilt build differs"),
            (replayed(BASE, '1', false)?, "signatures changed"),
            (failed_replay, "rebuild failed"),
        ];
        for (msg, expected) in cases {
            let (run, _): (Run, Cmd) = run_at("Encoded")?;
            let (run, _): (Run, Cmd) = send(run, msg)?;
            let reason: &str = failure(&run).context(expected)?;
            ensure!(reason.contains(expected), "{reason}");
        }
        Ok(())
    }

    #[test]
    fn judge_accepts_an_exact_rebuild_and_names_what_differs() -> Result<()> {
        let valid: Signatures = signatures(true)?;
        ensure!(judge(&tree('1')?, &valid, &tree('1')?, &valid, true).is_none());
        ensure!(matches!(
            judge(&tree('1')?, &valid, &tree('2')?, &valid, true),
            Some(DropReason::RebuildDiffers(_))
        ));
        ensure!(matches!(
            judge(&tree('1')?, &valid, &tree('1')?, &signatures(false)?, true),
            Some(DropReason::SignaturesChanged(_))
        ));
        Ok(())
    }
}
