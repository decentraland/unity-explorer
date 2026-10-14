//! Builds on disk: hashing, scanning, extracting, copying and removing.

use std::collections::BTreeMap;
use std::fs::{self, File, OpenOptions};
use std::io;
use std::path::{Path, PathBuf};

use anyhow::{Context, Result, ensure};
use sha2::Digest;
use walkdir::{DirEntry, WalkDir};

use crate::builds::members::{Member, check_members};
use crate::builds::signatures::Signatures;
use crate::builds::tree::{Node, Tree};
use crate::builds::values::{Mode, RelPath, Sha256};
use crate::capabilities::Signer;
use crate::releases::tag::Platform;

const S_IFMT: u32 = 0o170_000;
const S_IFLNK: u32 = 0o120_000;
const DEFAULT_MODE: &str = "644";

pub fn sha256_file(path: &Path) -> Result<Sha256> {
    let mut file: File = File::open(path).with_context(|| format!("cannot open {}", path.display()))?;
    let mut hasher: sha2::Sha256 = sha2::Sha256::new();
    let mut buffer: Vec<u8> = vec![0; 1 << 20];
    loop {
        let read: usize =
            io::Read::read(&mut file, &mut buffer).with_context(|| format!("cannot read {}", path.display()))?;
        if read == 0 {
            break;
        }
        hasher.update(buffer.get(..read).unwrap_or_default());
    }
    let digest: String = hex::encode(hasher.finalize());
    Ok(Sha256::try_new(digest)?)
}

pub fn file_size(path: &Path) -> Result<u64> {
    Ok(fs::metadata(path)
        .with_context(|| format!("cannot read {}", path.display()))?
        .len())
}

/// The path of `entry` relative to `root`, `/`-separated.
fn rel_path(root: &Path, path: &Path) -> Result<RelPath> {
    let relative: &Path = path
        .strip_prefix(root)
        .with_context(|| format!("{} is outside {}", path.display(), root.display()))?;
    let mut segments: Vec<&str> = Vec::new();
    for component in relative.components() {
        let segment: &str = component
            .as_os_str()
            .to_str()
            .with_context(|| format!("non-UTF-8 path {}", path.display()))?;
        segments.push(segment);
    }
    let joined: String = segments.join("/");
    RelPath::try_new(joined.clone()).with_context(|| format!("unsafe path in build: {joined:?}"))
}

#[cfg(unix)]
fn mode_of(metadata: &fs::Metadata) -> Result<Mode> {
    use std::os::unix::fs::PermissionsExt;
    let bits: u32 = metadata.permissions().mode() & 0o7777;
    Ok(Mode::try_new(format!("{bits:03o}"))?)
}

#[cfg(not(unix))]
#[allow(clippy::unnecessary_wraps)]
fn mode_of(_metadata: &fs::Metadata) -> Result<Mode> {
    Ok(Mode::try_new(DEFAULT_MODE.to_owned())?)
}

/// The real folder a link's target lands in must exist and lie inside the build. The lexical
/// check in `Tree::new` misses a climb through another link (`d/x -> up/../..` with `d/up -> ..`).
fn check_link_on_disk(root: &Path, link: &Path, rel: &RelPath, target: &str) -> Result<()> {
    let link_folder: &Path = link.parent().with_context(|| format!("link {rel} has no folder"))?;
    let target_folder: PathBuf = if link.is_dir() {
        link_folder.join(target)
    } else {
        let target_path: &Path = Path::new(target);
        link_folder.join(target_path.parent().unwrap_or(Path::new("")))
    };
    let real_root: PathBuf = fs::canonicalize(root).with_context(|| format!("cannot resolve {}", root.display()))?;
    let real_target: PathBuf = fs::canonicalize(&target_folder)
        .with_context(|| format!("link target folder does not exist: {rel} -> {target}"))?;
    ensure!(
        real_target.starts_with(&real_root),
        "link leaves the build: {rel} -> {target}"
    );
    Ok(())
}

