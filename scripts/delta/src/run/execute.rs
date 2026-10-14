//! Runs one `Command` and reports what happened. No decision about the run is made here: an
//! executor may call pure domain helpers, cleans up what it consumed or what failed, and never
//! sees the state.

use std::fs;
use std::path::{Path, PathBuf};

use anyhow::{Context, Result};

use crate::builds::disk::{check_signatures, empty_dir, extract, file_size, place, remove, scan, sha256_file};
use crate::builds::signatures::{Signatures, signed_artifacts};
use crate::builds::tree::{Build, Tree};
use crate::builds::values::{RelPath, Sha256};
use crate::io::{Caching, Io};
use crate::patching::delta::Delta;
use crate::patching::disk::{encode, rebuild};
use crate::patching::plan::Plan;
use crate::publishing::layout::UploadLayout;
use crate::publishing::values::ETag;
use crate::releases::tag::{Platform, Tag};
use crate::run::fsm::{Command, Downloads, Extractions, Fetch, Lookup, Rebuilt, Replay, Report, Scans, Unpack};

pub fn execute(command: Command, io: &Io) -> Report {
    match command {
        Command::LookupRelease { tag } => Report::ReleaseLookedUp(io.releases.is_published(&tag).map_err(text)),
        Command::RemovePrefix { prefix } => Report::PrefixRemoved(io.bucket.remove_prefix(&prefix).map_err(text)),
        Command::DownloadArchives { target, bases } => {
            Report::ArchivesDownloaded(download_archives(&target, bases, io).map_err(text))
        }
        Command::ExtractArchives { target, bases } => {
            Report::ArchivesExtracted(extract_archives(&target, bases).map_err(text))
        }
        Command::ScanBuilds {
            platform,
            target,
            bases,
        } => Report::BuildsScanned(scan_builds(platform, &target, bases, io).map_err(text)),
        Command::EncodePatches { staging, target, bases } => {
            Report::PatchesEncoded(encode_patches(&staging, &target, bases, io))
        }
        Command::ReplayPatches {
            platform,
            staging,
            artifacts,
            bases,
        } => Report::PatchesReplayed(replay_patches(platform, &staging, &artifacts, bases, io)),
        Command::WriteUpload {
            manifest_json,
            layout,
            moves,
        } => Report::UploadWritten(write_upload(&manifest_json, &layout, &moves).map_err(text)),
        Command::LookupTarget { tag, zip_key } => {
            Report::TargetLookedUp(lookup_target(&tag, &zip_key, io).map_err(text))
        }
        Command::UploadPatches { folder, prefix } => {
            Report::PatchesUploaded(io.bucket.upload(&folder, &prefix, Caching::Immutable).map_err(text))
        }
        Command::PublishManifest { folder, prefix } => {
            Report::ManifestPublished(io.bucket.upload(&folder, &prefix, Caching::Revalidated).map_err(text))
        }
    }
}

#[allow(clippy::needless_pass_by_value)] // passed to `map_err` as is
fn text(error: anyhow::Error) -> String {
    format!("{error:#}")
}

/// Removes `paths` after a failure; a failed removal is added to the reason.
fn discard(paths: &[&Path], error: anyhow::Error) -> String {
    for path in paths {
        if let Err(cleanup) = remove(path) {
            return format!("{error:#} (and cannot clean up: {cleanup:#})");
        }
    }
    text(error)
}

fn fetch(fetch: &Fetch, io: &Io) -> Result<ETag> {
    if let Some(parent) = fetch.to.parent() {
        fs::create_dir_all(parent).with_context(|| format!("cannot create {}", parent.display()))?;
    }
    io.bucket.download(&fetch.key, &fetch.to)
}

fn download_archives(target: &Fetch, bases: Vec<(Tag, Fetch)>, io: &Io) -> Result<Downloads> {
    let etag: ETag = fetch(target, io).context("cannot download the target zip")?;
    let size: u64 = file_size(&target.to)?;
    let sha256: Sha256 = sha256_file(&target.to)?;
    let mut results: Vec<(Tag, Result<PathBuf, String>)> = Vec::new();
    for (base, archive) in bases {
        let result: Result<PathBuf, String> = match fetch(&archive, io) {
            Ok(_) => Ok(archive.to),
            Err(error) => Err(text(error)),
        };
        results.push((base, result));
    }
    Ok(Downloads {
        etag,
        size,
        sha256,
        bases: results,
    })
}

