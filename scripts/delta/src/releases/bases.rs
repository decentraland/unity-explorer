//! Values per base release, never empty.

use std::collections::BTreeMap;

use anyhow::{Result, ensure};
use serde::{Deserialize, Serialize};

use crate::releases::tag::Tag;

/// One value per base release. `Bases::new` refuses an empty map, so a stage holding `Bases`
/// always has at least one base left.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct Bases<T>(BTreeMap<Tag, T>);

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