fn scan_entry(root: &Path, entry: &DirEntry, entries: &mut BTreeMap<RelPath, Node>) -> Result<()> {
    let path: &Path = entry.path();
    if entry.file_type().is_dir() {
        return Ok(());
    }
    let rel: RelPath = rel_path(root, path)?;
    if entry.file_type().is_symlink() {
        let target_path: PathBuf = fs::read_link(path).with_context(|| format!("cannot read link {rel}"))?;
        let target: String = target_path
            .to_str()
            .with_context(|| format!("non-UTF-8 target of link {rel}"))?
            .to_owned();
        crate::builds::tree::check_link(&rel, &target)?;
        check_link_on_disk(root, path, &rel, &target)?;
        entries.insert(rel, Node::Link { target });
        return Ok(());
    }
    let metadata: fs::Metadata = entry.metadata().with_context(|| format!("cannot read {rel}"))?;
    let node: Node = Node::File {
        size: metadata.len(),
        sha256: sha256_file(path)?,
        mode: mode_of(&metadata)?,
    };
    entries.insert(rel, node);
    Ok(())
}

/// Every file and link of the build at `root`. Links are recorded, never followed.
pub fn scan(root: &Path) -> Result<Tree> {
    ensure!(root.is_dir(), "not a directory: {}", root.display());
    let mut entries: BTreeMap<RelPath, Node> = BTreeMap::new();
    for item in WalkDir::new(root).min_depth(1).follow_links(false).sort_by_file_name() {
        let entry: DirEntry = item.with_context(|| format!("cannot list {}", root.display()))?;
        scan_entry(root, &entry, &mut entries)?;
    }
    Tree::new(entries)
}

/// Creates `path` if needed and refuses it when it is not empty.
pub fn empty_dir(path: &Path) -> Result<PathBuf> {
    fs::create_dir_all(path).with_context(|| format!("cannot create {}", path.display()))?;
    let mut listing: fs::ReadDir = fs::read_dir(path).with_context(|| format!("cannot list {}", path.display()))?;
    ensure!(listing.next().is_none(), "not empty: {}", path.display());
    std::path::absolute(path).with_context(|| format!("cannot resolve {}", path.display()))
}

/// Removes a file, link or folder; a missing path is fine. A link is removed, never followed.
pub fn remove(path: &Path) -> Result<()> {
    let Ok(metadata) = fs::symlink_metadata(path) else {
        return Ok(());
    };
    if metadata.is_dir() {
        fs::remove_dir_all(path).with_context(|| format!("cannot remove {}", path.display()))
    } else {
        fs::remove_file(path).with_context(|| format!("cannot remove {}", path.display()))
    }
}

/// Removes the folders above `path` that became empty, up to `root`.
pub fn remove_empty_parents(path: &Path, root: &Path) -> Result<()> {
    let mut folder: Option<&Path> = path.parent();
    while let Some(current) = folder {
        if current == root || !current.is_dir() {
            break;
        }
        let mut listing: fs::ReadDir =
            fs::read_dir(current).with_context(|| format!("cannot list {}", current.display()))?;
        if listing.next().is_some() {
            break;
        }
        fs::remove_dir(current).with_context(|| format!("cannot remove {}", current.display()))?;
        folder = current.parent();
    }
    Ok(())
}

#[cfg(unix)]
pub fn make_link(target: &str, link: &Path) -> Result<()> {
    std::os::unix::fs::symlink(target, link).with_context(|| format!("cannot create link {}", link.display()))
}

#[cfg(not(unix))]
pub fn make_link(_target: &str, link: &Path) -> Result<()> {
    anyhow::bail!("links are not supported on this platform: {}", link.display())
}

#[cfg(unix)]
pub fn set_mode(path: &Path, mode: &Mode) -> Result<()> {
    use std::os::unix::fs::PermissionsExt;
    fs::set_permissions(path, fs::Permissions::from_mode(mode.bits()))
        .with_context(|| format!("cannot chmod {}", path.display()))
}

