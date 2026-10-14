//! One workflow per YAML step. Each takes the stage it follows and the capabilities it needs,
//! and returns the next stage with what happened: read inputs (I/O), decide (pure), write.
//! A problem with one base drops that base; a problem with the run is an error.

use std::collections::BTreeMap;
use std::fs;
use std::path::{Path, PathBuf};

use anyhow::{Context, Result, bail, ensure};

use crate::builds::disk::{
    check_signatures, empty_dir, extract as extract_archive, file_size, place, remove, scan, sha256_file,
};
use crate::builds::signatures::{Signatures, signature_changes, signed_artifacts};
use crate::builds::tree::{Build, Tree};
use crate::builds::values::RelPath;
use crate::capabilities::{Caching, Cdn, Differ, Releases, Signer, Storage};
use crate::patching::delta::Delta;
use crate::patching::disk::{encode as encode_plan, rebuild};
use crate::patching::plan::{Plan, plan};
use crate::patching::replay::{Difference, Step, differences, replay_steps};
use crate::publishing::contract::ManifestDto;
use crate::publishing::layout::UploadLayout;
use crate::publishing::manifest::Manifest;
use crate::publishing::values::{ETag, ZipRef};
use crate::releases::bases::Bases;
use crate::releases::tag::{Platform, Tag};
use crate::run::event::Event;
use crate::run::stage::{
    Assembled, Candidate, Cleared, Confirmed, Downloaded, DropReason, Dropped, Encoded, Extracted, Fetched, Inspected,
    Published, Rechecked, Requested, Target, Uploaded, Verified,
};

/// The next stage, what happened, and the bases dropped on the way.
#[derive(Debug)]
pub struct Outcome<S> {
    pub stage: S,
    pub events: Vec<Event>,
    pub drops: Vec<Dropped>,
}

fn moved<S>(stage: S, event: Event) -> Outcome<S> {
    Outcome {
        stage,
        events: vec![event],
        drops: Vec::new(),
    }
}

/// Records a base as dropped.
fn drop_base(base: Tag, reason: DropReason, events: &mut Vec<Event>, drops: &mut Vec<Dropped>) {
    events.push(Event::BaseDropped {
        base: base.clone(),
        reason: reason.clone(),
    });
    drops.push(Dropped { base, reason });
}

/// The bases still in the run; an error naming every drop when none is left.
fn remaining<T>(kept: BTreeMap<Tag, T>, drops: &[Dropped]) -> Result<Bases<T>> {
    if kept.is_empty() {
        let reasons: Vec<String> = drops
            .iter()
            .map(|d: &Dropped| format!("{}: {}", d.base, d.reason))
            .collect();
        bail!("no base left ({})", reasons.join("; "));
    }
    Bases::new(kept)
}

fn error_text(error: &anyhow::Error) -> String {
    format!("{error:#}")
}

/// A run must have bases, none of them the target; duplicates are ignored.
pub fn start(target: Target, bases: Vec<Tag>) -> Result<Requested> {
    let mut unique: Vec<Tag> = Vec::new();
    for base in bases {
        ensure!(base != target.tag, "{base} cannot be its own base");
        if !unique.contains(&base) {
            unique.push(base);
        }
    }
    ensure!(!unique.is_empty(), "no base release to make patches from");
    Ok(Requested { target, bases: unique })
}

/// Patches of a published release are immutable: refuse to make them again.
pub fn confirm_unpublished(stage: Requested, releases: &dyn Releases) -> Result<Outcome<Confirmed>> {
    let Requested { target, bases } = stage;
    ensure!(
        !releases.is_published(&target.tag)?,
        "{} is published; its delta patches are immutable",
        target.tag
    );
    let event: Event = Event::TargetConfirmed(target.tag.clone());
    Ok(moved(Confirmed { target, bases }, event))
}

/// Patches left by an earlier run of this target describe the zip that run saw, so they go.
pub fn clear_stale(stage: Confirmed, storage: &dyn Storage) -> Result<Outcome<Cleared>> {
    let Confirmed { target, bases } = stage;
    let prefix: String = target.prefix();
    storage.remove_prefix(&format!("{prefix}/"))?;
    Ok(moved(Cleared { target, bases }, Event::StaleCleared(prefix)))
}

