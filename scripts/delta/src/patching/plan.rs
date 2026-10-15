//! What one base needs to become the target build, decided before any patch is made.

use std::collections::BTreeMap;

use anyhow::{Context, Result};
use serde::{Deserialize, Serialize};

use crate::builds::tree::{Node, Tree};
use crate::builds::values::{RelPath, is_lower_hex};
use crate::publishing::layout::patch_folder;
use crate::releases::tag::Tag;

/// Hex digits of the content hash Unity appends to a bundle name.
const BUNDLE_HASH_DIGITS: usize = 32;

/// How one file of the target build is produced.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "lowercase")]
pub enum Job {
    /// Patched from `from` in the base.
    Diff { path: RelPath, from: RelPath },
    /// Encoded on its own (a patch against an empty file).
    Whole { path: RelPath },
}

impl Job {
    pub fn path(&self) -> &RelPath {
        match self {
            Job::Diff { path, .. } | Job::Whole { path } => path,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Plan {
    /// Base paths (files or links) absent from the target build.
    pub deleted: Vec<RelPath>,
    pub jobs: Vec<Job>,
}

/// `dir/name` for `dir/name_<content hash>.bundle`: a bundle renamed by its content hash.
fn bundle_key(path: &RelPath) -> Option<&str> {
    let stem: &str = path.as_str().strip_suffix(".bundle")?;
    let (name, hash): (&str, &str) = stem.rsplit_once('_')?;
    let is_hash: bool = hash.len() == BUNDLE_HASH_DIGITS && is_lower_hex(hash);
    is_hash.then_some(name)
}

fn is_file(tree: &Tree, path: &RelPath) -> bool {
    matches!(tree.get(path), Some(Node::File { .. }))
}

/// Changed files are patched from the same path. A bundle renamed by its content hash is
/// patched from the one base bundle of the same name that left the build. Everything else that
/// is new or changed ships whole. Unchanged files, and changes to modes or links, need no job.
/// Changing the pairing changes the patch bytes: bump `ENCODING_GENERATION` (`run::fsm`).
pub fn plan(base: &Tree, target: &Tree) -> Plan {
    let mut renamed_away: BTreeMap<&str, Vec<&RelPath>> = BTreeMap::new();
    for (path, _, _) in base.files() {
        if !is_file(target, path)
            && let Some(key) = bundle_key(path)
        {
            renamed_away.entry(key).or_default().push(path);
        }
    }

    let mut jobs: Vec<Job> = Vec::new();
    for (path, _, sha256) in target.files() {
        if let Some(Node::File { sha256: old, .. }) = base.get(path) {
            if old != sha256 {
                jobs.push(Job::Diff {
                    path: path.clone(),
                    from: path.clone(),
                });
            }
            continue;
        }
        let candidates: &[&RelPath] = bundle_key(path)
            .and_then(|key: &str| renamed_away.get(key))
            .map_or(&[], Vec::as_slice);
        match candidates {
            [only] => jobs.push(Job::Diff {
                path: path.clone(),
                from: (*only).clone(),
            }),
            _ => jobs.push(Job::Whole { path: path.clone() }),
        }
    }

    let mut deleted: Vec<RelPath> = Vec::new();
    for path in base.entries().keys() {
        if target.get(path).is_none() {
            deleted.push(path.clone());
        }
    }
    Plan { deleted, jobs }
}

/// A patch at least half the file's size may lose to encoding the file on its own.
pub fn needs_whole_trial(file_size: u64, diff_size: u64) -> bool {
    diff_size.saturating_mul(2) >= file_size
}

/// The whole-file encoding wins only when strictly smaller; a tie keeps the patch. Changing this
/// or `needs_whole_trial` changes the patch bytes: bump `ENCODING_GENERATION` (`run::fsm`).
pub fn prefer_whole(diff_size: u64, whole_size: u64) -> bool {
    whole_size < diff_size
}

/// Where the patch of `path` from `base` lives, relative to the manifest's folder. `build_id`
/// names the release build, so a rebuild of the same tag never writes under a key a CDN edge may
/// already hold as immutable.
pub fn patch_file(base: &Tag, build_id: &str, path: &RelPath) -> Result<RelPath> {
    let text: String = format!("{}/{path}.hdiff", patch_folder(base, build_id));
    RelPath::try_new(text.clone()).with_context(|| format!("invalid patch path {text:?}"))
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeMap;

    use anyhow::{Result, ensure};

    use super::{Job, Plan, needs_whole_trial, patch_file, plan, prefer_whole};
    use crate::builds::tree::{Node, Tree};
    use crate::builds::values::{Mode, RelPath, Sha256};

    const OLD_BUNDLE: &str = "Data/aa/ui_assets_all_0123456789abcdef0123456789abcdef.bundle";
    const NEW_BUNDLE: &str = "Data/aa/ui_assets_all_fedcba9876543210fedcba9876543210.bundle";

    fn path(text: &str) -> Result<RelPath> {
        Ok(RelPath::try_new(text.to_owned())?)
    }

    fn file(digit: char, mode: &str) -> Result<Node> {
        Ok(Node::File {
            size: 1,
            sha256: Sha256::try_new(digit.to_string().repeat(64))?,
            mode: Mode::try_new(mode.to_owned())?,
        })
    }

    fn tree(entries: &[(&str, Node)]) -> Result<Tree> {
        let mut map: BTreeMap<RelPath, Node> = BTreeMap::new();
        for (text, node) in entries {
            map.insert(path(text)?, node.clone());
        }
        Tree::new(map)
    }

    #[test]
    fn patches_changed_files_and_skips_unchanged() -> Result<()> {
        let base: Tree = tree(&[("a", file('1', "644")?), ("same", file('5', "644")?)])?;
        let target: Tree = tree(&[("a", file('2', "644")?), ("same", file('5', "644")?)])?;
        ensure!(
            plan(&base, &target)
                == Plan {
                    deleted: vec![],
                    jobs: vec![Job::Diff {
                        path: path("a")?,
                        from: path("a")?
                    }]
                }
        );
        Ok(())
    }

    #[test]
    fn ships_new_files_whole_and_lists_deleted_paths() -> Result<()> {
        let base: Tree = tree(&[
            ("gone.txt", file('1', "644")?),
            (
                "old-link",
                Node::Link {
                    target: "gone.txt".to_owned(),
                },
            ),
        ])?;
        let target: Tree = tree(&[("added.txt", file('2', "644")?)])?;
        let expected: Plan = Plan {
            deleted: vec![path("gone.txt")?, path("old-link")?],
            jobs: vec![Job::Whole {
                path: path("added.txt")?,
            }],
        };
        ensure!(plan(&base, &target) == expected);
        Ok(())
    }

    #[test]
    fn pairs_a_renamed_bundle_with_its_old_name() -> Result<()> {
        let base: Tree = tree(&[(OLD_BUNDLE, file('1', "644")?)])?;
        let target: Tree = tree(&[(NEW_BUNDLE, file('2', "644")?)])?;
        let expected: Plan = Plan {
            deleted: vec![path(OLD_BUNDLE)?],
            jobs: vec![Job::Diff {
                path: path(NEW_BUNDLE)?,
                from: path(OLD_BUNDLE)?,
            }],
        };
        ensure!(plan(&base, &target) == expected);
        Ok(())
    }

    #[test]
    fn ships_a_bundle_whole_when_its_old_name_is_ambiguous() -> Result<()> {
        let other: &str = "Data/aa/ui_assets_all_11111111111111111111111111111111.bundle";
        let base: Tree = tree(&[(OLD_BUNDLE, file('1', "644")?), (other, file('3', "644")?)])?;
        let target: Tree = tree(&[(NEW_BUNDLE, file('2', "644")?)])?;
        ensure!(
            plan(&base, &target).jobs
                == vec![Job::Whole {
                    path: path(NEW_BUNDLE)?
                }]
        );
        Ok(())
    }

    #[test]
    fn ships_a_case_only_rename_whole_and_a_file_replacing_a_link_whole() -> Result<()> {
        let base: Tree = tree(&[("Plugin.dll", file('1', "644")?)])?;
        let target: Tree = tree(&[("plugin.dll", file('2', "644")?)])?;
        ensure!(
            plan(&base, &target)
                == Plan {
                    deleted: vec![path("Plugin.dll")?],
                    jobs: vec![Job::Whole {
                        path: path("plugin.dll")?
                    }]
                }
        );

        let linked: Tree = tree(&[("t", file('1', "644")?), ("Lib", Node::Link { target: "t".to_owned() })])?;
        let folder: Tree = tree(&[("t", file('1', "644")?), ("Lib/lib", file('2', "644")?)])?;
        ensure!(
            plan(&linked, &folder)
                == Plan {
                    deleted: vec![path("Lib")?],
                    jobs: vec![Job::Whole { path: path("Lib/lib")? }]
                }
        );
        Ok(())
    }

    #[test]
    fn mode_and_link_changes_need_no_job() -> Result<()> {
        let base: Tree = tree(&[
            ("run.sh", file('1', "644")?),
            ("t", file('2', "644")?),
            ("l", Node::Link { target: "t".to_owned() }),
        ])?;
        let target: Tree = tree(&[
            ("run.sh", file('1', "755")?),
            ("t", file('2', "644")?),
            (
                "l",
                Node::Link {
                    target: "run.sh".to_owned(),
                },
            ),
        ])?;
        ensure!(
            plan(&base, &target)
                == Plan {
                    deleted: vec![],
                    jobs: vec![]
                }
        );
        Ok(())
    }

    #[test]
    fn whole_file_rule() -> Result<()> {
        ensure!(!needs_whole_trial(100, 49));
        ensure!(needs_whole_trial(100, 50));
        ensure!(needs_whole_trial(0, 0));
        ensure!(prefer_whole(10, 9));
        ensure!(!prefer_whole(10, 10));
        Ok(())
    }

    #[test]
    fn patch_files_live_under_the_base_tag_and_the_build_id() -> Result<()> {
        let base: crate::releases::tag::Tag = "v1.0.0".parse()?;
        ensure!(
            patch_file(&base, "0123abcd", &path("Data/space dir/a.bin")?)?.as_str()
                == "from/v1.0.0/0123abcd/Data/space dir/a.bin.hdiff"
        );
        ensure!(
            patch_file(&base, "", &path("a")?).is_err(),
            "an empty build id is refused"
        );
        Ok(())
    }
}