#[cfg(not(unix))]
#[allow(clippy::unnecessary_wraps)]
pub fn set_mode(_path: &Path, _mode: &Mode) -> Result<()> {
    Ok(())
}

/// Copies a build, recreating links instead of following them and keeping modes.
pub fn copy_build(from: &Path, to: &Path) -> Result<()> {
    fs::create_dir_all(to).with_context(|| format!("cannot create {}", to.display()))?;
    for item in WalkDir::new(from).min_depth(1).follow_links(false).sort_by_file_name() {
        let entry: DirEntry = item.with_context(|| format!("cannot list {}", from.display()))?;
        let relative: &Path = entry.path().strip_prefix(from)?;
        let destination: PathBuf = to.join(relative);
        if entry.file_type().is_symlink() {
            let target: PathBuf = fs::read_link(entry.path())?;
            make_link(
                target
                    .to_str()
                    .with_context(|| format!("non-UTF-8 link {}", entry.path().display()))?,
                &destination,
            )?;
        } else if entry.file_type().is_dir() {
            fs::create_dir_all(&destination).with_context(|| format!("cannot create {}", destination.display()))?;
        } else {
            fs::copy(entry.path(), &destination).with_context(|| format!("cannot copy {}", entry.path().display()))?;
        }
    }
    Ok(())
}

/// Moves `from` to `to`, replacing whatever is at `to`.
pub fn place(from: &Path, to: &Path) -> Result<()> {
    if let Some(parent) = to.parent() {
        fs::create_dir_all(parent).with_context(|| format!("cannot create {}", parent.display()))?;
    }
    remove(to)?;
    fs::rename(from, to).with_context(|| format!("cannot move {} to {}", from.display(), to.display()))
}

// --- release archives ------------------------------------------------------------------

fn read_member(file: &mut zip::read::ZipFile<'_, File>) -> Result<Member> {
    let name: String = file.name().trim_end_matches('/').to_owned();
    let path: RelPath = RelPath::try_new(name.clone()).with_context(|| format!("unsafe archive entry {name:?}"))?;
    let unix_mode: u32 = file.unix_mode().unwrap_or(0);
    if file.is_dir() {
        return Ok(Member::Folder(path));
    }
    if unix_mode & S_IFMT == S_IFLNK {
        let mut target: String = String::new();
        io::Read::read_to_string(file, &mut target).with_context(|| format!("link {name:?} has no UTF-8 target"))?;
        return Ok(Member::Link { path, target });
    }
    let bits: u32 = unix_mode & 0o777;
    let mode: Mode = if bits == 0 {
        Mode::try_new(DEFAULT_MODE.to_owned())?
    } else {
        Mode::try_new(format!("{bits:03o}"))?
    };
    Ok(Member::File { path, mode })
}

fn list_members(archive: &mut zip::ZipArchive<File>) -> Result<Vec<Member>> {
    let mut members: Vec<Member> = Vec::new();
    for index in 0..archive.len() {
        let mut file: zip::read::ZipFile<'_, File> = archive.by_index(index)?;
        members.push(read_member(&mut file)?);
    }
    Ok(members)
}

/// Writes checked members. Files are created with `create_new`, so a write never goes through
/// anything already at that path.
fn write_members(archive: &mut zip::ZipArchive<File>, members: &[Member], out: &Path) -> Result<()> {
    for (index, member) in members.iter().enumerate() {
        let target: PathBuf = member.path().under(out);
        match member {
            Member::Folder(_) => {
                fs::create_dir_all(&target).with_context(|| format!("cannot create {}", target.display()))?;
            }
            Member::Link {
                target: link_target, ..
            } => {
                fs::create_dir_all(target.parent().unwrap_or(out))?;
                make_link(link_target, &target)?;
            }
            Member::File { mode, .. } => {
                fs::create_dir_all(target.parent().unwrap_or(out))?;
                let mut sink: File = OpenOptions::new()
                    .write(true)
                    .create_new(true)
                    .open(&target)
                    .with_context(|| format!("cannot create {}", target.display()))?;
                let mut source: zip::read::ZipFile<'_, File> = archive.by_index(index)?;
                io::copy(&mut source, &mut sink).with_context(|| format!("cannot extract {}", member.path()))?;
                set_mode(&target, mode)?;
            }
        }
    }
    Ok(())
}

