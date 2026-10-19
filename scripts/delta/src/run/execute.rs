//! Runs one `Cmd` and answers with the `Msg` that says what happened. No decision about the
//! run is made here: an executor may call pure domain helpers, cleans up what it consumed or
//! what failed, and never sees the state. A cmd that cannot be carried out at all answers
//! `IntegrityFailure`.

use std::fs;
use std::path::{Path, PathBuf};

use anyhow::{Context, Result, anyhow};

use crate::builds::disk::{empty_dir, extract, file_size, place, remove, scan, sha256_file};
use crate::builds::tree::{Build, Tree};
use crate::io::{Caching, Io};
use crate::patching::delta::Delta;
use crate::patching::disk::{encode, rebuild};
use crate::patching::plan::Plan;
use crate::publishing::layout::{UploadLayout, base_folder};
use crate::publishing::values::ETag;
use crate::releases::tag::Tag;
use crate::run::fsm::{Cmd, Fetch, Msg, Replay, Unpack};

pub fn execute(cmd: Cmd, io: &Io) -> Msg {
    let produced: Result<Msg> = match cmd {
        Cmd::RemoveStalePatches { prefix } => io.bucket.remove_prefix(&prefix).map(|()| Msg::StalePatchesRemoved),
        Cmd::DownloadArchives { release, bases } => download_archives(&release, bases, io),
        Cmd::ExtractArchives { release, bases } => extract_archives(&release, bases),
        Cmd::ScanBuilds { release_root, bases } => scan_builds(&release_root, bases),
        Cmd::EncodePatches {
            staging,
            build_id,
            release,
            bases,
        } => Ok(encode_patches(&staging, &build_id, &release, bases, io)),
        Cmd::ReplayPatches { staging, bases } => Ok(replay_patches(&staging, bases, io)),
        Cmd::PrepareUploadFolder {
            manifest_json,
            layout,
            moves,
        } => prepare_upload_folder(&manifest_json, &layout, &moves).map(|()| Msg::UploadFolderPrepared),
        Cmd::RecheckRelease { tag, zip_key } => recheck_release(&tag, &zip_key, io),
        Cmd::UploadPatches { folder, prefix } => io
            .bucket
            .upload(&folder, &prefix, Caching::Immutable)
            .map(|()| Msg::PatchesUploaded),
        Cmd::PublishManifest { folder, prefix } => io
            .bucket
            .upload(&folder, &prefix, Caching::ShortLived)
            .map(|()| Msg::ManifestPublished),
        Cmd::Complete => Err(anyhow!("complete asks the runtime to stop; it is not executed")),
    };
    match produced {
        Ok(msg) => msg,
        Err(error) => Msg::IntegrityFailure {
            reason: format!("{error:#}"),
        },
    }
}

fn text(error: &anyhow::Error) -> String {
    format!("{error:#}")
}

/// Removes every one of `paths`, going on past a failed removal; the reasons of the failed ones.
fn remove_all(paths: &[&Path]) -> Vec<String> {
    paths
        .iter()
        .filter_map(|path: &&Path| remove(path).err())
        .map(|cleanup: anyhow::Error| text(&cleanup))
        .collect()
}

/// Removes `paths` after a failure; each failed removal is added to the reason.
fn discard(paths: &[&Path], error: &anyhow::Error) -> String {
    let failures: Vec<String> = remove_all(paths);
    if failures.is_empty() {
        text(error)
    } else {
        format!("{error:#} (and cannot clean up: {})", failures.join("; "))
    }
}

/// `None` when the bucket has no object at the key.
fn fetch(fetch: &Fetch, io: &Io) -> Result<Option<ETag>> {
    if let Some(parent) = fetch.to.parent() {
        fs::create_dir_all(parent).with_context(|| format!("cannot create {}", parent.display()))?;
    }
    io.bucket.download(&fetch.key, &fetch.to)
}

/// A base without a zip is reported as missing. Any other download failure, for the release or
/// for a base, is a failure of the cmd: credentials, throttling or the network cannot be trusted.
fn download_archives(release: &Fetch, bases: Vec<(Tag, Fetch)>, io: &Io) -> Result<Msg> {
    let zip_etag: ETag = fetch(release, io)
        .and_then(|found: Option<ETag>| found.with_context(|| format!("no object {}", release.key)))
        .context("cannot download the release zip")?;
    let zip_size: u64 = file_size(&release.to)?;
    let zip_sha256 = sha256_file(&release.to)?;
    let mut found: Vec<(Tag, PathBuf)> = Vec::new();
    let mut missing: Vec<(Tag, String)> = Vec::new();
    for (base, archive) in bases {
        match fetch(&archive, io).with_context(|| format!("cannot download {}", archive.key))? {
            Some(_) => found.push((base, archive.to)),
            None => missing.push((base, format!("no object {}", archive.key))),
        }
    }
    Ok(Msg::ArchivesDownloaded {
        zip_etag,
        zip_size,
        zip_sha256,
        bases: found,
        missing,
    })
}