/// Each archive is removed once extracted; a base that fails leaves no folder behind.
fn extract_archives(target: &Unpack, bases: Vec<(Tag, Unpack)>) -> Result<Extractions> {
    let root: PathBuf = extract(&target.archive, &target.into).context("cannot extract the target zip")?;
    remove(&target.archive)?;
    let mut results: Vec<(Tag, Result<PathBuf, String>)> = Vec::new();
    for (base, unpack) in bases {
        let extracted: Result<PathBuf> = extract(&unpack.archive, &unpack.into);
        remove(&unpack.archive)?;
        let result: Result<PathBuf, String> = match extracted {
            Ok(base_root) => Ok(base_root),
            Err(error) => Err(discard(&[&unpack.into], error)),
        };
        results.push((base, result));
    }
    Ok(Extractions { root, bases: results })
}

/// A base that cannot be described is removed.
fn scan_builds(platform: Platform, target: &Path, bases: Vec<(Tag, PathBuf)>, io: &Io) -> Result<Scans> {
    let tree: Tree = scan(target).context("cannot describe the target build")?;
    let artifacts: Vec<RelPath> = signed_artifacts(platform, &tree);
    let signatures: Signatures = check_signatures(platform, target, &artifacts, io.signer.as_ref())?;
    let mut results: Vec<(Tag, Result<Build, String>)> = Vec::new();
    for (base, root) in bases {
        let result: Result<Build, String> = match scan(&root) {
            Ok(base_tree) => Ok(Build { root, tree: base_tree }),
            Err(error) => Err(discard(&[&root], error)),
        };
        results.push((base, result));
    }
    Ok(Scans {
        build: Build {
            root: target.to_path_buf(),
            tree,
        },
        signatures,
        bases: results,
    })
}

/// A base whose patch set fails to encode leaves nothing in staging, and its build is removed.
fn encode_patches(
    staging: &Path,
    target: &Build,
    bases: Vec<(Tag, Build, Plan)>,
    io: &Io,
) -> Vec<(Tag, Result<Delta, String>)> {
    let mut results: Vec<(Tag, Result<Delta, String>)> = Vec::new();
    for (base, build, plan) in bases {
        let result: Result<Delta, String> = match encode(&plan, &base, &build, target, staging, &io.hdiff) {
            Ok(delta) => Ok(delta),
            Err(error) => {
                let partial: PathBuf = staging.join("from").join(base.to_string());
                Err(discard(&[&partial, &build.root], error))
            }
        };
        results.push((base, result));
    }
    results
}

fn rebuild_and_describe(
    platform: Platform,
    staging: &Path,
    artifacts: &[RelPath],
    replay: &Replay,
    io: &Io,
) -> Result<Rebuilt> {
    let rebuilt: PathBuf = empty_dir(&replay.rebuild)?;
    rebuild(&replay.actions, &replay.base, staging, &rebuilt, &io.hdiff)?;
    let tree: Tree = scan(&rebuilt)?;
    let signatures: Signatures = check_signatures(platform, &rebuilt, artifacts, io.signer.as_ref())?;
    Ok(Rebuilt { tree, signatures })
}

/// The base build and its rebuild folder are removed afterwards, whatever the outcome.
fn replay_patches(
    platform: Platform,
    staging: &Path,
    artifacts: &[RelPath],
    bases: Vec<(Tag, Replay)>,
    io: &Io,
) -> Vec<(Tag, Result<Rebuilt, String>)> {
    let mut results: Vec<(Tag, Result<Rebuilt, String>)> = Vec::new();
    for (base, replay) in bases {
        let replayed: Result<Rebuilt> = rebuild_and_describe(platform, staging, artifacts, &replay, io);
        let cleaned: Result<()> = remove(&replay.rebuild).and_then(|()| remove(&replay.base));
        let result: Result<Rebuilt, String> = match (replayed, cleaned) {
            (Ok(rebuilt), Ok(())) => Ok(rebuilt),
            (Err(error), _) | (Ok(_), Err(error)) => Err(text(error)),
        };
        results.push((base, result));
    }
    results
}

fn write_upload(manifest_json: &str, layout: &UploadLayout, moves: &[(PathBuf, PathBuf)]) -> Result<()> {
    empty_dir(&layout.patches)?;
    empty_dir(&layout.manifest)?;
    for (from, to) in moves {
        place(from, to)?;
    }
    fs::write(layout.manifest.join("manifest.json"), manifest_json).context("cannot write manifest.json")
}

fn lookup_target(tag: &Tag, zip_key: &str, io: &Io) -> Result<Lookup> {
    Ok(Lookup {
        published: io.releases.is_published(tag)?,
        etag: io.bucket.etag(zip_key)?,
    })
}