fn only_entry(folder: &Path) -> Result<Option<String>> {
    let mut names: Vec<String> = Vec::new();
    for item in fs::read_dir(folder).with_context(|| format!("cannot list {}", folder.display()))? {
        names.push(item?.file_name().to_string_lossy().into_owned());
    }
    Ok(if names.len() == 1 { names.pop() } else { None })
}

/// Extracts a release zip into the empty `out` folder and returns the build root. Windows zips
/// hold the build directly; macOS zips hold `build.tar` (keeping modes and links) whose `build/`
/// folder is the root. Every zip entry is checked before anything is written; the `tar` crate
/// refuses entries that would land outside `out`.
pub fn extract(archive_path: &Path, out_dir: &Path) -> Result<PathBuf> {
    let out: PathBuf = empty_dir(out_dir)?;
    let file: File = File::open(archive_path).with_context(|| format!("cannot open {}", archive_path.display()))?;
    let mut archive: zip::ZipArchive<File> =
        zip::ZipArchive::new(file).with_context(|| format!("not a zip: {}", archive_path.display()))?;
    let members: Vec<Member> = list_members(&mut archive)?;
    check_members(&members)?;
    write_members(&mut archive, &members, &out)?;
    if only_entry(&out)?.as_deref() == Some("build.tar") {
        let tar_path: PathBuf = out.join("build.tar");
        let tar_file: File = File::open(&tar_path)?;
        let mut tar_archive: tar::Archive<File> = tar::Archive::new(tar_file);
        tar_archive.set_overwrite(false);
        tar_archive
            .unpack(&out)
            .with_context(|| format!("cannot unpack {}", tar_path.display()))?;
        fs::remove_file(&tar_path)?;
    }
    if only_entry(&out)?.as_deref() == Some("build") && out.join("build").is_dir() {
        return Ok(out.join("build"));
    }
    Ok(out)
}

/// The verification result of each artifact under `root`.
pub fn check_signatures(
    platform: Platform,
    root: &Path,
    artifacts: &[RelPath],
    signer: &dyn Signer,
) -> Result<Signatures> {
    let mut signatures: Signatures = Signatures::new();
    for artifact in artifacts {
        let valid: bool = signer.verifies(platform, &artifact.under(root))?;
        signatures.insert(artifact.clone(), valid);
    }
    Ok(signatures)
}

#[cfg(test)]
mod tests {
    use std::fs;

    use anyhow::{Result, ensure};

    use super::{empty_dir, remove_empty_parents, sha256_file};

    #[test]
    fn hashes_a_file() -> Result<()> {
        let dir: tempfile::TempDir = tempfile::tempdir()?;
        let file: std::path::PathBuf = dir.path().join("a");
        fs::write(&file, b"abc")?;
        ensure!(sha256_file(&file)?.to_string() == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        Ok(())
    }

    #[test]
    fn empty_dir_refuses_a_folder_with_content() -> Result<()> {
        let dir: tempfile::TempDir = tempfile::tempdir()?;
        fs::write(dir.path().join("x"), b"x")?;
        ensure!(empty_dir(dir.path()).is_err());
        ensure!(empty_dir(&dir.path().join("new")).is_ok());
        Ok(())
    }

    #[test]
    fn removes_only_emptied_parents() -> Result<()> {
        let dir: tempfile::TempDir = tempfile::tempdir()?;
        let deep: std::path::PathBuf = dir.path().join("a/b/c");
        fs::create_dir_all(&deep)?;
        fs::write(dir.path().join("a/keep"), b"x")?;
        remove_empty_parents(&deep.join("file"), dir.path())?;
        ensure!(!dir.path().join("a/b").exists());
        ensure!(dir.path().join("a").exists());
        Ok(())
    }
}
