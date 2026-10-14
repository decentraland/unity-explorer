//! `manifest.json`, the published contract with the launcher (docs/delta-updates.md). Kept apart
//! from the domain `Manifest` so a domain change cannot change the file launchers read.

use std::collections::BTreeMap;

use serde::{Deserialize, Serialize};

use crate::builds::tree::Node;
use crate::patching::delta::Delta;
use crate::publishing::manifest::Manifest;

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ManifestDto {
    pub version: String,
    pub platform: String,
    pub zip: ZipDto,
    pub files: Vec<FileDto>,
    pub bases: BTreeMap<String, DeltaDto>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct ZipDto {
    pub path: String,
    pub size: u64,
    pub sha256: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(untagged, deny_unknown_fields)]
pub enum FileDto {
    File {
        path: String,
        size: u64,
        sha256: String,
        mode: String,
    },
    Link {
        path: String,
        symlink: String,
    },
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct DeltaDto {
    pub deleted: Vec<String>,
    pub patches: Vec<PatchDto>,
    pub whole: Vec<WholeDto>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct PatchDto {
    pub path: String,
    pub from_path: String,
    pub from_sha256: String,
    pub patch: String,
    pub patch_size: u64,
    pub patch_sha256: String,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WholeDto {
    pub path: String,
    pub patch: String,
    pub patch_size: u64,
    pub patch_sha256: String,
}

fn delta_dto(delta: &Delta) -> DeltaDto {
    DeltaDto {
        deleted: delta.deleted().iter().map(ToString::to_string).collect(),
        patches: delta
            .patches()
            .iter()
            .map(|p: &crate::patching::delta::Patch| PatchDto {
                path: p.path.to_string(),
                from_path: p.from.to_string(),
                from_sha256: p.from_sha256.to_string(),
                patch: p.file.to_string(),
                patch_size: p.size,
                patch_sha256: p.sha256.to_string(),
            })
            .collect(),
        whole: delta
            .whole()
            .iter()
            .map(|w: &crate::patching::delta::Whole| WholeDto {
                path: w.path.to_string(),
                patch: w.file.to_string(),
                patch_size: w.size,
                patch_sha256: w.sha256.to_string(),
            })
            .collect(),
    }
}

impl From<&Manifest> for ManifestDto {
    fn from(manifest: &Manifest) -> ManifestDto {
        let mut files: Vec<FileDto> = Vec::new();
        for (path, node) in manifest.files().entries() {
            files.push(match node {
                Node::File { size, sha256, mode } => FileDto::File {
                    path: path.to_string(),
                    size: *size,
                    sha256: sha256.to_string(),
                    mode: mode.to_string(),
                },
                Node::Link { target } => FileDto::Link {
                    path: path.to_string(),
                    symlink: target.clone(),
                },
            });
        }
        let mut bases: BTreeMap<String, DeltaDto> = BTreeMap::new();
        for (tag, delta) in manifest.deltas() {
            bases.insert(tag.to_string(), delta_dto(delta));
        }
        ManifestDto {
            version: manifest.release().to_string(),
            platform: manifest.platform().to_string(),
            zip: ZipDto {
                path: manifest.zip().name.to_string(),
                size: manifest.zip().size,
                sha256: manifest.zip().sha256.to_string(),
            },
            files,
            bases,
        }
    }
}

#[cfg(test)]
mod tests {
    use anyhow::{Result, ensure};

    use super::ManifestDto;

    /// The exact JSON the launcher reads; a change here is a change to the launcher contract.
    #[test]
    fn the_launcher_contract_is_pinned() -> Result<()> {
        let sha: String = "a".repeat(64);
        let json: String = format!(
            r#"{{"version":"v1.1.0-alpha","platform":"windows64","zip":{{"path":"Decentraland_windows64.zip","size":3,"sha256":"{sha}"}},"files":[{{"path":"a.dll","size":1,"sha256":"{sha}","mode":"644"}},{{"path":"l","symlink":"a.dll"}}],"bases":{{"v1.0.0-alpha":{{"deleted":["b"],"patches":[{{"path":"a.dll","from_path":"a.dll","from_sha256":"{sha}","patch":"from/v1.0.0-alpha/aaaaaaaa/a.dll.hdiff","patch_size":1,"patch_sha256":"{sha}"}}],"whole":[{{"path":"c","patch":"from/v1.0.0-alpha/aaaaaaaa/c.hdiff","patch_size":2,"patch_sha256":"{sha}"}}]}}}}}}"#
        );
        let dto: ManifestDto = serde_json::from_str(&json)?;
        ensure!(serde_json::to_value(&dto)? == serde_json::from_str::<serde_json::Value>(&json)?);
        Ok(())
    }
}
