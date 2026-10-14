//! Making a patch set with `HDiffPatch`, and replaying one onto a copy of its base.

use std::fs;
use std::path::{Path, PathBuf};

use anyhow::{Context, Result, ensure};

use crate::builds::disk::{
    copy_build, file_size, make_link, place, remove, remove_empty_parents, set_mode, sha256_file,
};
use crate::builds::tree::{Build, Node};
use crate::builds::values::{RelPath, Sha256};
use crate::patching::delta::{Delta, Patch, Whole};
use crate::patching::plan::{Job, Plan, needs_whole_trial, patch_file, prefer_whole};
use crate::patching::replay::Action;
use crate::releases::tag::Tag;
use crate::shell::hdiff::HDiffPatch;

/// Scratch files of one encoding or replay: an empty file and a work slot.
struct Scratch {
    _dir: tempfile::TempDir,
    empty: PathBuf,
    slot: PathBuf,
}

impl Scratch {
    /// Created inside `near`, on the same file system, so results can be moved into place.
    fn new(near: &Path) -> Result<Scratch> {
        fs::create_dir_all(near).with_context(|| format!("cannot create {}", near.display()))?;
        let dir: tempfile::TempDir = tempfile::tempdir_in(near)?;
        let empty: PathBuf = dir.path().join("empty");
        fs::write(&empty, b"").context("cannot create the empty file")?;
        let slot: PathBuf = dir.path().join("slot");
        Ok(Scratch { _dir: dir, empty, slot })
    }
}

/// Writes the patch and proves it turns `old` back into the file with digest `expected`.
fn diff_checked(
    hdiff: &HDiffPatch,
    old: &Path,
    new: &Path,
    expected: &Sha256,
    patch: &Path,
    roundtrip: &Path,
) -> Result<()> {
    hdiff.diff(old, new, patch)?;
    hdiff.patch(old, patch, roundtrip)?;
    let rebuilt: Sha256 = sha256_file(roundtrip)?;
    ensure!(&rebuilt == expected, "patch round trip differs for {}", new.display());
    remove(roundtrip)
}

fn target_digest(target: &Build, path: &RelPath) -> Result<(u64, Sha256)> {
    match target.tree.get(path) {
        Some(Node::File { size, sha256, .. }) => Ok((*size, sha256.clone())),
        _ => anyhow::bail!("{path} is not a file of the target build"),
    }
}

fn base_digest(base: &Build, path: &RelPath) -> Result<Sha256> {
    match base.tree.get(path) {
        Some(Node::File { sha256, .. }) => Ok(sha256.clone()),
        _ => anyhow::bail!("{path} is not a file of the base build"),
    }
}

/// Encodes every job of `plan` under `staging` (`staging/from/<base>/<path>.hdiff`). A diff
/// at least half the file's size is also tried whole, and the smaller wins. Every patch is
/// applied back and must reproduce the target file.
pub fn encode(
    plan: &Plan,
    base_tag: &Tag,
    base: &Build,
    target: &Build,
    staging: &Path,
    hdiff: &HDiffPatch,
) -> Result<Delta> {
    let scratch: Scratch = Scratch::new(staging)?;
    let mut patches: Vec<Patch> = Vec::new();
    let mut whole: Vec<Whole> = Vec::new();
    for job in &plan.jobs {
        let path: &RelPath = job.path();
        let file: RelPath = patch_file(base_tag, path)?;
        let out: PathBuf = file.under(staging);
        fs::create_dir_all(out.parent().unwrap_or(staging))
            .with_context(|| format!("cannot create the folder of {file}"))?;
        let new_file: PathBuf = path.under(&target.root);
        let (size, sha256): (u64, Sha256) = target_digest(target, path)?;
        match job {
            Job::Whole { .. } => {
                diff_checked(hdiff, &scratch.empty, &new_file, &sha256, &out, &scratch.slot)?;
                whole.push(Whole {
                    path: path.clone(),
                    size: file_size(&out)?,
                    sha256: sha256_file(&out)?,
                    file,
                });
            }
            Job::Diff { from, .. } => {
                diff_checked(hdiff, &from.under(&base.root), &new_file, &sha256, &out, &scratch.slot)?;
                let diff_size: u64 = file_size(&out)?;
                let mut whole_wins: bool = false;
                if needs_whole_trial(size, diff_size) {
                    let trial: PathBuf = scratch.slot.with_extension("whole");
                    diff_checked(hdiff, &scratch.empty, &new_file, &sha256, &trial, &scratch.slot)?;
                    whole_wins = prefer_whole(diff_size, file_size(&trial)?);
                    if whole_wins {
                        place(&trial, &out)?;
                    } else {
                        remove(&trial)?;
                    }
                }
                if whole_wins {
                    whole.push(Whole {
                        path: path.clone(),
                        size: file_size(&out)?,
                        sha256: sha256_file(&out)?,
                        file,
                    });
                } else {
                    let from_sha256: Sha256 = base_digest(base, from)?;
                    patches.push(Patch {
                        path: path.clone(),
                        from: from.clone(),
                        from_sha256,
                        size: diff_size,
                        sha256: sha256_file(&out)?,
                        file,
                    });
                }
            }
        }
    }
    Delta::new(plan.deleted.clone(), patches, whole)
}

fn apply_patch(
    old: &Path,
    patch: &Path,
    sha256: &Sha256,
    target: &Path,
    scratch: &Scratch,
    hdiff: &HDiffPatch,
) -> Result<()> {
    ensure!(patch.is_file(), "missing patch {}", patch.display());
    ensure!(
        &sha256_file(patch)? == sha256,
        "patch sha256 mismatch: {}",
        patch.display()
    );
    hdiff.patch(old, patch, &scratch.slot)?;
    place(&scratch.slot, target)
}

/// Copies `base` into the empty `rebuild` folder and applies `actions` like the launcher.
/// Patches are read from `patches` and applied to files of the untouched `base`.
pub fn rebuild(actions: &[Action], base: &Path, patches: &Path, rebuild: &Path, hdiff: &HDiffPatch) -> Result<()> {
    copy_build(base, rebuild)?;
    let scratch: Scratch = Scratch::new(rebuild.parent().unwrap_or(rebuild))?;
    for action in actions {
        match action {
            Action::Delete(path) => {
                let target: PathBuf = path.under(rebuild);
                remove(&target)?;
                remove_empty_parents(&target, rebuild)?;
            }
            Action::Diff {
                from,
                file,
                sha256,
                path,
            } => {
                apply_patch(
                    &from.under(base),
                    &file.under(patches),
                    sha256,
                    &path.under(rebuild),
                    &scratch,
                    hdiff,
                )?;
            }
            Action::Whole { file, sha256, path } => {
                apply_patch(
                    &scratch.empty,
                    &file.under(patches),
                    sha256,
                    &path.under(rebuild),
                    &scratch,
                    hdiff,
                )?;
            }
            Action::Link { path, target } => {
                let link: PathBuf = path.under(rebuild);
                remove(&link)?;
                fs::create_dir_all(link.parent().unwrap_or(rebuild))?;
                make_link(target, &link)?;
            }
            Action::Mode { path, mode } => set_mode(&path.under(rebuild), mode)?,
        }
    }
    Ok(())
}
