//! The launcher's order for applying a patch set, and how a rebuilt build is compared.

use std::fmt;

use crate::builds::tree::{Node, Tree};
use crate::builds::values::{Mode, RelPath, Sha256};
use crate::patching::delta::{Delta, Patch, Whole};

/// One action of the launcher's replay.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Action {
    Delete(RelPath),
    Diff {
        from: RelPath,
        file: RelPath,
        sha256: Sha256,
        path: RelPath,
    },
    Whole {
        file: RelPath,
        sha256: Sha256,
        path: RelPath,
    },
    Link {
        path: RelPath,
        target: String,
    },
    Mode {
        path: RelPath,
        mode: Mode,
    },
}

/// Deletes first, so a case-only rename or a folder replaced by a link cannot remove new
/// files. Then patches (read from the untouched base) and whole files. Then every link and mode
/// of the target build: an unchanged file can still change its mode, a link its target.
pub fn replay_actions(delta: &Delta, target: &Tree) -> Vec<Action> {
    let mut actions: Vec<Action> = Vec::new();
    for path in delta.deleted() {
        actions.push(Action::Delete(path.clone()));
    }
    for patch in delta.patches() {
        let Patch {
            path,
            from,
            file,
            sha256,
            ..
        } = patch;
        actions.push(Action::Diff {
            from: from.clone(),
            file: file.clone(),
            sha256: sha256.clone(),
            path: path.clone(),
        });
    }
    for whole in delta.whole() {
        let Whole { path, file, sha256, .. } = whole;
        actions.push(Action::Whole {
            file: file.clone(),
            sha256: sha256.clone(),
            path: path.clone(),
        });
    }
    for (path, node) in target.entries() {
        match node {
            Node::Link { target: link_target } => actions.push(Action::Link {
                path: path.clone(),
                target: link_target.clone(),
            }),
            Node::File { mode, .. } => actions.push(Action::Mode {
                path: path.clone(),
                mode: mode.clone(),
            }),
        }
    }
    actions
}

/// How a rebuilt build differs from the target build.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Difference {
    Missing(RelPath),
    Unexpected(RelPath),
    Changed(RelPath),
}

impl fmt::Display for Difference {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Difference::Missing(path) => write!(f, "missing {path}"),
            Difference::Unexpected(path) => write!(f, "unexpected {path}"),
            Difference::Changed(path) => write!(f, "changed {path}"),
        }
    }
}

fn same(want: &Node, got: &Node, compare_modes: bool) -> bool {
    match (want, got) {
        (
            Node::File {
                size: want_size,
                sha256: want_sha256,
                mode: want_mode,
            },
            Node::File {
                size: got_size,
                sha256: got_sha256,
                mode: got_mode,
            },
        ) => want_size == got_size && want_sha256 == got_sha256 && (!compare_modes || want_mode == got_mode),
        (Node::Link { target: want_target }, Node::Link { target: got_target }) => want_target == got_target,
        _ => false,
    }
}

/// Every path where `got` differs from `want`. Modes are compared only when the file system
/// keeps them (not on Windows).
pub fn differences(want: &Tree, got: &Tree, compare_modes: bool) -> Vec<Difference> {
    let mut found: Vec<Difference> = Vec::new();
    for (path, node) in want.entries() {
        match got.get(path) {
            None => found.push(Difference::Missing(path.clone())),
            Some(other) if !same(node, other, compare_modes) => found.push(Difference::Changed(path.clone())),
            Some(_) => {}
        }
    }
    for path in got.entries().keys() {
        if want.get(path).is_none() {
            found.push(Difference::Unexpected(path.clone()));
        }
    }
    found
}

#[cfg(test)]
mod tests {
    use std::collections::BTreeMap;

    use anyhow::{Result, ensure};

    use super::{Action, Difference, differences, replay_actions};
    use crate::builds::tree::{Node, Tree};
    use crate::builds::values::{Mode, RelPath, Sha256};
    use crate::patching::delta::{Delta, Patch, Whole};

    fn path(text: &str) -> Result<RelPath> {
        Ok(RelPath::try_new(text.to_owned())?)
    }

    fn sha(digit: char) -> Result<Sha256> {
        Ok(Sha256::try_new(digit.to_string().repeat(64))?)
    }

    fn file(digit: char, mode: &str) -> Result<Node> {
        Ok(Node::File {
            size: 1,
            sha256: sha(digit)?,
            mode: Mode::try_new(mode.to_owned())?,
        })
    }

    fn tree(entries: Vec<(&str, Node)>) -> Result<Tree> {
        let mut map: BTreeMap<RelPath, Node> = BTreeMap::new();
        for (text, node) in entries {
            map.insert(path(text)?, node);
        }
        Tree::new(map)
    }

    #[test]
    fn deletes_first_then_patches_then_whole_then_links_and_modes() -> Result<()> {
        let patch: Patch = Patch {
            path: path("a")?,
            from: path("a")?,
            from_sha256: sha('1')?,
            file: path("from/v1.0.0/a.hdiff")?,
            size: 1,
            sha256: sha('2')?,
        };
        let whole: Whole = Whole {
            path: path("b")?,
            file: path("from/v1.0.0/b.hdiff")?,
            size: 1,
            sha256: sha('3')?,
        };
        let delta: Delta = Delta::new(vec![path("gone")?], vec![patch], vec![whole])?;
        let target: Tree = tree(vec![
            ("a", file('4', "644")?),
            ("b", file('5', "755")?),
            ("c", Node::Link { target: "a".to_owned() }),
        ])?;
        let expected: Vec<Action> = vec![
            Action::Delete(path("gone")?),
            Action::Diff {
                from: path("a")?,
                file: path("from/v1.0.0/a.hdiff")?,
                sha256: sha('2')?,
                path: path("a")?,
            },
            Action::Whole {
                file: path("from/v1.0.0/b.hdiff")?,
                sha256: sha('3')?,
                path: path("b")?,
            },
            Action::Mode {
                path: path("a")?,
                mode: Mode::try_new("644".to_owned())?,
            },
            Action::Mode {
                path: path("b")?,
                mode: Mode::try_new("755".to_owned())?,
            },
            Action::Link {
                path: path("c")?,
                target: "a".to_owned(),
            },
        ];
        ensure!(replay_actions(&delta, &target) == expected);
        Ok(())
    }

    #[test]
    fn lists_every_difference() -> Result<()> {
        let want: Tree = tree(vec![
            ("same", file('1', "644")?),
            ("changed", file('1', "644")?),
            ("missing", file('1', "644")?),
            ("mode", file('1', "755")?),
        ])?;
        let got: Tree = tree(vec![
            ("same", file('1', "644")?),
            ("changed", file('2', "644")?),
            ("extra", file('1', "644")?),
            ("mode", file('1', "644")?),
        ])?;
        let expected: Vec<Difference> = vec![
            Difference::Changed(path("changed")?),
            Difference::Missing(path("missing")?),
            Difference::Changed(path("mode")?),
            Difference::Unexpected(path("extra")?),
        ];
        ensure!(differences(&want, &got, true) == expected);
        ensure!(!differences(&want, &got, false).contains(&Difference::Changed(path("mode")?)));
        Ok(())
    }
}
