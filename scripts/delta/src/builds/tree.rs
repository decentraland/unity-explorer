//! What a build contains: every file and link, keyed by path.

use std::collections::BTreeMap;
use std::path::PathBuf;

use anyhow::{Result, bail, ensure};
use relative_path::{Component, RelativePath, RelativePathBuf};
use serde::{Deserialize, Serialize};

use crate::builds::values::{Mode, RelPath, Sha256};

/// One entry of a build. Folders are implied by the paths.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "lowercase")]
pub enum Node {
    File { size: u64, sha256: Sha256, mode: Mode },
    Link { target: String },
}

/// Every file and link of a build. `Tree::new` refuses an entry inside a file or link, and a
/// link that can leave the build.
#[derive(Debug, Clone, PartialEq, Eq, Default, Serialize, Deserialize)]
pub struct Tree(BTreeMap<RelPath, Node>);

/// An extracted build on disk and what it contains.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Build {
    pub root: PathBuf,
    pub tree: Tree,
}

impl Tree {
    pub fn new(entries: BTreeMap<RelPath, Node>) -> Result<Tree> {
        for (path, node) in &entries {
            if let Some(parent) = leaf_ancestor(path, &entries) {
                bail!("{path} is inside {parent}, which is a file or link");
            }
            if let Node::Link { target } = node {
                check_link(path, target)?;
            }
        }
        Ok(Tree(entries))
    }

    pub fn entries(&self) -> &BTreeMap<RelPath, Node> {
        &self.0
    }

    pub fn get(&self, path: &RelPath) -> Option<&Node> {
        self.0.get(path)
    }

    /// Paths and sizes of the regular files.
    pub fn files(&self) -> Vec<(&RelPath, u64, &Sha256)> {
        let mut files: Vec<(&RelPath, u64, &Sha256)> = Vec::new();
        for (path, node) in &self.0 {
            if let Node::File { size, sha256, .. } = node {
                files.push((path, *size, sha256));
            }
        }
        files
    }
}

/// The nearest ancestor of `path` listed as a file or link.
pub(crate) fn leaf_ancestor<'a, V>(path: &RelPath, entries: &'a BTreeMap<RelPath, V>) -> Option<&'a RelPath> {
    let mut current: &RelativePath = RelativePath::new(path.as_str());
    while let Some(parent) = current.parent() {
        if parent.as_str().is_empty() {
            return None;
        }
        if let Ok(key) = RelPath::try_new(parent.as_str().to_owned())
            && let Some((found, _)) = entries.get_key_value(&key)
        {
            return Some(found);
        }
        current = parent;
    }
    None
}

/// A link target must be relative, free of `\`, `:` and control characters, and must not climb
/// above the build root when resolved from the link's folder. Links through other links are
/// checked on disk by the scan.
pub fn check_link(path: &RelPath, target: &str) -> Result<()> {
    let unsafe_char: bool = target.chars().any(|c: char| c.is_control() || c == '\\' || c == ':');
    ensure!(
        !unsafe_char,
        "unsupported character in the target of link {path}: {target:?}"
    );
    ensure!(
        !target.is_empty() && !target.starts_with('/'),
        "absolute or empty target of link {path}: {target:?}"
    );
    let folder: &RelativePath = RelativePath::new(path.as_str())
        .parent()
        .unwrap_or(RelativePath::new(""));
    let resolved: RelativePathBuf = folder.join_normalized(target);
    let climbs: bool = matches!(resolved.components().next(), Some(Component::ParentDir));
    ensure!(!climbs, "link leaves the build: {path} -> {target}");
    Ok(())
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeMap;

    use anyhow::{Result, ensure};
    use rstest::rstest;

    use super::{Node, Tree, check_link};
    use crate::builds::values::{Mode, RelPath, Sha256};

    fn path(text: &str) -> Result<RelPath> {
        Ok(RelPath::try_new(text.to_owned())?)
    }

    fn file() -> Result<Node> {
        Ok(Node::File {
            size: 1,
            sha256: Sha256::try_new("a".repeat(64))?,
            mode: Mode::try_new("644".to_owned())?,
        })
    }

    #[test]
    fn refuses_an_entry_inside_a_file_or_link() -> Result<()> {
        let mut inside_file: BTreeMap<RelPath, Node> = BTreeMap::new();
        inside_file.insert(path("a")?, file()?);
        inside_file.insert(path("a/b")?, file()?);
        ensure!(Tree::new(inside_file).is_err());

        let mut inside_link: BTreeMap<RelPath, Node> = BTreeMap::new();
        inside_link.insert(path("Lib")?, Node::Link { target: "x".to_owned() });
        inside_link.insert(path("Lib/lib")?, file()?);
        ensure!(Tree::new(inside_link).is_err());
        Ok(())
    }

    #[test]
    fn accepts_nested_files() -> Result<()> {
        let mut entries: BTreeMap<RelPath, Node> = BTreeMap::new();
        entries.insert(path("a/b")?, file()?);
        entries.insert(path("a/c/d")?, file()?);
        entries.insert(path("a/link")?, Node::Link { target: "c".to_owned() });
        ensure!(Tree::new(entries)?.files().len() == 2);
        Ok(())
    }

    #[rstest]
    #[case("sub/link", "../sibling", true)]
    #[case("sub/up", "..", true)]
    #[case("Fw/Versions/Current", "A", true)]
    #[case("sub/link", "../../outside", false)]
    #[case("link", "..", false)]
    #[case("a/b", "./../../x", false)]
    #[case("a/b", "c/../../..", false)]
    #[case("sub/link", "/etc/passwd", false)]
    #[case("sub/link", "", false)]
    #[case("sub/link", "x\nF\tevil", false)]
    #[case("sub/link", "..\\x", false)]
    fn link_containment(#[case] link: &str, #[case] target: &str, #[case] allowed: bool) -> Result<()> {
        ensure!(
            check_link(&path(link)?, target).is_ok() == allowed,
            "{link} -> {target}"
        );
        Ok(())
    }

    #[test]
    fn round_trips_through_json() -> Result<()> {
        let mut entries: BTreeMap<RelPath, Node> = BTreeMap::new();
        entries.insert(path("a")?, file()?);
        entries.insert(path("l")?, Node::Link { target: "a".to_owned() });
        let tree: Tree = Tree::new(entries)?;
        let text: String = serde_json::to_string(&tree)?;
        let read: Tree = serde_json::from_str(&text)?;
        ensure!(read == tree);
        Ok(())
    }
}