/// The target zip (and its `ETag`) is required; a base without a zip is dropped.
pub fn download(stage: Cleared, storage: &dyn Storage) -> Result<Outcome<Downloaded>> {
    let Cleared { target, bases } = stage;
    let archive: PathBuf = target.archive();
    fs::create_dir_all(target.work.join("bases")).context("cannot create the work folder")?;
    let etag: ETag = storage
        .download(&target.zip_key(), &archive)
        .context("cannot download the target zip")?;
    let zip: ZipRef = ZipRef {
        name: target.zip.clone(),
        size: file_size(&archive)?,
        sha256: sha256_file(&archive)?,
    };
    let mut events: Vec<Event> = vec![Event::TargetDownloaded {
        size: zip.size,
        etag: etag.clone(),
    }];
    let mut drops: Vec<Dropped> = Vec::new();
    let mut kept: BTreeMap<Tag, PathBuf> = BTreeMap::new();
    for base in bases {
        let path: PathBuf = target.base_archive(&base);
        match storage.download(&target.base_zip_key(&base), &path) {
            Ok(_) => {
                events.push(Event::BaseDownloaded(base.clone()));
                kept.insert(base, path);
            }
            Err(error) => drop_base(
                base,
                DropReason::ZipMissing(error_text(&error)),
                &mut events,
                &mut drops,
            ),
        }
    }
    let bases: Bases<PathBuf> = remaining(kept, &drops)?;
    Ok(Outcome {
        stage: Downloaded {
            target,
            fetched: Fetched { zip, etag },
            bases,
        },
        events,
        drops,
    })
}

/// Unpacks every zip, then removes it. A base whose zip cannot be extracted safely is dropped.
pub fn extract(stage: Downloaded) -> Result<Outcome<Extracted>> {
    let Downloaded { target, fetched, bases } = stage;
    let root: PathBuf =
        extract_archive(&target.archive(), &target.build_dir()).context("cannot extract the target zip")?;
    remove(&target.archive())?;
    let mut events: Vec<Event> = Vec::new();
    let mut drops: Vec<Dropped> = Vec::new();
    let mut kept: BTreeMap<Tag, PathBuf> = BTreeMap::new();
    for (base, archive) in bases {
        let folder: PathBuf = target.base_dir(&base);
        let extracted: Result<PathBuf> = extract_archive(&archive, &folder);
        remove(&archive)?;
        match extracted {
            Ok(base_root) => {
                events.push(Event::BaseExtracted(base.clone()));
                kept.insert(base, base_root);
            }
            Err(error) => {
                remove(&folder)?;
                drop_base(
                    base,
                    DropReason::Unreadable(error_text(&error)),
                    &mut events,
                    &mut drops,
                );
            }
        }
    }
    let bases: Bases<PathBuf> = remaining(kept, &drops)?;
    Ok(Outcome {
        stage: Extracted {
            target,
            fetched,
            root,
            bases,
        },
        events,
        drops,
    })
}

/// Describes every build and records how the target's signed artifacts verify.
pub fn inspect(stage: Extracted, signer: &dyn Signer) -> Result<Outcome<Inspected>> {
    let Extracted {
        target,
        fetched,
        root,
        bases,
    } = stage;
    let tree: Tree = scan(&root).context("cannot describe the target build")?;
    let artifacts: Vec<RelPath> = signed_artifacts(target.platform, &tree);
    let signatures: Signatures = check_signatures(target.platform, &root, &artifacts, signer)?;
    let mut events: Vec<Event> = vec![Event::TargetInspected {
        entries: tree.entries().len(),
        signed: signatures.len(),
    }];
    let mut drops: Vec<Dropped> = Vec::new();
    let mut kept: BTreeMap<Tag, Build> = BTreeMap::new();
    for (base, base_root) in bases {
        match scan(&base_root) {
            Ok(base_tree) => {
                events.push(Event::BaseInspected {
                    base: base.clone(),
                    entries: base_tree.entries().len(),
                });
                kept.insert(
                    base,
                    Build {
                        root: base_root,
                        tree: base_tree,
                    },
                );
            }
            Err(error) => {
                remove(&base_root)?;
                drop_base(
                    base,
                    DropReason::Unreadable(error_text(&error)),
                    &mut events,
                    &mut drops,
                );
            }
        }
    }
    let bases: Bases<Build> = remaining(kept, &drops)?;
    Ok(Outcome {
        stage: Inspected {
            target,
            fetched,
            build: Build { root, tree },
            signatures,
            bases,
        },
        events,
        drops,
    })
}

fn encoded_event(base: &Tag, delta: &Delta) -> Event {
    Event::PatchSetEncoded {
        base: base.clone(),
        patched: delta.patches().len(),
        whole: delta.whole().len(),
        deleted: delta.deleted().len(),
        bytes: delta.download_size(),
    }
}

