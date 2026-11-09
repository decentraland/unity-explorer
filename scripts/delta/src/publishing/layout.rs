//! Where releases and patch sets live in the bucket.

use std::path::PathBuf;

use serde::{Deserialize, Serialize};

use crate::publishing::values::ZipName;
use crate::releases::tag::{Platform, Tag};

/// `<releases>/<tag>/<zip>`: a release's full zip.
pub fn zip_key(releases: &str, tag: &Tag, zip: &ZipName) -> String {
    format!("{releases}/{tag}/{zip}")
}

/// `from/<base>`: everything a patch set from `base` holds, relative to the manifest's folder.
pub fn base_folder(base: &Tag) -> String {
    format!("from/{base}")
}

/// `from/<base>/<build id>`: the patches of one release build from `base`.
pub fn patch_folder(base: &Tag, build_id: &str) -> String {
    format!("{}/{build_id}", base_folder(base))
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
