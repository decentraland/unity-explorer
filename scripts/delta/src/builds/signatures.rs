//! Which artifacts of a build are signed, and whether a rebuilt build verifies like the new one.

use std::collections::BTreeMap;
use std::path::Path;

use crate::builds::tree::Tree;
use crate::builds::values::RelPath;
use crate::releases::tag::Platform;

/// Whether each signed artifact's signature verifies.
pub type Signatures = BTreeMap<RelPath, bool>;

fn has_extension(name: &str, extension: &str) -> bool {
    Path::new(name)
        .extension()
        .is_some_and(|found: &std::ffi::OsStr| found.eq_ignore_ascii_case(extension))
}

/// windows64: every `*.exe` file. macos: every top-level `*.app` bundle.
pub fn signed_artifacts(platform: Platform, tree: &Tree) -> Vec<RelPath> {
    let mut artifacts: Vec<RelPath> = Vec::new();
    for (path, _, _) in tree.files() {
        match platform {
            Platform::Windows64 => {
                if has_extension(path.as_str(), "exe") {
                    artifacts.push(path.clone());
                }
            }
            Platform::Macos => {
                let top: &str = path.as_str().split('/').next().unwrap_or_default();
                let nested: bool = path.as_str().contains('/');
                if nested
                    && has_extension(top, "app")
                    && let Ok(app) = RelPath::try_new(top.to_owned())
                    && !artifacts.contains(&app)
                {
                    artifacts.push(app);
                }
            }
        }
    }
    artifacts
}

/// Artifacts whose verification result differs between the new and the rebuilt build.
pub fn signature_changes(new: &Signatures, rebuilt: &Signatures) -> Vec<RelPath> {
    let mut changed: Vec<RelPath> = Vec::new();
    for (artifact, valid) in new {
        if rebuilt.get(artifact) != Some(valid) {
            changed.push(artifact.clone());
        }
    }
    for artifact in rebuilt.keys() {
        if !new.contains_key(artifact) {
            changed.push(artifact.clone());
        }
    }
    changed
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeMap;

    use anyhow::{Result, ensure};

    use super::{Signatures, signature_changes, signed_artifacts};
    use crate::builds::tree::{Node, Tree};
    use crate::builds::values::{Mode, RelPath, Sha256};
    use crate::releases::tag::Platform;

    fn tree(paths: &[&str]) -> Result<Tree> {
        let mut entries: BTreeMap<RelPath, Node> = BTreeMap::new();
        for path in paths {
            let node: Node = Node::File {
                size: 1,
                sha256: Sha256::try_new("a".repeat(64))?,
                mode: Mode::try_new("644".to_owned())?,
            };
            entries.insert(RelPath::try_new((*path).to_owned())?, node);
        }
        Tree::new(entries)
    }

    #[test]
    fn signed_artifacts_per_platform() -> Result<()> {
        let windows: Tree = tree(&["Decentraland.exe", "sub/Helper.exe", "readme.txt"])?;
        let found: Vec<String> = signed_artifacts(Platform::Windows64, &windows)
            .iter()
            .map(ToString::to_string)
            .collect();
        ensure!(found == ["Decentraland.exe", "sub/Helper.exe"]);
        let macos: Tree = tree(&[
            "Decentraland.app/Contents/Info.plist",
            "Decentraland.app/Contents/x",
            "Other.app/x",
            "x.app",
        ])?;
        let apps: Vec<String> = signed_artifacts(Platform::Macos, &macos)
            .iter()
            .map(ToString::to_string)
            .collect();
        ensure!(apps == ["Decentraland.app", "Other.app"]);
        Ok(())
    }

    #[test]
    fn changes_cover_flipped_missing_and_extra_artifacts() -> Result<()> {
        let mut new: Signatures = BTreeMap::new();
        new.insert(RelPath::try_new("a.exe".to_owned())?, true);
        new.insert(RelPath::try_new("b.exe".to_owned())?, true);
        ensure!(signature_changes(&new, &new).is_empty());
        let mut rebuilt: Signatures = BTreeMap::new();
        rebuilt.insert(RelPath::try_new("a.exe".to_owned())?, false);
        rebuilt.insert(RelPath::try_new("c.exe".to_owned())?, true);
        let changed: Vec<String> = signature_changes(&new, &rebuilt)
            .iter()
            .map(ToString::to_string)
            .collect();
        ensure!(changed == ["a.exe", "b.exe", "c.exe"]);
        Ok(())
    }
}