/// Plans and encodes the patch set of every base into the staging folder.
pub fn encode(stage: Inspected, differ: &dyn Differ) -> Result<Outcome<Encoded>> {
    let Inspected {
        target,
        fetched,
        build,
        signatures,
        bases,
    } = stage;
    let staging: PathBuf = target.staging();
    let mut events: Vec<Event> = Vec::new();
    let mut drops: Vec<Dropped> = Vec::new();
    let mut kept: BTreeMap<Tag, Candidate> = BTreeMap::new();
    for (base, base_build) in bases {
        let base_plan: Plan = plan(&base_build.tree, &build.tree);
        match encode_plan(&base_plan, &base, &base_build, &build, &staging, differ) {
            Ok(delta) => {
                events.push(encoded_event(&base, &delta));
                kept.insert(
                    base,
                    Candidate {
                        build: base_build,
                        delta,
                    },
                );
            }
            Err(error) => {
                remove(&staging.join("from").join(base.to_string()))?;
                remove(&base_build.root)?;
                drop_base(
                    base,
                    DropReason::EncodeFailed(error_text(&error)),
                    &mut events,
                    &mut drops,
                );
            }
        }
    }
    let bases: Bases<Candidate> = remaining(kept, &drops)?;
    Ok(Outcome {
        stage: Encoded {
            target,
            fetched,
            build,
            signatures,
            bases,
        },
        events,
        drops,
    })
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

struct Replay<'a> {
    platform: Platform,
    staging: &'a Path,
    files: &'a Tree,
    artifacts: &'a [RelPath],
    differ: &'a dyn Differ,
    signer: &'a dyn Signer,
}

impl Replay<'_> {
    /// Applies `delta` to a copy of the base like the launcher, then describes the result.
    fn run(&self, base_root: &Path, delta: &Delta, folder: &Path) -> Result<(Tree, Signatures)> {
        let rebuilt: PathBuf = empty_dir(folder)?;
        let steps: Vec<Step> = replay_steps(delta, self.files);
        rebuild(&steps, base_root, self.staging, &rebuilt, self.differ)?;
        let tree: Tree = scan(&rebuilt)?;
        let signatures: Signatures = check_signatures(self.platform, &rebuilt, self.artifacts, self.signer)?;
        Ok((tree, signatures))
    }
}

/// Replays every patch set onto its base like the launcher; a base whose rebuilt build is not
/// exactly the target build (signatures included) is dropped.
pub fn verify(stage: Encoded, differ: &dyn Differ, signer: &dyn Signer) -> Result<Outcome<Verified>> {
    let Encoded {
        target,
        fetched,
        build,
        signatures,
        bases,
    } = stage;
    let staging: PathBuf = target.staging();
    let artifacts: Vec<RelPath> = signatures.keys().cloned().collect();
    let replay: Replay<'_> = Replay {
        platform: target.platform,
        staging: &staging,
        files: &build.tree,
        artifacts: &artifacts,
        differ,
        signer,
    };
    let mut events: Vec<Event> = Vec::new();
    let mut drops: Vec<Dropped> = Vec::new();
    let mut kept: BTreeMap<Tag, Delta> = BTreeMap::new();
    for (base, candidate) in bases {
        let folder: PathBuf = target.rebuild_dir(&base);
        let replayed: Result<(Tree, Signatures)> = replay.run(&candidate.build.root, &candidate.delta, &folder);
        remove(&folder)?;
        remove(&candidate.build.root)?;
        let verdict: Option<DropReason> = match replayed {
            Ok((tree, rebuilt_signatures)) => judge(&build.tree, &signatures, &tree, &rebuilt_signatures, cfg!(unix)),
            Err(error) => Some(DropReason::RebuildFailed(error_text(&error))),
        };
        match verdict {
            None => {
                events.push(Event::BaseVerified(base.clone()));
                kept.insert(base, candidate.delta);
            }
            Some(reason) => {
                remove(&staging.join("from").join(base.to_string()))?;
                drop_base(base, reason, &mut events, &mut drops);
            }
        }
    }
    let deltas: Bases<Delta> = remaining(kept, &drops)?;
    Ok(Outcome {
        stage: Verified {
            target,
            fetched,
            files: build.tree,
            deltas,
        },
        events,
        drops,
    })
}

/// Builds the manifest and the upload folders: the verified patches and `manifest.json`.
pub fn assemble(stage: Verified) -> Result<Outcome<Assembled>> {
    let Verified {
        target,
        fetched,
        files,
        deltas,
    } = stage;
    let manifest: Manifest = Manifest::new(target.tag.clone(), target.platform, fetched.zip.clone(), files, deltas)?;
    let layout: UploadLayout = target.upload();
    empty_dir(&layout.patches)?;
    empty_dir(&layout.manifest)?;
    for base in manifest.deltas().keys() {
        let folder: PathBuf = Path::new("from").join(base.to_string());
        place(&target.staging().join(&folder), &layout.patches.join(&folder))?;
    }
    let text: String = serde_json::to_string_pretty(&ManifestDto::from(&manifest))?;
    fs::write(layout.manifest.join("manifest.json"), text).context("cannot write manifest.json")?;
    let event: Event = Event::ManifestAssembled {
        bases: manifest.deltas().keys().len(),
    };
    Ok(moved(
        Assembled {
            target,
            fetched,
            manifest,
            layout,
        },
        event,
    ))
}

