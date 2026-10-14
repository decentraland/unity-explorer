//! `HDiffPatch`: the pinned download and the `hdiffz` / `hpatchz` processes.

use std::env;
use std::fs::{self, File};
use std::path::{Path, PathBuf};

use anyhow::{Context, Result, ensure};

use crate::builds::disk::{remove, sha256_file};
use crate::shell::process::{path_text, run};

pub const VERSION: &str = "v5.1.3";
/// Patch size over speed; the same flags give the same patches on every run.
pub const HDIFFZ_ARGS: [&str; 2] = ["-m-6", "-c-lzma2-9-16m"];

/// Release asset and the sha256 of its zip, per OS.
fn pinned() -> Result<(&'static str, &'static str)> {
    match (env::consts::OS, env::consts::ARCH) {
        ("linux", "x86_64") => Ok((
            "linux64",
            "628963bf2ee9108a97260fa5eef44acd9ec94369b76090a957c9182b3abbb558",
        )),
        ("macos", _) => Ok((
            "macos",
            "9e9d7318db1ea5607dbdde41e6614cfe5b73c589034b38ee91a79f4ca80a940c",
        )),
        ("windows", _) => Ok((
            "windows64",
            "77f141386e5d8f785c1c846e10fbbc19b6c05aa00e3f59cc44670fb3f0e2ae94",
        )),
        (os, arch) => anyhow::bail!("no pinned HDiffPatch build for {os}/{arch}"),
    }
}

#[cfg(unix)]
fn make_executable(path: &Path) -> Result<()> {
    use std::os::unix::fs::PermissionsExt;
    fs::set_permissions(path, fs::Permissions::from_mode(0o755))
        .with_context(|| format!("cannot chmod {}", path.display()))
}

#[cfg(not(unix))]
#[allow(clippy::unnecessary_wraps)]
fn make_executable(_path: &Path) -> Result<()> {
    Ok(())
}

/// Installs the pinned `HDiffPatch` build for this OS into `tools`, checked against its pinned
/// sha256, and returns the folder holding `hdiffz` and `hpatchz`.
pub fn fetch_tools(tools: &Path) -> Result<PathBuf> {
    let (asset, sha256): (&str, &str) = pinned()?;
    fs::create_dir_all(tools).with_context(|| format!("cannot create {}", tools.display()))?;
    let archive: PathBuf = tools.join(format!("hdiffpatch_{VERSION}_bin_{asset}.zip"));
    let cached: bool = archive.is_file() && sha256_file(&archive)?.to_string() == sha256;
    if !cached {
        let url: String = format!(
            "https://github.com/sisong/HDiffPatch/releases/download/{VERSION}/hdiffpatch_{VERSION}_bin_{asset}.zip"
        );
        let out: &str = archive
            .to_str()
            .with_context(|| format!("non-UTF-8 path {}", archive.display()))?;
        run("curl", ["-fsSL", "--retry", "3", "-o", out, &url])?;
    }
    let actual: String = sha256_file(&archive)?.to_string();
    if actual != sha256 {
        remove(&archive)?;
        anyhow::bail!("HDiffPatch sha256 mismatch: got {actual}, want {sha256}");
    }
    let bin: PathBuf = tools.join(asset);
    remove(&bin)?;
    let mut zipped: zip::ZipArchive<File> = zip::ZipArchive::new(File::open(&archive)?)?;
    zipped
        .extract(tools)
        .with_context(|| format!("cannot unpack {}", archive.display()))?;
    ensure!(bin.is_dir(), "unexpected HDiffPatch archive layout: no {asset}/ folder");
    for item in fs::read_dir(&bin)? {
        let tool: PathBuf = item?.path();
        let name: String = tool
            .file_name()
            .map(|n: &std::ffi::OsStr| n.to_string_lossy().into_owned())
            .unwrap_or_default();
        if name.starts_with("hdiffz") || name.starts_with("hpatchz") {
            make_executable(&tool)?;
        }
    }
    std::path::absolute(&bin).with_context(|| format!("cannot resolve {}", bin.display()))
}

/// `hdiffz` and `hpatchz`: from `$HDIFFZ` / `$HPATCHZ`, else from `PATH`.
#[derive(Debug, Clone)]
pub struct HDiffPatch {
    hdiffz: String,
    hpatchz: String,
}

impl HDiffPatch {
    pub fn from_env() -> HDiffPatch {
        HDiffPatch {
            hdiffz: env::var("HDIFFZ").unwrap_or_else(|_| "hdiffz".to_owned()),
            hpatchz: env::var("HPATCHZ").unwrap_or_else(|_| "hpatchz".to_owned()),
        }
    }

    /// Writes a patch turning `old` into `new`.
    pub fn diff(&self, old: &Path, new: &Path, patch: &Path) -> Result<()> {
        let [memory, compression]: [&str; 2] = HDIFFZ_ARGS;
        run(
            &self.hdiffz,
            [
                "-f",
                memory,
                compression,
                path_text(old)?,
                path_text(new)?,
                path_text(patch)?,
            ],
        )?;
        Ok(())
    }

    /// Applies `patch` to `old`, writing `out`.
    pub fn patch(&self, old: &Path, patch: &Path, out: &Path) -> Result<()> {
        run(
            &self.hpatchz,
            ["-f", path_text(old)?, path_text(patch)?, path_text(out)?],
        )?;
        Ok(())
    }
}