/// Each archive is removed once extracted; a base that fails leaves no folder behind.
fn extract_archives(release: &Unpack, bases: Vec<(Tag, Unpack)>) -> Result<Msg> {
    let release_root: PathBuf = extract(&release.archive, &release.into).context("cannot extract the release zip")?;
    remove(&release.archive)?;
    let mut found: Vec<(Tag, PathBuf)> = Vec::new();
    let mut unreadable: Vec<(Tag, String)> = Vec::new();
    for (base, unpack) in bases {
        let extracted: Result<PathBuf> = extract(&unpack.archive, &unpack.into);
        remove(&unpack.archive)?;
        match extracted {
            Ok(root) => found.push((base, root)),
            Err(error) => unreadable.push((base, discard(&[&unpack.into], &error))),
        }
    }
    Ok(Msg::ArchivesExtracted {
        release_root,
        bases: found,
        unreadable,
    })
}

/// A base that cannot be described is removed.
fn scan_builds(release_root: &Path, bases: Vec<(Tag, PathBuf)>) -> Result<Msg> {
    let tree: Tree = scan(release_root).context("cannot describe the release build")?;
    let mut found: Vec<(Tag, Build)> = Vec::new();
    let mut unreadable: Vec<(Tag, String)> = Vec::new();
    for (base, root) in bases {
        match scan(&root) {
            Ok(base_tree) => found.push((base, Build { root, tree: base_tree })),
            Err(error) => unreadable.push((base, discard(&[&root], &error))),
        }
    }
    Ok(Msg::BuildsScanned {
        release: Build {
            root: release_root.to_path_buf(),
            tree,
        },
        bases: found,
        unreadable,
    })
}

/// A base whose patch set fails to encode leaves nothing in staging, and its build is removed.
fn encode_patches(staging: &Path, build_id: &str, release: &Build, bases: Vec<(Tag, Build, Plan)>, io: &Io) -> Msg {
    let mut encoded: Vec<(Tag, Delta)> = Vec::new();
    let mut failed: Vec<(Tag, String)> = Vec::new();
    for (base, build, plan) in bases {
        match encode(&plan, &base, build_id, &build, release, staging, &io.hdiff) {
            Ok(delta) => encoded.push((base, delta)),
            Err(error) => {
                let partial: PathBuf = staging.join(base_folder(&base));
                failed.push((base, discard(&[&partial, &build.root], &error)));
            }
        }
    }
    Msg::PatchesEncoded { encoded, failed }
}

fn rebuild_and_describe(staging: &Path, replay: &Replay, io: &Io) -> Result<Tree> {
    let rebuilt: PathBuf = empty_dir(&replay.rebuild)?;
    rebuild(&replay.actions, &replay.base, staging, &rebuilt, &io.hdiff)?;
    scan(&rebuilt)
}

/// The base build and its rebuild folder are removed afterwards, whatever the outcome.
fn replay_patches(staging: &Path, bases: Vec<(Tag, Replay)>, io: &Io) -> Msg {
    let mut rebuilt: Vec<(Tag, Tree)> = Vec::new();
    let mut failed: Vec<(Tag, String)> = Vec::new();
    for (base, replay) in bases {
        let replayed: Result<Tree> = rebuild_and_describe(staging, &replay, io);
        match replayed {
            Ok(result) => {
                let failures: Vec<String> = remove_all(&[&replay.rebuild, &replay.base]);
                if failures.is_empty() {
                    rebuilt.push((base, result));
                } else {
                    failed.push((base, format!("cannot clean up: {}", failures.join("; "))));
                }
            }
            Err(error) => failed.push((base, discard(&[&replay.rebuild, &replay.base], &error))),
        }
    }
    Msg::PatchesReplayed { rebuilt, failed }
}

fn prepare_upload_folder(manifest_json: &str, layout: &UploadLayout, moves: &[(PathBuf, PathBuf)]) -> Result<()> {
    empty_dir(&layout.patches)?;
    empty_dir(&layout.manifest)?;
    for (from, to) in moves {
        place(from, to)?;
    }
    fs::write(layout.manifest.join("manifest.json"), manifest_json).context("cannot write manifest.json")
}

fn recheck_release(tag: &Tag, zip_key: &str, io: &Io) -> Result<Msg> {
    Ok(Msg::ReleaseRechecked {
        published: io.releases.is_published(tag)?,
        zip_etag: io.bucket.etag(zip_key)?,
    })
}
