//! The entries of a release archive, checked before anything is written.

use std::collections::{BTreeMap, BTreeSet};

use anyhow::{Result, bail};

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

/// Extraction writes only inside its folder when no two entries share a path (also ignoring
/// case, as on NTFS and APFS), no entry lies under a file or link entry, and no link can leave
/// the folder.
pub fn check_members(members: &[Member]) -> Result<()> {
    let mut seen: BTreeSet<String> = BTreeSet::new();
    let mut leaves: BTreeMap<RelPath, &Member> = BTreeMap::new();
    for member in members {
        if !seen.insert(member.path().as_str().to_lowercase()) {
            bail!("archive lists {} more than once (ignoring case)", member.path());
        }
        if !matches!(member, Member::Folder(_)) {
            leaves.insert(member.path().clone(), member);
        }
    }
    for member in members {
        if let Some(parent) = leaf_ancestor(member.path(), &leaves) {
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
    fn refuses_duplicates_including_case_variants() -> Result<()> {
        ensure!(check_members(&[link("a", "b")?, file("a")?]).is_err());
        ensure!(check_members(&[file("Plugin.dll")?, file("plugin.dll")?]).is_err());
        Ok(())
    }
}
