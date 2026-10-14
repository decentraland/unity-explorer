//! Values per base release, never empty.

use std::collections::BTreeMap;

use anyhow::{Result, ensure};
use serde::{Deserialize, Serialize};

use crate::releases::tag::Tag;

/// One value per base release. `Bases::new` refuses an empty map, so a state holding `Bases`
/// always has at least one base left; deserialization goes through it.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(try_from = "BTreeMap<Tag, T>")]
pub struct Bases<T>(BTreeMap<Tag, T>);

impl<T> TryFrom<BTreeMap<Tag, T>> for Bases<T> {
    type Error = anyhow::Error;

    fn try_from(values: BTreeMap<Tag, T>) -> Result<Bases<T>> {
        Bases::new(values)
    }
}

impl<T> Bases<T> {
    pub fn new(values: BTreeMap<Tag, T>) -> Result<Bases<T>> {
        ensure!(!values.is_empty(), "no base left");
        Ok(Bases(values))
    }

    pub fn iter(&self) -> std::collections::btree_map::Iter<'_, Tag, T> {
        self.0.iter()
    }

    pub fn keys(&self) -> std::collections::btree_map::Keys<'_, Tag, T> {
        self.0.keys()
    }

    pub fn contains(&self, tag: &Tag) -> bool {
        self.0.contains_key(tag)
    }

    pub fn into_map(self) -> BTreeMap<Tag, T> {
        self.0
    }
}

impl<'a, T> IntoIterator for &'a Bases<T> {
    type Item = (&'a Tag, &'a T);
    type IntoIter = std::collections::btree_map::Iter<'a, Tag, T>;

    fn into_iter(self) -> Self::IntoIter {
        self.0.iter()
    }
}

impl<T> IntoIterator for Bases<T> {
    type Item = (Tag, T);
    type IntoIter = std::collections::btree_map::IntoIter<Tag, T>;

    fn into_iter(self) -> Self::IntoIter {
        self.0.into_iter()
    }
}

#[cfg(test)]
mod tests {
    use anyhow::{Result, ensure};

    use super::Bases;

    #[test]
    fn an_empty_map_is_refused_when_read_from_json() -> Result<()> {
        ensure!(serde_json::from_str::<Bases<u32>>("{}").is_err());
        ensure!(serde_json::from_str::<Bases<u32>>(r#"{"v1.0.0":1}"#).is_ok());
        Ok(())
    }
}
