//! The entries of a release archive, checked before anything is written.

use std::collections::{BTreeMap, BTreeSet};

use anyhow::{Context, Result, bail};

use crate::builds::tree::{check_link, leaf_ancestor};
use crate::builds::values::{Mode, RelPath};

/// One archive entry.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Member {
    Folder(RelPath),
    File { path: RelPath, mode: Mode },
    Link { path: RelPath, target: String },
}

impl Member {
    pub fn path(&self) -> &RelPath {
        match self {
            Member::Folder(path) | Member::File { path, .. } | Member::Link { path, .. } => path,
        }
    }
}

/// The path with its case folded, as NTFS and APFS compare it.
fn folded(path: &RelPath) -> Result<RelPath> {
    RelPath::try_new(path.as_str().to_lowercase()).with_context(|| format!("cannot fold the case of {path}"))
}

/// Extraction writes only inside its folder when no two entries share a path, no entry lies
/// under a file or link entry (both ignoring case, as on NTFS and APFS), and no link can leave
/// the folder.
pub fn check_members(members: &[Member]) -> Result<()> {
    let mut seen: BTreeSet<RelPath> = BTreeSet::new();
    let mut leaves: BTreeMap<RelPath, &Member> = BTreeMap::new();
    for member in members {
        let key: RelPath = folded(member.path())?;
        if !seen.insert(key.clone()) {
            bail!("archive lists {} more than once (ignoring case)", member.path());
        }
        if !matches!(member, Member::Folder(_)) {
            leaves.insert(key, member);
        }
    }
    for member in members {
        if let Some(parent) = leaf_ancestor(&folded(member.path())?, &leaves) {
            bail!("archive entry {} lies under the file or link {parent}", member.path());
        }
        if let Member::Link { path, target } = member {
            check_link(path, target)?;
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use anyhow::{Result, ensure};

    use super::{Member, check_members};
    use crate::builds::values::{Mode, RelPath};

    fn path(text: &str) -> Result<RelPath> {
        Ok(RelPath::try_new(text.to_owned())?)
    }

    fn file(text: &str) -> Result<Member> {
        Ok(Member::File {
            path: path(text)?,
            mode: Mode::try_new("644".to_owned())?,
        })
    }

    fn link(text: &str, target: &str) -> Result<Member> {
        Ok(Member::Link {
            path: path(text)?,
            target: target.to_owned(),
        })
    }

    #[test]
    fn accepts_a_normal_build() -> Result<()> {
        let members: Vec<Member> = vec![Member::Folder(path("Fw")?), file("Fw/A/lib")?, link("Fw/Current", "A")?];
        check_members(&members)
    }

    #[test]
    fn refuses_writing_through_a_link() -> Result<()> {
        ensure!(check_members(&[link("lnk", "sub")?, file("lnk/pwned")?]).is_err());
        Ok(())
    }

    #[test]
    fn refuses_a_link_leaving_the_folder() -> Result<()> {
        ensure!(check_members(&[link("lnk", "../escape")?]).is_err());
        ensure!(check_members(&[link("lnk", "/absolute")?]).is_err());
        Ok(())
    }

    #[test]
    fn refuses_writing_under_a_link_whatever_the_case() -> Result<()> {
        // On APFS `d/X` and `d/x` are the same name; `d/x` resolves above the folder.
        let members: Vec<Member> = vec![link("d/up", "..")?, link("d/x", "up/../..")?, file("d/X/evil")?];
        ensure!(check_members(&members).is_err());
        Ok(())
    }

    #[test]
    fn refuses_duplicates_including_case_variants() -> Result<()> {
        ensure!(check_members(&[link("a", "b")?, file("a")?]).is_err());
        ensure!(check_members(&[file("Plugin.dll")?, file("plugin.dll")?]).is_err());
        Ok(())
    }
}
