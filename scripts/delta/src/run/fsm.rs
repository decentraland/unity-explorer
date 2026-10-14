//! The state machine of a run: `update(run, msg) -> (run, command)`, pure.
//!
//! A YAML step sends a `Step`. `update` answers with the one `Command` that step needs, or fails
//! the run when the step does not fit the state. The executor runs the command and sends back a
//! `Report`; `update` folds it into the next state, dropping the bases that failed, or fails the
//! run. Every decision lives here: keys and paths, the patch plans, the replay order, the
//! verdict on a rebuilt build, the manifest, the recheck. Nothing here touches the file system,
//! processes or the environment.

use std::collections::BTreeMap;
use std::fmt;
use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};

use crate::builds::signatures::{Signatures, signature_changes};
use crate::builds::tree::{Build, Tree};
use crate::builds::values::{RelPath, Sha256};
use crate::patching::delta::Delta;
use crate::patching::plan::{Plan, plan};
use crate::patching::replay::{Action, Difference, differences, replay_actions};
use crate::publishing::contract::ManifestDto;
use crate::publishing::layout::UploadLayout;
use crate::publishing::manifest::Manifest;
use crate::publishing::values::{ETag, ZipRef};
use crate::releases::bases::Bases;
use crate::releases::tag::{Platform, Tag};
use crate::run::state::{
    Archives, Builds, Candidate, Candidates, DropReason, Dropped, Folders, Request, Run, State, TargetZip, Upload,
};

/// What a YAML step sends, in lifecycle order.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, clap::ValueEnum)]
#[serde(rename_all = "kebab-case")]
pub enum Step {
    ConfirmUnpublished,
    RemoveStalePatches,
    DownloadArchives,
    ExtractArchives,
    ScanBuilds,
    EncodePatches,
    ReplayPatches,
    WriteManifest,
    RecheckTarget,
    UploadPatches,
    PublishManifest,
}

impl fmt::Display for Step {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let name: &str = match self {
            Step::ConfirmUnpublished => "confirm-unpublished",
            Step::RemoveStalePatches => "remove-stale-patches",
            Step::DownloadArchives => "download-archives",
            Step::ExtractArchives => "extract-archives",
            Step::ScanBuilds => "scan-builds",
            Step::EncodePatches => "encode-patches",
            Step::ReplayPatches => "replay-patches",
            Step::WriteManifest => "write-manifest",
            Step::RecheckTarget => "recheck-target",
            Step::UploadPatches => "upload-patches",
            Step::PublishManifest => "publish-manifest",
        };
        f.write_str(name)
    }
}

/// An object to download: the bucket key and the local path.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Fetch {
    pub key: String,
    pub to: PathBuf,
}

/// An archive to extract into an empty folder.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Unpack {
    pub archive: PathBuf,
    pub into: PathBuf,
}

/// A patch set to apply to a copy of its base build, in the launcher's order.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Replay {
    pub base: PathBuf,
    pub actions: Vec<Action>,
    pub rebuild: PathBuf,
}

/// The one I/O a step needs. Each variant says what to do, never why.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Command {
    LookupRelease {
        tag: Tag,
    },
    RemovePrefix {
        prefix: String,
    },
    DownloadArchives {
        target: Fetch,
        bases: Vec<(Tag, Fetch)>,
    },
    /// Extraction consumes each archive.
    ExtractArchives {
        target: Unpack,
        bases: Vec<(Tag, Unpack)>,
    },
    ScanBuilds {
        platform: Platform,
        target: PathBuf,
        bases: Vec<(Tag, PathBuf)>,
    },
    /// Patches go to `staging/from/<base>/<path>.hdiff`. A base that fails leaves nothing there.
    EncodePatches {
        staging: PathBuf,
        target: Build,
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
    WriteUpload {
        manifest_json: String,
        layout: UploadLayout,
        moves: Vec<(PathBuf, PathBuf)>,
    },
    LookupTarget {
        tag: Tag,
        zip_key: String,
    },
    /// Immutable: a patch never changes once uploaded.
    UploadPatches {
        folder: PathBuf,
        prefix: String,
    },
    /// Revalidated: a rebuild of this unpublished tag rewrites the manifest.
    PublishManifest {
        folder: PathBuf,
        prefix: String,
    },
}

/// The target zip as downloaded, and each base's download.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Downloads {
    pub etag: ETag,
    pub size: u64,
    pub sha256: Sha256,
    pub bases: Vec<(Tag, Result<PathBuf, String>)>,
}

/// The target build root, and each base's.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Extractions {
    pub root: PathBuf,
    pub bases: Vec<(Tag, Result<PathBuf, String>)>,
}

/// The target build with its signatures, and each base build.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Scans {
    pub build: Build,
    pub signatures: Signatures,
    pub bases: Vec<(Tag, Result<Build, String>)>,
}

/// A rebuilt build as the launcher would produce it.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Rebuilt {
    pub tree: Tree,
    pub signatures: Signatures,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Lookup {
    pub published: bool,
    pub etag: ETag,
}

