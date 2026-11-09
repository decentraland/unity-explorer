//! Simple types of publishing.

use nutype::nutype;
use serde::{Deserialize, Serialize};

use crate::builds::values::Sha256;

fn is_file_name(value: &str) -> bool {
    !value.is_empty()
        && value != "."
        && value != ".."
        && value
            .chars()
            .all(|c: char| c.is_ascii_alphanumeric() || c == '_' || c == '.' || c == '-')
}

/// A plain file name, e.g. `Decentraland_windows64.zip`.
#[nutype(
    validate(predicate = is_file_name),
    derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord, Hash, AsRef, Display, FromStr, Serialize, Deserialize)
)]
pub struct ZipName(String);

/// The S3 entity tag of an object version.
#[nutype(
    sanitize(trim),
    validate(not_empty),
    derive(Debug, Clone, PartialEq, Eq, AsRef, Display, Serialize, Deserialize)
)]
pub struct ETag(String);

/// The published full zip of a release.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ZipRef {
    pub name: ZipName,
    pub size: u64,
    pub sha256: Sha256,
}
