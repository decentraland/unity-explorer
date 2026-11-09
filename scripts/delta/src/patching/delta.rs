//! The patch set from one base: what to delete and how to rebuild each changed file.

use std::collections::BTreeSet;

use anyhow::{Result, ensure};
use serde::{Deserialize, Serialize};

use crate::builds::values::{RelPath, Sha256};

/// `path` is rebuilt by applying `file` to `from` in the base.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Patch {
    pub path: RelPath,
    pub from: RelPath,
    pub from_sha256: Sha256,
    pub file: RelPath,
    pub size: u64,
    pub sha256: Sha256,
}

/// `path` is rebuilt by applying `file` to an empty file.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Whole {
    pub path: RelPath,
    pub file: RelPath,
    pub size: u64,
    pub sha256: Sha256,
}

/// `Delta::new` refuses a path rebuilt twice; deserialization goes through it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(try_from = "DeltaParts")]
pub struct Delta {
    deleted: Vec<RelPath>,
    patches: Vec<Patch>,
    whole: Vec<Whole>,
}

/// The serialized shape of a `Delta`, before its rule is applied.
#[derive(Debug, Deserialize)]
struct DeltaParts {
    deleted: Vec<RelPath>,
    patches: Vec<Patch>,
    whole: Vec<Whole>,
}

impl TryFrom<DeltaParts> for Delta {
    type Error = anyhow::Error;

    fn try_from(parts: DeltaParts) -> Result<Delta> {
        Delta::new(parts.deleted, parts.patches, parts.whole)
    }
}

impl Delta {
    pub fn new(deleted: Vec<RelPath>, patches: Vec<Patch>, whole: Vec<Whole>) -> Result<Delta> {
        let mut rebuilt: BTreeSet<&RelPath> = BTreeSet::new();
        for path in patches
            .iter()
            .map(|p: &Patch| &p.path)
            .chain(whole.iter().map(|w: &Whole| &w.path))
        {
            ensure!(rebuilt.insert(path), "{path} is rebuilt more than once");
        }
        Ok(Delta {
            deleted,
            patches,
            whole,
        })
    }

    pub fn deleted(&self) -> &[RelPath] {
        &self.deleted
    }

    pub fn patches(&self) -> &[Patch] {
        &self.patches
    }

    pub fn whole(&self) -> &[Whole] {
        &self.whole
    }

    /// Bytes a launcher downloads for this patch set.
    pub fn download_size(&self) -> u64 {
        let patches: u64 = self.patches.iter().map(|p: &Patch| p.size).fold(0, u64::saturating_add);
        let whole: u64 = self.whole.iter().map(|w: &Whole| w.size).fold(0, u64::saturating_add);
        patches.saturating_add(whole)
    }

    /// Every path the patch set rebuilds.
    pub fn rebuilt(&self) -> Vec<&RelPath> {
        self.patches
            .iter()
            .map(|p: &Patch| &p.path)
            .chain(self.whole.iter().map(|w: &Whole| &w.path))
            .collect()
    }
}

#[cfg(test)]
mod tests {
    use anyhow::{Result, ensure};

    use super::{Delta, Whole};
    use crate::builds::values::{RelPath, Sha256};

    #[test]
    fn refuses_a_path_rebuilt_twice() -> Result<()> {
        let whole: Whole = Whole {
            path: RelPath::try_new("a".to_owned())?,
            file: RelPath::try_new("from/v1.0.0/a.hdiff".to_owned())?,
            size: 3,
            sha256: Sha256::try_new("a".repeat(64))?,
        };
        ensure!(Delta::new(vec![], vec![], vec![whole.clone(), whole.clone()]).is_err());
        let twice: String = serde_json::to_string(&serde_json::json!({
            "deleted": [], "patches": [], "whole": [whole, whole]
        }))?;
        ensure!(serde_json::from_str::<Delta>(&twice).is_err(), "read through the rule");
        ensure!(Delta::new(vec![], vec![], vec![whole])?.download_size() == 3);
        Ok(())
    }
}
