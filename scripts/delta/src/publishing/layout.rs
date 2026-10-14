//! Where releases and patch sets live in the bucket.

use std::path::PathBuf;

use serde::{Deserialize, Serialize};

use crate::publishing::values::ZipName;
use crate::releases::tag::{Platform, Tag};

/// `<releases>/<tag>/<zip>`: a release's full zip.
pub fn zip_key(releases: &str, tag: &Tag, zip: &ZipName) -> String {
    format!("{releases}/{tag}/{zip}")
}

/// `<releases>/<tag>/delta/<platform>`: the manifest and `from/<base>/` patches of a release.
pub fn delta_prefix(releases: &str, tag: &Tag, platform: Platform) -> String {
    format!("{releases}/{tag}/delta/{platform}")
}

/// Local folders uploaded under `prefix`: `patches` holds `from/<base>/**.hdiff`, `manifest`
/// holds `manifest.json`.
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct UploadLayout {
    pub prefix: String,
    pub patches: PathBuf,
    pub manifest: PathBuf,
}