/// Refuses to upload when the target was published meanwhile, or when its zip in the bucket is
/// no longer the one the patches lead to (a later build of the same tag replaced it).
pub fn recheck(stage: Assembled, releases: &dyn Releases, storage: &dyn Storage) -> Result<Outcome<Rechecked>> {
    let Assembled {
        target,
        fetched,
        manifest,
        layout,
    } = stage;
    ensure!(
        !releases.is_published(&target.tag)?,
        "{} was published while its patches were made",
        target.tag
    );
    let now: ETag = storage.etag(&target.zip_key())?;
    ensure!(
        now == fetched.etag,
        "{} changed in the bucket during the run (ETag {} -> {now})",
        target.zip,
        fetched.etag
    );
    let event: Event = Event::TargetRechecked(target.tag.clone());
    Ok(moved(
        Rechecked {
            target,
            manifest,
            layout,
        },
        event,
    ))
}

/// Patches first, as immutable: the manifest never names a patch that is not uploaded yet.
pub fn upload_patches(stage: Rechecked, cdn: &dyn Cdn) -> Result<Outcome<Uploaded>> {
    let Rechecked {
        target,
        manifest,
        layout,
    } = stage;
    cdn.upload(&layout.patches, &layout.prefix, Caching::Immutable)?;
    let event: Event = Event::PatchesUploaded(layout.prefix.clone());
    Ok(moved(
        Uploaded {
            target,
            manifest,
            layout,
        },
        event,
    ))
}

/// The manifest last, revalidated: a rebuild of this unpublished tag rewrites it.
pub fn publish_manifest(stage: Uploaded, cdn: &dyn Cdn) -> Result<Outcome<Published>> {
    let Uploaded {
        target,
        manifest,
        layout,
    } = stage;
    cdn.upload(&layout.manifest, &layout.prefix, Caching::Revalidated)?;
    let event: Event = Event::ManifestPublished(layout.prefix.clone());
    Ok(moved(Published { target, manifest }, event))
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeMap;

    use anyhow::{Result, ensure};

    use super::{judge, start};
    use crate::builds::signatures::Signatures;
    use crate::builds::tree::{Node, Tree};
    use crate::builds::values::{Mode, RelPath, Sha256};
    use crate::releases::tag::{Platform, Tag};
    use crate::run::stage::{DropReason, Target};

    fn target() -> Result<Target> {
        Ok(Target {
            tag: "v1.1.0".parse()?,
            platform: Platform::Windows64,
            zip: crate::publishing::values::ZipName::try_new("Decentraland_windows64.zip".to_owned())?,
            releases: "releases".to_owned(),
            work: std::path::PathBuf::from("work"),
        })
    }

    fn tree(digit: char) -> Result<Tree> {
        let mut entries: BTreeMap<RelPath, Node> = BTreeMap::new();
        let node: Node = Node::File {
            size: 1,
            sha256: Sha256::try_new(digit.to_string().repeat(64))?,
            mode: Mode::try_new("644".to_owned())?,
        };
        entries.insert(RelPath::try_new("a.exe".to_owned())?, node);
        Tree::new(entries)
    }

    #[test]
    fn start_refuses_no_bases_and_the_target_as_base() -> Result<()> {
        ensure!(start(target()?, vec![]).is_err());
        let own: Tag = "v1.1.0".parse()?;
        ensure!(start(target()?, vec![own]).is_err());
        let base: Tag = "v1.0.0".parse()?;
        ensure!(start(target()?, vec![base.clone(), base])?.bases.len() == 1);
        Ok(())
    }

    #[test]
    fn judge_accepts_an_exact_rebuild_and_names_what_differs() -> Result<()> {
        let mut signatures: Signatures = Signatures::new();
        signatures.insert(RelPath::try_new("a.exe".to_owned())?, true);
        ensure!(judge(&tree('1')?, &signatures, &tree('1')?, &signatures, true).is_none());
        ensure!(matches!(
            judge(&tree('1')?, &signatures, &tree('2')?, &signatures, true),
            Some(DropReason::RebuildDiffers(_))
        ));
        let mut invalid: Signatures = Signatures::new();
        invalid.insert(RelPath::try_new("a.exe".to_owned())?, false);
        ensure!(matches!(
            judge(&tree('1')?, &signatures, &tree('1')?, &invalid, true),
            Some(DropReason::SignaturesChanged(_))
        ));
        Ok(())
    }
}