/// What the executor saw. An `Err` at the top is a run-level failure; an `Err` for one base is
/// that base's failure.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Report {
    ReleaseLookedUp(Result<bool, String>),
    PrefixRemoved(Result<(), String>),
    ArchivesDownloaded(Result<Downloads, String>),
    ArchivesExtracted(Result<Extractions, String>),
    BuildsScanned(Result<Scans, String>),
    PatchesEncoded(Vec<(Tag, Result<Delta, String>)>),
    PatchesReplayed(Vec<(Tag, Result<Rebuilt, String>)>),
    UploadWritten(Result<(), String>),
    TargetLookedUp(Result<Lookup, String>),
    PatchesUploaded(Result<(), String>),
    ManifestPublished(Result<(), String>),
}

impl Report {
    /// The step this report answers.
    pub fn step(&self) -> Step {
        match self {
            Report::ReleaseLookedUp(_) => Step::ConfirmUnpublished,
            Report::PrefixRemoved(_) => Step::RemoveStalePatches,
            Report::ArchivesDownloaded(_) => Step::DownloadArchives,
            Report::ArchivesExtracted(_) => Step::ExtractArchives,
            Report::BuildsScanned(_) => Step::ScanBuilds,
            Report::PatchesEncoded(_) => Step::EncodePatches,
            Report::PatchesReplayed(_) => Step::ReplayPatches,
            Report::UploadWritten(_) => Step::WriteManifest,
            Report::TargetLookedUp(_) => Step::RecheckTarget,
            Report::PatchesUploaded(_) => Step::UploadPatches,
            Report::ManifestPublished(_) => Step::PublishManifest,
        }
    }

    /// What happened, one line per item, for the log.
    pub fn lines(&self) -> Vec<String> {
        match self {
            Report::ReleaseLookedUp(Ok(true)) => vec!["the release is published".to_owned()],
            Report::ReleaseLookedUp(Ok(false)) => vec!["the release is not published".to_owned()],
            Report::PrefixRemoved(Ok(())) => vec!["earlier patches removed".to_owned()],
            Report::ArchivesDownloaded(Ok(downloads)) => {
                let mut lines: Vec<String> =
                    vec![format!("target zip: {} bytes, ETag {}", downloads.size, downloads.etag)];
                for (base, result) in &downloads.bases {
                    lines.push(match result {
                        Ok(_) => format!("{base}: downloaded"),
                        Err(reason) => format!("{base}: {reason}"),
                    });
                }
                lines
            }
            Report::ArchivesExtracted(Ok(extractions)) => {
                let mut lines: Vec<String> = vec![format!("target extracted to {}", extractions.root.display())];
                for (base, result) in &extractions.bases {
                    lines.push(match result {
                        Ok(_) => format!("{base}: extracted"),
                        Err(reason) => format!("{base}: {reason}"),
                    });
                }
                lines
            }
            Report::BuildsScanned(Ok(scans)) => {
                let mut lines: Vec<String> = vec![format!(
                    "target build: {} entries, {} signed artifacts",
                    scans.build.tree.entries().len(),
                    scans.signatures.len()
                )];
                for (base, result) in &scans.bases {
                    lines.push(match result {
                        Ok(build) => format!("{base}: {} entries", build.tree.entries().len()),
                        Err(reason) => format!("{base}: {reason}"),
                    });
                }
                lines
            }
            Report::PatchesEncoded(bases) => {
                let mut lines: Vec<String> = Vec::new();
                for (base, result) in bases {
                    lines.push(match result {
                        Ok(delta) => format!(
                            "{base}: {} patched, {} whole, {} deleted, {} bytes to download",
                            delta.patches().len(),
                            delta.whole().len(),
                            delta.deleted().len(),
                            delta.download_size()
                        ),
                        Err(reason) => format!("{base}: {reason}"),
                    });
                }
                lines
            }
            Report::PatchesReplayed(bases) => {
                let mut lines: Vec<String> = Vec::new();
                for (base, result) in bases {
                    lines.push(match result {
                        Ok(_) => format!("{base}: replayed"),
                        Err(reason) => format!("{base}: {reason}"),
                    });
                }
                lines
            }
            Report::UploadWritten(Ok(())) => vec!["upload folders written".to_owned()],
            Report::TargetLookedUp(Ok(lookup)) => vec![format!(
                "the release is {}, zip ETag {}",
                if lookup.published { "published" } else { "not published" },
                lookup.etag
            )],
            Report::PatchesUploaded(Ok(())) => vec!["patches uploaded".to_owned()],
            Report::ManifestPublished(Ok(())) => vec!["manifest published".to_owned()],
            Report::ReleaseLookedUp(Err(reason))
            | Report::PrefixRemoved(Err(reason))
            | Report::ArchivesDownloaded(Err(reason))
            | Report::ArchivesExtracted(Err(reason))
            | Report::BuildsScanned(Err(reason))
            | Report::UploadWritten(Err(reason))
            | Report::TargetLookedUp(Err(reason))
            | Report::PatchesUploaded(Err(reason))
            | Report::ManifestPublished(Err(reason)) => vec![reason.clone()],
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Msg {
    Step(Step),
    Report(Report),
}

/// Runs one message through the machine. A failed run ignores every message and keeps the
/// reason it failed.
pub fn update(run: Run, msg: Msg) -> (Run, Option<Command>) {
    let Run {
        schema,
        state,
        mut dropped,
    } = run;
    let (state, command): (State, Option<Command>) = match (state, msg) {
        (State::Failed { at, reason }, _) => (State::Failed { at, reason }, None),
        (state, Msg::Step(step)) => decide(state, step),
        (state, Msg::Report(report)) => (apply(state, report, &mut dropped), None),
    };
    (Run { schema, state, dropped }, command)
}

/// The command a step needs in the current state. The state does not change: the step has not
/// done anything yet.
#[allow(clippy::too_many_lines)] // one arm per step: the table reads best whole
fn decide(state: State, step: Step) -> (State, Option<Command>) {
    match (step, state) {
        (Step::ConfirmUnpublished, State::Idle(request)) => {
            let command: Command = Command::LookupRelease {
                tag: request.target.tag.clone(),
            };
            (State::Idle(request), Some(command))
        }
        (Step::RemoveStalePatches, State::Unpublished(request)) => {
            let command: Command = Command::RemovePrefix {
                prefix: format!("{}/", request.target.prefix()),
            };
            (State::Unpublished(request), Some(command))
        }
        (Step::DownloadArchives, State::Cleared(request)) => {
            let target = &request.target;
            let command: Command = Command::DownloadArchives {
                target: Fetch {
                    key: target.zip_key(),
                    to: target.archive(),
                },
                bases: request
                    .bases
                    .iter()
                    .map(|base: &Tag| {
                        let fetch: Fetch = Fetch {
                            key: target.base_zip_key(base),
                            to: target.base_archive(base),
                        };
                        (base.clone(), fetch)
                    })
                    .collect(),
            };
            (State::Cleared(request), Some(command))
        }
        (Step::ExtractArchives, State::Downloaded(archives)) => {
            let target = &archives.target;
            let command: Command = Command::ExtractArchives {
                target: Unpack {
                    archive: target.archive(),
                    into: target.build_dir(),
                },
                bases: archives
                    .bases
                    .iter()
                    .map(|(base, archive): (&Tag, &PathBuf)| {
                        let unpack: Unpack = Unpack {
                            archive: archive.clone(),
                            into: target.base_dir(base),
                        };
                        (base.clone(), unpack)
                    })
                    .collect(),
            };
            (State::Downloaded(archives), Some(command))
        }
        (Step::ScanBuilds, State::Extracted(folders)) => {
            let command: Command = Command::ScanBuilds {
                platform: folders.target.platform,
                target: folders.root.clone(),
                bases: folders
                    .bases
                    .iter()
                    .map(|(base, root): (&Tag, &PathBuf)| (base.clone(), root.clone()))
                    .collect(),
            };
            (State::Extracted(folders), Some(command))
        }
        (Step::EncodePatches, State::Scanned(builds)) => {
            let command: Command = Command::EncodePatches {
                staging: builds.target.staging(),
                target: builds.build.clone(),
                bases: builds
                    .bases
                    .iter()
                    .map(|(base, build): (&Tag, &Build)| {
                        (base.clone(), build.clone(), plan(&build.tree, &builds.build.tree))
                    })
                    .collect(),
            };
            (State::Scanned(builds), Some(command))
        }
        (Step::ReplayPatches, State::Encoded(candidates)) => {
            let target = &candidates.target;
            let command: Command = Command::ReplayPatches {
                platform: target.platform,
                staging: target.staging(),
                artifacts: candidates.signatures.keys().cloned().collect(),
                bases: candidates
                    .bases
                    .iter()
                    .map(|(base, candidate): (&Tag, &Candidate)| {
                        let replay: Replay = Replay {
                            base: candidate.build.root.clone(),
                            actions: replay_actions(&candidate.delta, &candidates.build.tree),
                            rebuild: target.rebuild_dir(base),
                        };
                        (base.clone(), replay)
                    })
                    .collect(),
            };
            (State::Encoded(candidates), Some(command))
        }
        (Step::WriteManifest, State::Verified(upload)) => match manifest_json(&upload.manifest) {
            Ok(manifest_json) => {
                let staging: PathBuf = upload.target.staging();
                let moves: Vec<(PathBuf, PathBuf)> = upload
                    .manifest
                    .deltas()
                    .keys()
                    .map(|base: &Tag| {
                        let folder: PathBuf = Path::new("from").join(base.to_string());
                        (staging.join(&folder), upload.layout.patches.join(&folder))
                    })
                    .collect();
                let command: Command = Command::WriteUpload {
                    manifest_json,
                    layout: upload.layout.clone(),
                    moves,
                };
                (State::Verified(upload), Some(command))
            }
            Err(reason) => (State::Failed { at: step, reason }, None),
        },
        (Step::RecheckTarget, State::Assembled(upload)) => {
            let command: Command = Command::LookupTarget {
                tag: upload.target.tag.clone(),
                zip_key: upload.target.zip_key(),
            };
            (State::Assembled(upload), Some(command))
        }
        (Step::UploadPatches, State::Rechecked(upload)) => {
            let command: Command = Command::UploadPatches {
                folder: upload.layout.patches.clone(),
                prefix: upload.layout.prefix.clone(),
            };
            (State::Rechecked(upload), Some(command))
        }
        (Step::PublishManifest, State::PatchesUploaded(upload)) => {
            let command: Command = Command::PublishManifest {
                folder: upload.layout.manifest.clone(),
                prefix: upload.layout.prefix.clone(),
            };
            (State::PatchesUploaded(upload), Some(command))
        }
        (step, state) => {
            let reason: String = format!("{step} cannot run on a {state} run");
            (State::Failed { at: step, reason }, None)
        }
    }
}

/// The next state after a report; a reason when the report ends the run.
fn apply(state: State, report: Report, dropped: &mut Vec<Dropped>) -> State {
    let at: Step = report.step();
    match transition(state, report, dropped) {
        Ok(state) => state,
        Err(reason) => State::Failed { at, reason },
    }
}

/// Each arm names the state a report moves on from.
#[allow(clippy::too_many_lines)] // one arm per report: the table reads best whole
fn transition(state: State, report: Report, dropped: &mut Vec<Dropped>) -> Result<State, String> {
    match (state, report) {
        (State::Idle(request), Report::ReleaseLookedUp(published)) => {
            if published? {
                Err(format!(
                    "{} is published; its delta patches are immutable",
                    request.target.tag
                ))
            } else {
                Ok(State::Unpublished(request))
            }
        }
        (State::Unpublished(request), Report::PrefixRemoved(removed)) => {
            removed?;
            Ok(State::Cleared(request))
        }
        (State::Cleared(request), Report::ArchivesDownloaded(downloads)) => {
            let Downloads {
                etag,
                size,
                sha256,
                bases,
            } = downloads?;
            let Request { target, bases: held } = request;
            let held: Vec<(Tag, ())> = held.into_iter().map(|base: Tag| (base, ())).collect();
            let kept: BTreeMap<Tag, PathBuf> = settle(held, bases, DropReason::ZipMissing, dropped)
                .into_iter()
                .map(|(base, ((), path)): (Tag, ((), PathBuf))| (base, path))
                .collect();
            let zip: TargetZip = TargetZip {
                zip: ZipRef {
                    name: target.zip.clone(),
                    size,
                    sha256,
                },
                etag,
            };
            Ok(State::Downloaded(Archives {
                target,
                zip,
                bases: remaining(kept, dropped)?,
            }))
        }
        (State::Downloaded(archives), Report::ArchivesExtracted(extractions)) => {
            let Extractions { root, bases } = extractions?;
            let Archives {
                target,
                zip,
                bases: held,
            } = archives;
            let kept: BTreeMap<Tag, PathBuf> = settle(held, bases, DropReason::Unreadable, dropped)
                .into_iter()
                .map(|(base, (_, base_root)): (Tag, (PathBuf, PathBuf))| (base, base_root))
                .collect();
            Ok(State::Extracted(Folders {
                target,
                zip,
                root,
                bases: remaining(kept, dropped)?,
            }))
        }
        (State::Extracted(folders), Report::BuildsScanned(scans)) => {
            let Scans {
                build,
                signatures,
                bases,
            } = scans?;
            let Folders {
                target,
                zip,
                bases: held,
                ..
            } = folders;
            let kept: BTreeMap<Tag, Build> = settle(held, bases, DropReason::Unreadable, dropped)
                .into_iter()
                .map(|(base, (_, base_build)): (Tag, (PathBuf, Build))| (base, base_build))
                .collect();
            Ok(State::Scanned(Builds {
                target,
                zip,
                build,
                signatures,
                bases: remaining(kept, dropped)?,
            }))
        }
        (State::Scanned(builds), Report::PatchesEncoded(results)) => {
            let Builds {
                target,
                zip,
                build,
                signatures,
                bases: held,
            } = builds;
            let kept: BTreeMap<Tag, Candidate> = settle(held, results, DropReason::EncodeFailed, dropped)
                .into_iter()
                .map(|(base, (base_build, delta)): (Tag, (Build, Delta))| {
                    let candidate: Candidate = Candidate {
                        build: base_build,
                        delta,
                    };
                    (base, candidate)
                })
                .collect();
            Ok(State::Encoded(Candidates {
                target,
                zip,
                build,
                signatures,
                bases: remaining(kept, dropped)?,
            }))
        }
        (State::Encoded(candidates), Report::PatchesReplayed(results)) => {
            let Candidates {
                target,
                zip,
                build,
                signatures,
                bases: held,
            } = candidates;
            let mut kept: BTreeMap<Tag, Delta> = BTreeMap::new();
            for (base, (candidate, rebuilt)) in settle(held, results, DropReason::RebuildFailed, dropped) {
                match judge(&build.tree, &signatures, &rebuilt.tree, &rebuilt.signatures, cfg!(unix)) {
                    None => {
                        kept.insert(base, candidate.delta);
                    }
                    Some(reason) => drop_base(base, reason, dropped),
                }
            }
            let deltas: Bases<Delta> = remaining(kept, dropped)?;
            let manifest: Manifest =
                Manifest::new(target.tag.clone(), target.platform, zip.zip.clone(), build.tree, deltas)
                    .map_err(|error: anyhow::Error| format!("{error:#}"))?;
            let layout: UploadLayout = target.upload();
            Ok(State::Verified(Upload {
                target,
                zip,
                manifest,
                layout,
            }))
        }
        (State::Verified(upload), Report::UploadWritten(written)) => {
            written?;
            Ok(State::Assembled(upload))
        }
        (State::Assembled(upload), Report::TargetLookedUp(lookup)) => {
            let Lookup { published, etag } = lookup?;
            if published {
                Err(format!(
                    "{} was published while its patches were made",
                    upload.target.tag
                ))
            } else if etag != upload.zip.etag {
                Err(format!(
                    "{} changed in the bucket during the run (ETag {} -> {etag})",
                    upload.target.zip, upload.zip.etag
                ))
            } else {
                Ok(State::Rechecked(upload))
            }
        }
        (State::Rechecked(upload), Report::PatchesUploaded(uploaded)) => {
            uploaded?;
            Ok(State::PatchesUploaded(upload))
        }
        (State::PatchesUploaded(upload), Report::ManifestPublished(published)) => {
            published?;
            Ok(State::Published(upload.manifest))
        }
        (state, report) => Err(format!("a report for {} does not fit a {state} run", report.step())),
    }
}

fn manifest_json(manifest: &Manifest) -> Result<String, String> {
    serde_json::to_string_pretty(&ManifestDto::from(manifest)).map_err(|error: serde_json::Error| error.to_string())
}

/// Whether a rebuilt build is the target build: same entries, and every signed artifact
/// verifies the same way.
pub fn judge(
    target: &Tree,
    signatures: &Signatures,
    rebuilt: &Tree,
    rebuilt_signatures: &Signatures,
    compare_modes: bool,
) -> Option<DropReason> {
    let found: Vec<Difference> = differences(target, rebuilt, compare_modes);
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

/// Pairs each base the run holds with its result. A base whose result is an `Err`, or that has
/// no result at all, is dropped.
fn settle<H, R>(
    held: impl IntoIterator<Item = (Tag, H)>,
    results: Vec<(Tag, Result<R, String>)>,
    failed: fn(String) -> DropReason,
    dropped: &mut Vec<Dropped>,
) -> BTreeMap<Tag, (H, R)> {
    let mut results: BTreeMap<Tag, Result<R, String>> = results.into_iter().collect();
    let mut kept: BTreeMap<Tag, (H, R)> = BTreeMap::new();
    for (base, value) in held {
        match results.remove(&base) {
            Some(Ok(result)) => {
                kept.insert(base, (value, result));
            }
            Some(Err(reason)) => drop_base(base, failed(reason), dropped),
            None => drop_base(base, failed("no result reported".to_owned()), dropped),
        }
    }
    kept
}

/// The bases still in the run; an error naming every drop when none is left.
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

    use super::{Command, Downloads, Extractions, Lookup, Msg, Rebuilt, Report, Scans, Step, judge, update};
    use crate::builds::signatures::Signatures;
    use crate::builds::tree::{Build, Node, Tree};
    use crate::builds::values::{Mode, RelPath, Sha256};
    use crate::patching::delta::{Delta, Whole};
    use crate::patching::plan::Job;
    use crate::publishing::values::{ETag, ZipName};
    use crate::releases::tag::{Platform, Tag};
    use crate::run::state::{DropReason, Request, Run, State, Target};

    const TARGET: &str = "v1.1.0";
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
        Delta::new(vec![], vec![], vec![whole])
    }

    fn run(bases: &[&str]) -> Result<Run> {
        let target: Target = Target {
            tag: tag(TARGET)?,
            platform: Platform::Windows64,
            zip: ZipName::try_new("Decentraland_windows64.zip".to_owned())?,
            releases: "releases".to_owned(),
            work: PathBuf::from("work"),
        };
        let mut tags: Vec<Tag> = Vec::new();
        for base in bases {
            tags.push(tag(base)?);
        }
        Ok(Run::new(Request::new(target, tags)?))
    }

    /// Sends a step and expects an command back.
    fn send(run: Run, step: Step) -> Result<(Run, Command)> {
        let (run, command): (Run, Option<Command>) = update(run, Msg::Step(step));
        let command: Command = command.with_context(|| format!("{step} gave no command: {}", run.state))?;
        Ok((run, command))
    }

    fn answer(run: Run, report: Report) -> Run {
        update(run, Msg::Report(report)).0
    }

    fn failure(run: &Run) -> Option<(Step, &str)> {
        match &run.state {
            State::Failed { at, reason } => Some((*at, reason.as_str())),
            _ => None,
        }
    }

    fn ok_downloads(bases: &[&str]) -> Result<Report> {
        let mut results: Vec<(Tag, Result<PathBuf, String>)> = Vec::new();
        for base in bases {
            results.push((tag(base)?, Ok(PathBuf::from(format!("work/bases/{base}.zip")))));
        }
        Ok(Report::ArchivesDownloaded(Ok(Downloads {
            etag: etag("e1")?,
            size: 10,
            sha256: sha('a')?,
            bases: results,
        })))
    }

    fn ok_extractions(bases: &[&str]) -> Result<Report> {
        let mut results: Vec<(Tag, Result<PathBuf, String>)> = Vec::new();
        for base in bases {
            results.push((tag(base)?, Ok(PathBuf::from(format!("work/bases/{base}")))));
        }
        Ok(Report::ArchivesExtracted(Ok(Extractions {
            root: PathBuf::from("work/target"),
            bases: results,
        })))
    }

    fn ok_scans(bases: &[&str]) -> Result<Report> {
        let mut results: Vec<(Tag, Result<Build, String>)> = Vec::new();
        for base in bases {
            results.push((tag(base)?, Ok(build(&format!("work/bases/{base}"), '2')?)));
        }
        Ok(Report::BuildsScanned(Ok(Scans {
            build: build("work/target", '1')?,
            signatures: signatures(true)?,
            bases: results,
        })))
    }

    fn ok_encodings(bases: &[&str]) -> Result<Report> {
        let mut results: Vec<(Tag, Result<Delta, String>)> = Vec::new();
        for base in bases {
            results.push((tag(base)?, Ok(delta(base)?)));
        }
        Ok(Report::PatchesEncoded(results))
    }

    fn replayed(base: &str, digit: char, valid: bool) -> Result<(Tag, Result<Rebuilt, String>)> {
        Ok((
            tag(base)?,
            Ok(Rebuilt {
                tree: tree(digit)?,
                signatures: signatures(valid)?,
            }),
        ))
    }

    /// A run with one base, taken to `state_name` along the happy path.
    fn run_at(state_name: &str) -> Result<Run> {
        let mut run: Run = run(&[BASE])?;
        let path: [(Step, Report); 11] = [
            (Step::ConfirmUnpublished, Report::ReleaseLookedUp(Ok(false))),
            (Step::RemoveStalePatches, Report::PrefixRemoved(Ok(()))),
            (Step::DownloadArchives, ok_downloads(&[BASE])?),
            (Step::ExtractArchives, ok_extractions(&[BASE])?),
            (Step::ScanBuilds, ok_scans(&[BASE])?),
            (Step::EncodePatches, ok_encodings(&[BASE])?),
            (
                Step::ReplayPatches,
                Report::PatchesReplayed(vec![replayed(BASE, '1', true)?]),
            ),
            (Step::WriteManifest, Report::UploadWritten(Ok(()))),
            (
                Step::RecheckTarget,
                Report::TargetLookedUp(Ok(Lookup {
                    published: false,
                    etag: etag("e1")?,
                })),
            ),
            (Step::UploadPatches, Report::PatchesUploaded(Ok(()))),
            (Step::PublishManifest, Report::ManifestPublished(Ok(()))),
        ];
        for (step, report) in path {
            if run.state.name() == state_name {
                return Ok(run);
            }
            let (sent, _): (Run, Command) = send(run, step)?;
            run = answer(sent, report);
            ensure!(failure(&run).is_none(), "{step}: {:?}", failure(&run));
        }
        ensure!(run.state.name() == state_name, "never reached {state_name}");
        Ok(run)
    }

    #[test]
    fn the_lifecycle_runs_from_idle_to_verified() -> Result<()> {
        let run: Run = run(&[BASE])?;
        ensure!(run.state.name() == "Idle");

        let (run, command): (Run, Command) = send(run, Step::ConfirmUnpublished)?;
        ensure!(command == Command::LookupRelease { tag: tag(TARGET)? });
        let run: Run = answer(run, Report::ReleaseLookedUp(Ok(false)));
        ensure!(run.state.name() == "Unpublished");

        let (run, command): (Run, Command) = send(run, Step::RemoveStalePatches)?;
        ensure!(
            command
                == Command::RemovePrefix {
                    prefix: "releases/v1.1.0/delta/windows64/".to_owned()
                }
        );
        let run: Run = answer(run, Report::PrefixRemoved(Ok(())));
        ensure!(run.state.name() == "Cleared");

        let (run, command): (Run, Command) = send(run, Step::DownloadArchives)?;
        let Command::DownloadArchives { target, bases } = command else {
            bail!("not a download")
        };
        ensure!(target.key == "releases/v1.1.0/Decentraland_windows64.zip");
        ensure!(target.to == PathBuf::from("work").join("target.zip"));
        ensure!(bases.len() == 1);
        ensure!(bases.first().map(|(_, f)| f.key.as_str()) == Some("releases/v1.0.0/Decentraland_windows64.zip"));
        let run: Run = answer(run, ok_downloads(&[BASE])?);
        let State::Downloaded(archives) = &run.state else {
            bail!("not downloaded: {:?}", failure(&run))
        };
        ensure!(archives.zip.etag == etag("e1")?);
        ensure!(archives.zip.zip.size == 10 && archives.zip.zip.name.to_string() == "Decentraland_windows64.zip");

        let (run, command): (Run, Command) = send(run, Step::ExtractArchives)?;
        let Command::ExtractArchives { target, bases } = command else {
            bail!("not an extraction")
        };
        ensure!(target.into == PathBuf::from("work").join("target"));
        ensure!(bases.first().map(|(_, u)| u.into.clone()) == Some(PathBuf::from("work/bases").join(BASE)));
        let run: Run = answer(run, ok_extractions(&[BASE])?);
        ensure!(run.state.name() == "Extracted");

        let (run, command): (Run, Command) = send(run, Step::ScanBuilds)?;
        ensure!(matches!(
            command,
            Command::ScanBuilds {
                platform: Platform::Windows64,
                ..
            }
        ));
        let run: Run = answer(run, ok_scans(&[BASE])?);
        ensure!(run.state.name() == "Scanned");

        let (run, command): (Run, Command) = send(run, Step::EncodePatches)?;
        let Command::EncodePatches { staging, bases, .. } = command else {
            bail!("not an encoding")
        };
        ensure!(staging == PathBuf::from("work").join("staging"));
        let jobs: Vec<Job> = bases.first().map(|(_, _, plan)| plan.jobs.clone()).unwrap_or_default();
        ensure!(
            jobs == vec![Job::Diff {
                path: path(EXE)?,
                from: path(EXE)?
            }],
            "a changed file is planned as a diff: {jobs:?}"
        );
        let run: Run = answer(run, ok_encodings(&[BASE])?);
        ensure!(run.state.name() == "Encoded");

        let (run, command): (Run, Command) = send(run, Step::ReplayPatches)?;
        let Command::ReplayPatches { artifacts, bases, .. } = command else {
            bail!("not a replay")
        };
        ensure!(artifacts == vec![path(EXE)?]);
        ensure!(bases.first().is_some_and(|(_, r)| !r.actions.is_empty()));
        let run: Run = answer(run, Report::PatchesReplayed(vec![replayed(BASE, '1', true)?]));
        let State::Verified(upload) = &run.state else {
            bail!("not verified: {:?}", failure(&run))
        };
        ensure!(upload.manifest.deltas().keys().count() == 1);
        Ok(())
    }

    #[test]
    fn the_lifecycle_runs_from_verified_to_published() -> Result<()> {
        let run: Run = run_at("Verified")?;
        let (run, command): (Run, Command) = send(run, Step::WriteManifest)?;
        let Command::WriteUpload {
            manifest_json, moves, ..
        } = command
        else {
            bail!("not a write")
        };
        ensure!(manifest_json.contains("\"version\": \"v1.1.0\""));
        ensure!(moves.len() == 1);
        let run: Run = answer(run, Report::UploadWritten(Ok(())));
        ensure!(run.state.name() == "Assembled");

        let (run, command): (Run, Command) = send(run, Step::RecheckTarget)?;
        ensure!(matches!(command, Command::LookupTarget { .. }));
        let run: Run = answer(
            run,
            Report::TargetLookedUp(Ok(Lookup {
                published: false,
                etag: etag("e1")?,
            })),
        );
        ensure!(run.state.name() == "Rechecked");

        let (run, command): (Run, Command) = send(run, Step::UploadPatches)?;
        ensure!(
            matches!(command, Command::UploadPatches { ref prefix, .. } if prefix == "releases/v1.1.0/delta/windows64")
        );
        let run: Run = answer(run, Report::PatchesUploaded(Ok(())));
        ensure!(run.state.name() == "PatchesUploaded");

        let (run, command): (Run, Command) = send(run, Step::PublishManifest)?;
        ensure!(matches!(command, Command::PublishManifest { .. }));
        let run: Run = answer(run, Report::ManifestPublished(Ok(())));
        ensure!(run.state.name() == "Published");
        ensure!(run.dropped.is_empty());

        let (finished, command): (Run, Option<Command>) = update(run, Msg::Step(Step::ConfirmUnpublished));
        ensure!(
            command.is_none() && failure(&finished).is_some(),
            "a step on a finished run fails"
        );
        Ok(())
    }

    #[test]
    fn the_run_document_round_trips_at_every_state() -> Result<()> {
        for name in [
            "Idle",
            "Unpublished",
            "Cleared",
            "Downloaded",
            "Extracted",
            "Scanned",
            "Encoded",
            "Verified",
            "Assembled",
            "Rechecked",
            "PatchesUploaded",
            "Published",
        ] {
            let run: Run = run_at(name)?;
            let text: String = serde_json::to_string(&run)?;
            let read: Run = serde_json::from_str(&text)?;
            ensure!(read == run, "{name}");
        }
        Ok(())
    }

    #[test]
    fn a_step_out_of_order_fails_and_a_failed_run_stays_failed() -> Result<()> {
        let (run, command): (Run, Option<Command>) = update(run(&[BASE])?, Msg::Step(Step::EncodePatches));
        ensure!(command.is_none());
        let (at, reason): (Step, &str) = failure(&run).context("should fail")?;
        ensure!(at == Step::EncodePatches && reason == "encode-patches cannot run on a Idle run");
        let reason: String = reason.to_owned();

        let (again, command): (Run, Option<Command>) = update(run, Msg::Step(Step::ConfirmUnpublished));
        ensure!(command.is_none());
        ensure!(failure(&again) == Some((Step::EncodePatches, reason.as_str())));
        let again: Run = answer(again, Report::ReleaseLookedUp(Ok(false)));
        ensure!(failure(&again) == Some((Step::EncodePatches, reason.as_str())));
        Ok(())
    }

    #[test]
    fn a_report_for_another_state_fails() -> Result<()> {
        let run: Run = answer(run(&[BASE])?, Report::PrefixRemoved(Ok(())));
        let (at, reason): (Step, &str) = failure(&run).context("should fail")?;
        ensure!(at == Step::RemoveStalePatches && reason.contains("does not fit a Idle run"));
        Ok(())
    }

    #[test]
    fn a_published_target_fails_the_run() -> Result<()> {
        let (run, _): (Run, Command) = send(run(&[BASE])?, Step::ConfirmUnpublished)?;
        let run: Run = answer(run, Report::ReleaseLookedUp(Ok(true)));
        let (at, reason): (Step, &str) = failure(&run).context("should fail")?;
        ensure!(at == Step::ConfirmUnpublished && reason.contains("is published"));

        let (run, _): (Run, Command) = send(run_at("Assembled")?, Step::RecheckTarget)?;
        let run: Run = answer(
            run,
            Report::TargetLookedUp(Ok(Lookup {
                published: true,
                etag: etag("e1")?,
            })),
        );
        let (at, reason): (Step, &str) = failure(&run).context("should fail")?;
        ensure!(at == Step::RecheckTarget && reason.contains("was published"));
        Ok(())
    }

    #[test]
    fn a_zip_replaced_during_the_run_fails_the_recheck() -> Result<()> {
        let (run, _): (Run, Command) = send(run_at("Assembled")?, Step::RecheckTarget)?;
        let run: Run = answer(
            run,
            Report::TargetLookedUp(Ok(Lookup {
                published: false,
                etag: etag("e2")?,
            })),
        );
        let (at, reason): (Step, &str) = failure(&run).context("should fail")?;
        ensure!(
            at == Step::RecheckTarget && reason.contains("changed in the bucket"),
            "{reason}"
        );
        Ok(())
    }

    #[test]
    fn an_io_failure_fails_the_run_with_its_reason() -> Result<()> {
        let (run, _): (Run, Command) = send(run(&[BASE])?, Step::ConfirmUnpublished)?;
        let run: Run = answer(run, Report::ReleaseLookedUp(Err("gh: Bad Gateway".to_owned())));
        ensure!(failure(&run) == Some((Step::ConfirmUnpublished, "gh: Bad Gateway")));

        let (run, _): (Run, Command) = send(run_at("Cleared")?, Step::DownloadArchives)?;
        let run: Run = answer(run, Report::ArchivesDownloaded(Err("no target zip".to_owned())));
        ensure!(failure(&run) == Some((Step::DownloadArchives, "no target zip")));
        Ok(())
    }

    #[test]
    fn a_failing_base_is_dropped_and_the_others_continue() -> Result<()> {
        let run: Run = run(&[BASE, OTHER])?;
        let (run, _): (Run, Command) = send(run, Step::ConfirmUnpublished)?;
        let run: Run = answer(run, Report::ReleaseLookedUp(Ok(false)));
        let (run, _): (Run, Command) = send(run, Step::RemoveStalePatches)?;
        let run: Run = answer(run, Report::PrefixRemoved(Ok(())));
        let (run, command): (Run, Command) = send(run, Step::DownloadArchives)?;
        ensure!(matches!(command, Command::DownloadArchives { ref bases, .. } if bases.len() == 2));
        let run: Run = answer(
            run,
            Report::ArchivesDownloaded(Ok(Downloads {
                etag: etag("e1")?,
                size: 10,
                sha256: sha('a')?,
                bases: vec![
                    (tag(BASE)?, Ok(PathBuf::from("work/bases/v1.0.0.zip"))),
                    (tag(OTHER)?, Err("no object".to_owned())),
                ],
            })),
        );
        let State::Downloaded(archives) = &run.state else {
            bail!("not downloaded: {:?}", failure(&run))
        };
        ensure!(archives.bases.keys().count() == 1 && archives.bases.contains(&tag(BASE)?));
        ensure!(
            run.dropped
                == vec![crate::run::state::Dropped {
                    base: tag(OTHER)?,
                    reason: DropReason::ZipMissing("no object".to_owned()),
                }]
        );
        Ok(())
    }

    #[test]
    fn a_base_without_a_result_is_dropped() -> Result<()> {
        let (run, _): (Run, Command) = send(run_at("Scanned")?, Step::EncodePatches)?;
        let run: Run = answer(run, Report::PatchesEncoded(vec![]));
        let (at, reason): (Step, &str) = failure(&run).context("no base left")?;
        ensure!(
            at == Step::EncodePatches && reason.contains("no result reported"),
            "{reason}"
        );
        ensure!(matches!(
            run.dropped.first().map(|d| &d.reason),
            Some(DropReason::EncodeFailed(_))
        ));
        Ok(())
    }

    #[test]
    fn every_base_dropped_fails_the_run_naming_each_reason() -> Result<()> {
        let (run, _): (Run, Command) = send(run_at("Cleared")?, Step::DownloadArchives)?;
        let run: Run = answer(
            run,
            Report::ArchivesDownloaded(Ok(Downloads {
                etag: etag("e1")?,
                size: 10,
                sha256: sha('a')?,
                bases: vec![(tag(BASE)?, Err("no object".to_owned()))],
            })),
        );
        let (at, reason): (Step, &str) = failure(&run).context("should fail")?;
        ensure!(at == Step::DownloadArchives && reason == "no base left (v1.0.0: zip missing: no object)");
        Ok(())
    }

    #[test]
    fn a_rebuild_that_differs_or_fails_or_changes_signatures_drops_the_base() -> Result<()> {
        let cases: [(Result<Rebuilt, String>, &str); 3] = [
            (
                Ok(Rebuilt {
                    tree: tree('2')?,
                    signatures: signatures(true)?,
                }),
                "rebuilt build differs",
            ),
            (
                Ok(Rebuilt {
                    tree: tree('1')?,
                    signatures: signatures(false)?,
                }),
                "signatures changed",
            ),
            (Err("hpatchz failed".to_owned()), "rebuild failed"),
        ];
        for (result, expected) in cases {
            let (run, _): (Run, Command) = send(run_at("Encoded")?, Step::ReplayPatches)?;
            let run: Run = answer(run, Report::PatchesReplayed(vec![(tag(BASE)?, result)]));
            let (at, reason): (Step, &str) = failure(&run).context(expected)?;
            ensure!(at == Step::ReplayPatches && reason.contains(expected), "{reason}");
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
