//! End to end: synthetic builds through every step, with real `hdiffz`/`hpatchz` and a folder
//! standing in for the bucket. Without `HDiffPatch` the tests are skipped locally and fail in
//! CI (`$CI` set). The transitions themselves are tested without disk in `run::fsm`.

use std::collections::BTreeMap;
use std::fs::{self, File};
use std::io::Write as _;
use std::path::{Path, PathBuf};
use std::process::Output;

use anyhow::{Context, Result, ensure};

use delta::builds::disk::extract;
use delta::io::Io;
use delta::publishing::contract::{DeltaDto, FileDto, ManifestDto};
use delta::publishing::values::ZipName;
use delta::releases::tag::{Platform, Tag};
use delta::run::document;
use delta::run::fsm::{Cmd, init};
use delta::run::runtime::drain;
use delta::run::state::{DropReason, Release, Request, Run, State};
use delta::shell;
use delta::shell::hdiff::HDiffPatch;

const BUCKET_PREFIX: &str = "releases";
const RELEASE: &str = "v1.1.0-alpha";
const BASE: &str = "v1.0.0-alpha";
const OLD_BUNDLE: &str = "Data/aa/ui_assets_all_0123456789abcdef0123456789abcdef.bundle";
const NEW_BUNDLE: &str = "Data/aa/ui_assets_all_fedcba9876543210fedcba9876543210.bundle";

/// `HDiffPatch` from `$HDIFFZ`/`$HPATCHZ` or PATH; `None` when missing outside CI.
fn tools() -> Result<Option<HDiffPatch>> {
    let tools: HDiffPatch = HDiffPatch::from_env();
    let dir: tempfile::TempDir = tempfile::tempdir()?;
    let old: PathBuf = dir.path().join("old");
    let new: PathBuf = dir.path().join("new");
    fs::write(&old, b"a")?;
    fs::write(&new, b"b")?;
    match tools.diff(&old, &new, &dir.path().join("patch")) {
        Ok(()) => Ok(Some(tools)),
        Err(error) if std::env::var_os("CI").is_some() => Err(error.context("HDiffPatch is required in CI")),
        Err(_) => {
            eprintln!("skipped: hdiffz/hpatchz not found (delta fetch-tools <dir>, then set HDIFFZ/HPATCHZ or PATH)");
            Ok(None)
        }
    }
}

fn write(path: &Path, content: &[u8]) -> Result<()> {
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)?;
    }
    fs::write(path, content)?;
    Ok(())
}

fn poke(path: &Path, offset: usize, data: &[u8]) -> Result<()> {
    let mut content: Vec<u8> = fs::read(path)?;
    for (index, byte) in data.iter().enumerate() {
        let slot: &mut u8 = content
            .get_mut(offset.saturating_add(index))
            .context("poke past the end")?;
        *slot = *byte;
    }
    fs::write(path, content)?;
    Ok(())
}

/// Bytes that do not compress or diff well, deterministic per seed.
fn noise(seed: u64, length: usize) -> Vec<u8> {
    let mut state: u64 = seed.wrapping_mul(6_364_136_223_846_793_005).wrapping_add(1);
    let mut bytes: Vec<u8> = Vec::with_capacity(length);
    while bytes.len() < length {
        state = state
            .wrapping_mul(6_364_136_223_846_793_005)
            .wrapping_add(1_442_695_040_888_963_407);
        bytes.extend_from_slice(&state.to_le_bytes());
    }
    bytes.truncate(length);
    bytes
}

/// The base and target builds: changed, added, deleted, renamed and case-renamed files.
fn make_builds(root: &Path) -> Result<(PathBuf, PathBuf)> {
    let base: PathBuf = root.join("base");
    let target: PathBuf = root.join("target");
    for build in [&base, &target] {
        write(&build.join("same.txt"), b"unchanged\n")?;
        write(&build.join("Game.dll"), &noise(1, 262_144))?;
        write(&build.join("Data/space dir/asset file.bin"), &noise(2, 65_536))?;
        write(&build.join("Decentraland.exe"), b"exe")?;
    }
    write(&base.join(OLD_BUNDLE), &noise(3, 131_072))?;
    write(&base.join("gone.txt"), b"removed in the target\n")?;
    write(&base.join("Data/boot.config"), b"a=1\n")?;
    write(&base.join("Data/catalog.bin"), &noise(4, 4096))?;
    write(&base.join("Plugin.dll"), b"old casing\n")?;

    poke(&target.join("Game.dll"), 1000, b"PATCHED-CODE")?;
    poke(&target.join("Game.dll"), 200_000, b"MORE")?;
    write(&target.join(NEW_BUNDLE), &noise(3, 131_072))?;
    poke(&target.join(NEW_BUNDLE), 5000, b"BUNDLE-CHANGE")?;
    write(&target.join("Data/boot.config"), b"a=2\n")?;
    write(&target.join("Data/catalog.bin"), &noise(5, 4096))?;
    poke(&target.join("Data/space dir/asset file.bin"), 10, b"X")?;
    write(&target.join("plugin.dll"), b"new casing\n")?;
    write(&target.join("added.txt"), b"brand new\n")?;
    add_links(&base, &target)?;
    Ok((base, target))
}

#[cfg(unix)]
fn add_links(base: &Path, target: &Path) -> Result<()> {
    use std::os::unix::fs::{PermissionsExt, symlink};
    write(&base.join("run.sh"), b"#!/bin/sh\n")?;
    write(&target.join("run.sh"), b"#!/bin/sh\n")?;
    fs::set_permissions(base.join("run.sh"), fs::Permissions::from_mode(0o644))?;
    fs::set_permissions(target.join("run.sh"), fs::Permissions::from_mode(0o755))?;
    symlink("same.txt", target.join("link.txt"))?;
    // A folder that becomes a link (.framework Versions/Current) and a link that becomes a folder.
    write(&base.join("Fw/Versions/A/lib"), b"lib a\n")?;
    write(&target.join("Fw/Versions/A/lib"), b"lib a\n")?;
    write(&base.join("Fw/Versions/Current/lib"), b"lib current\n")?;
    symlink("A", target.join("Fw/Versions/Current"))?;
    symlink("Fw/Versions/A", base.join("Lib"))?;
    write(&target.join("Lib/lib"), b"lib dir\n")?;
    Ok(())
}

#[cfg(not(unix))]
#[allow(clippy::unnecessary_wraps)]
fn add_links(_base: &Path, _target: &Path) -> Result<()> {
    Ok(())
}

#[cfg(unix)]
fn mode_of(path: &Path) -> Result<u32> {
    use std::os::unix::fs::PermissionsExt;
    Ok(fs::metadata(path)?.permissions().mode() & 0o777)
}

#[cfg(not(unix))]
#[allow(clippy::unnecessary_wraps)]
fn mode_of(_path: &Path) -> Result<u32> {
    Ok(0o644)
}

/// Like `zip -ry`: keeps modes and stores links as links.
fn zip_folder(folder: &Path, archive: &Path) -> Result<()> {
    if let Some(parent) = archive.parent() {
        fs::create_dir_all(parent)?;
    }
    let mut writer: zip::ZipWriter<File> = zip::ZipWriter::new(File::create(archive)?);
    for item in walkdir::WalkDir::new(folder).min_depth(1).sort_by_file_name() {
        let entry: walkdir::DirEntry = item?;
        let name: String = entry.path().strip_prefix(folder)?.to_string_lossy().replace('\\', "/");
        if entry.path_is_symlink() {
            let target: String = fs::read_link(entry.path())?.to_string_lossy().into_owned();
            writer.add_symlink(name, target, zip::write::SimpleFileOptions::default())?;
        } else if entry.file_type().is_file() {
            let options: zip::write::SimpleFileOptions =
                zip::write::SimpleFileOptions::default().unix_permissions(mode_of(entry.path())?);
            writer.start_file(name, options)?;
            writer.write_all(&fs::read(entry.path())?)?;
        }
    }
    writer.finish()?;
    Ok(())
}

/// A zip of raw entries: `(name, Some(link target))` is a link, `(name, None)` a file.
fn zip_entries(archive: &Path, entries: &[(&str, Option<&str>)]) -> Result<()> {
    if let Some(parent) = archive.parent() {
        fs::create_dir_all(parent)?;
    }
    let mut writer: zip::ZipWriter<File> = zip::ZipWriter::new(File::create(archive)?);
    for (name, link) in entries {
        if let Some(target) = link {
            writer.add_symlink(*name, *target, zip::write::SimpleFileOptions::default())?;
        } else {
            writer.start_file(*name, zip::write::SimpleFileOptions::default())?;
            writer.write_all(b"pwned")?;
        }
    }
    writer.finish()?;
    Ok(())
}

struct Fixture {
    _dir: tempfile::TempDir,
    root: PathBuf,
    bucket: PathBuf,
    base: PathBuf,
    release: PathBuf,
}

impl Fixture {
    fn new() -> Result<Fixture> {
        let dir: tempfile::TempDir = tempfile::tempdir()?;
        let root: PathBuf = dir.path().to_path_buf();
        let (base, release): (PathBuf, PathBuf) = make_builds(&root.join("builds"))?;
        let bucket: PathBuf = root.join("bucket");
        Ok(Fixture {
            _dir: dir,
            root,
            bucket,
            base,
            release,
        })
    }

    fn zip_name() -> Result<ZipName> {
        Ok(ZipName::try_new("Decentraland_windows64.zip".to_owned())?)
    }

    fn zip_key(&self, tag: &str) -> Result<PathBuf> {
        Ok(self
            .bucket
            .join(BUCKET_PREFIX)
            .join(tag)
            .join(Fixture::zip_name()?.to_string()))
    }

    /// Publishes the builds' zips into the bucket, as the release build does.
    fn upload_zips(&self) -> Result<()> {
        zip_folder(&self.release, &self.zip_key(RELEASE)?)?;
        zip_folder(&self.base, &self.zip_key(BASE)?)
    }

    /// Writes a new run document, as `create-run` does, and returns its path.
    fn create_run(&self, bases: &[&str], platform: Platform) -> Result<PathBuf> {
        let mut tags: Vec<Tag> = Vec::new();
        for base in bases {
            tags.push(base.parse()?);
        }
        let release: Release = Release {
            tag: RELEASE.parse()?,
            platform,
            zip: Fixture::zip_name()?,
            bucket_prefix: BUCKET_PREFIX.to_owned(),
            work: self.root.join("work"),
        };
        let (run, cmds): (Run, Vec<Cmd>) = init(Request::new(release, tags)?);
        let path: PathBuf = self.root.join("run.json");
        document::save(&path, &run, &cmds)?;
        Ok(path)
    }

    fn prefix(&self) -> PathBuf {
        self.bucket
            .join(BUCKET_PREFIX)
            .join(RELEASE)
            .join("delta")
            .join("windows64")
    }
}

struct World {
    io: Io,
}

impl World {
    fn new(fixture: &Fixture, hdiff: HDiffPatch) -> World {
        World {
            io: shell::folder(fixture.bucket.clone(), hdiff),
        }
    }

    /// Drains the run document like `delta run`, to completion or to `until`.
    fn run(&self, document: &Path, until: Option<&str>) -> Result<Run> {
        drain(document, until, &self.io)
    }
}

fn failure(run: &Run) -> Option<&str> {
    match &run.state {
        State::Failed { reason } => Some(reason.as_str()),
        _ => None,
    }
}

fn read_manifest(fixture: &Fixture) -> Result<ManifestDto> {
    let text: String = fs::read_to_string(fixture.prefix().join("manifest.json"))?;
    Ok(serde_json::from_str(&text)?)
}

#[test]
fn a_run_publishes_verified_patches_and_the_manifest() -> Result<()> {
    let Some(differ) = tools()? else { return Ok(()) };
    let fixture: Fixture = Fixture::new()?;
    fixture.upload_zips()?;
    let world: World = World::new(&fixture, differ);
    let run: Run = world.run(&fixture.create_run(&[BASE], Platform::Windows64)?, None)?;
    ensure!(failure(&run).is_none(), "run failed: {:?}", failure(&run));
    ensure!(run.state.name() == "Published");
    ensure!(run.dropped.is_empty());

    let manifest: ManifestDto = read_manifest(&fixture)?;
    ensure!(manifest.version == RELEASE && manifest.platform == "windows64");
    ensure!(manifest.zip.path == "Decentraland_windows64.zip");
    let delta: &DeltaDto = manifest.bases.get(BASE).context("no patch set for the base")?;
    let patched: BTreeMap<&str, &str> = delta
        .patches
        .iter()
        .map(|p| (p.path.as_str(), p.from_path.as_str()))
        .collect();
    let whole: Vec<&str> = delta.whole.iter().map(|w| w.path.as_str()).collect();
    ensure!(patched.get("Game.dll") == Some(&"Game.dll"));
    ensure!(
        patched.get(NEW_BUNDLE) == Some(&OLD_BUNDLE),
        "renamed bundle not paired: {patched:?}"
    );
    ensure!(whole.contains(&"added.txt") && whole.contains(&"plugin.dll") && whole.contains(&"Data/catalog.bin"));
    ensure!(
        patched.contains_key("Data/boot.config"),
        "a small edit is kept as a patch"
    );
    for deleted in [OLD_BUNDLE, "gone.txt", "Plugin.dll"] {
        ensure!(delta.deleted.iter().any(|d| d == deleted), "{deleted} not deleted");
    }
    ensure!(!patched.contains_key("same.txt") && !whole.contains(&"same.txt"));
    let game: &delta::publishing::contract::PatchDto = delta
        .patches
        .iter()
        .find(|p| p.path == "Game.dll")
        .context("no Game.dll patch")?;
    let build_id: String = format!("{}-e1", manifest.zip.sha256.get(..8).context("short sha256")?);
    ensure!(
        game.patch == format!("from/{BASE}/{build_id}/Game.dll.hdiff"),
        "{}",
        game.patch
    );
    ensure!(game.patch_size < 262_144 / 10, "a small change gives a small patch");
    ensure!(fixture.prefix().join(&game.patch).is_file(), "patch not uploaded");
    ensure!(
        manifest
            .files
            .iter()
            .any(|f| matches!(f, FileDto::File { path, .. } if path == "same.txt"))
    );

    let caching: String = fs::read_to_string(fixture.prefix().join(".caching"))?;
    ensure!(
        caching == "Immutable\nShortLived\n",
        "patches first and immutable, the manifest last: {caching:?}"
    );
    Ok(())
}

#[cfg(unix)]
#[test]
fn links_and_modes_survive_the_round_trip() -> Result<()> {
    let Some(differ) = tools()? else { return Ok(()) };
    let fixture: Fixture = Fixture::new()?;
    fixture.upload_zips()?;
    let world: World = World::new(&fixture, differ);
    let run: Run = world.run(&fixture.create_run(&[BASE], Platform::Windows64)?, None)?;
    ensure!(failure(&run).is_none(), "run failed: {:?}", failure(&run));
    let manifest: ManifestDto = read_manifest(&fixture)?;
    ensure!(
        manifest
            .files
            .iter()
            .any(|f| matches!(f, FileDto::Link { path, symlink } if path == "link.txt" && symlink == "same.txt"))
    );
    ensure!(
        manifest
            .files
            .iter()
            .any(|f| matches!(f, FileDto::File { path, mode, .. } if path == "run.sh" && mode == "755"))
    );
    let delta: &DeltaDto = manifest.bases.get(BASE).context("no patch set")?;
    ensure!(
        delta.deleted.iter().any(|d| d == "Lib"),
        "a link replaced by a folder is deleted first"
    );
    Ok(())
}

#[test]
fn a_bad_base_is_dropped_and_the_others_ship() -> Result<()> {
    let Some(differ) = tools()? else { return Ok(()) };
    let fixture: Fixture = Fixture::new()?;
    fixture.upload_zips()?;
    let corrupt: &str = "v0.9.0-alpha";
    let missing: &str = "v0.8.0-alpha";
    let unsafe_zip: &str = "v0.7.0-alpha";
    write(
        &fixture.zip_key(corrupt)?,
        fs::read(fixture.zip_key(BASE)?)?.get(..100).context("short zip")?,
    )?;
    zip_entries(
        &fixture.zip_key(unsafe_zip)?,
        &[("lnk", Some("sub")), ("lnk/pwned", None)],
    )?;
    let world: World = World::new(&fixture, differ);
    let run: Run = world.run(
        &fixture.create_run(&[BASE, corrupt, missing, unsafe_zip], Platform::Windows64)?,
        None,
    )?;
    ensure!(failure(&run).is_none(), "run failed: {:?}", failure(&run));
    let reasons: BTreeMap<String, &DropReason> = run.dropped.iter().map(|d| (d.base.to_string(), &d.reason)).collect();
    ensure!(
        matches!(reasons.get(corrupt), Some(DropReason::Unreadable(_))),
        "{reasons:?}"
    );
    ensure!(
        matches!(reasons.get(missing), Some(DropReason::ZipMissing(_))),
        "{reasons:?}"
    );
    ensure!(
        matches!(reasons.get(unsafe_zip), Some(DropReason::Unreadable(_))),
        "{reasons:?}"
    );
    let manifest: ManifestDto = read_manifest(&fixture)?;
    ensure!(manifest.bases.keys().collect::<Vec<_>>() == [BASE]);
    ensure!(
        !fixture.root.join("work/bases").join(unsafe_zip).join("lnk").exists(),
        "nothing of the unsafe zip was written"
    );
    Ok(())
}

/// APFS takes the composed and decomposed forms of `é` as one name, which the case-folded entry
/// check does not see: a file listed under the other form of a link's name must not be written
/// through the link chain `d/é -> up/../..`, `d/up -> ..` above the build.
#[test]
fn extraction_never_writes_through_a_link_named_in_another_unicode_form() -> Result<()> {
    let dir: tempfile::TempDir = tempfile::tempdir()?;
    let archive: PathBuf = dir.path().join("release.zip");
    zip_entries(
        &archive,
        &[
            ("d/up", Some("..")),
            ("d/\u{e9}", Some("up/../..")),
            ("d/e\u{301}/payload", None),
        ],
    )?;
    let above: PathBuf = dir.path().join("above");
    let extracted: Result<PathBuf> = extract(&archive, &above.join("out"));
    let written: Vec<String> = fs::read_dir(&above)?
        .map(|item| Ok(item?.file_name().to_string_lossy().into_owned()))
        .collect::<Result<Vec<String>>>()?;
    ensure!(
        written == ["out"],
        "written next to the build: {written:?} ({extracted:?})"
    );
    ensure!(
        !dir.path().join("payload").exists(),
        "written above the build ({extracted:?})"
    );
    Ok(())
}

#[cfg(unix)]
#[test]
fn a_macos_zip_holding_build_tar_is_extracted_with_links_and_modes() -> Result<()> {
    let Some(differ) = tools()? else { return Ok(()) };
    let fixture: Fixture = Fixture::new()?;
    for (tag, build) in [(RELEASE, &fixture.release), (BASE, &fixture.base)] {
        let staging: PathBuf = fixture.root.join("tar").join(tag);
        fs::create_dir_all(&staging)?;
        let mut tar: tar::Builder<File> = tar::Builder::new(File::create(staging.join("build.tar"))?);
        tar.follow_symlinks(false);
        tar.append_dir_all("build", build)?;
        tar.finish()?;
        zip_folder(&staging, &fixture.zip_key(tag)?)?;
    }
    let world: World = World::new(&fixture, differ);
    let run: Run = world.run(&fixture.create_run(&[BASE], Platform::Macos)?, Some("Scanned"))?;
    let State::Scanned(builds) = &run.state else {
        anyhow::bail!("not scanned: {:?}", failure(&run))
    };
    ensure!(builds.build.root.ends_with("build"));
    let run_sh: Option<&delta::builds::tree::Node> = builds.build.tree.get(&"run.sh".parse()?);
    ensure!(matches!(run_sh, Some(delta::builds::tree::Node::File { mode, .. }) if mode.to_string() == "755"));
    Ok(())
}

fn delta_binary(args: &[&str]) -> Result<Output> {
    Ok(std::process::Command::new(env!("CARGO_BIN_EXE_delta"))
        .args(args)
        .output()?)
}

#[test]
fn a_base_that_only_lost_files_ships_an_empty_patch_set() -> Result<()> {
    let Some(differ) = tools()? else { return Ok(()) };
    let fixture: Fixture = Fixture::new()?;
    fixture.upload_zips()?;
    let superset: &str = "v0.9.5-alpha";
    let folder: PathBuf = fixture.root.join("builds").join("superset");
    delta::builds::disk::copy_build(&fixture.release, &folder)?;
    write(&folder.join("extra.txt"), b"only in the base\n")?;
    zip_folder(&folder, &fixture.zip_key(superset)?)?;
    let world: World = World::new(&fixture, differ);
    let run: Run = world.run(&fixture.create_run(&[superset], Platform::Windows64)?, None)?;
    ensure!(failure(&run).is_none(), "run failed: {:?}", failure(&run));
    let manifest: ManifestDto = read_manifest(&fixture)?;
    let delta: &DeltaDto = manifest.bases.get(superset).context("no patch set")?;
    ensure!(delta.deleted == vec!["extra.txt"] && delta.patches.is_empty() && delta.whole.is_empty());
    Ok(())
}

#[test]
fn a_zip_replaced_during_the_run_is_not_published() -> Result<()> {
    let Some(differ) = tools()? else { return Ok(()) };
    let fixture: Fixture = Fixture::new()?;
    fixture.upload_zips()?;
    let world: World = World::new(&fixture, differ);
    let document: PathBuf = fixture.create_run(&[BASE], Platform::Windows64)?;
    let paused: Run = world.run(&document, Some("Prepared"))?;
    ensure!(paused.state.name() == "Prepared", "{:?}", failure(&paused));
    write(&fixture.zip_key(RELEASE)?, b"a later build of the same tag")?;
    let run: Run = world.run(&document, None)?;
    let reason: &str = failure(&run).context("the run should fail")?;
    ensure!(reason.contains("changed in the bucket"), "{reason}");
    ensure!(
        !fixture.prefix().join("manifest.json").exists(),
        "nothing was published"
    );
    ensure!(!fixture.prefix().join("from").exists(), "no patch was uploaded either");
    Ok(())
}

#[test]
fn a_zip_replaced_after_the_patch_upload_holds_back_the_manifest() -> Result<()> {
    let Some(differ) = tools()? else { return Ok(()) };
    let fixture: Fixture = Fixture::new()?;
    fixture.upload_zips()?;
    let world: World = World::new(&fixture, differ);
    let document: PathBuf = fixture.create_run(&[BASE], Platform::Windows64)?;
    let paused: Run = world.run(&document, Some("PatchesUploaded"))?;
    ensure!(paused.state.name() == "PatchesUploaded", "{:?}", failure(&paused));
    ensure!(fixture.prefix().join("from").exists(), "the patches are up");
    write(&fixture.zip_key(RELEASE)?, b"a later build of the same tag")?;
    let run: Run = world.run(&document, None)?;
    let reason: &str = failure(&run).context("the run should fail")?;
    ensure!(reason.contains("changed in the bucket"), "{reason}");
    ensure!(
        !fixture.prefix().join("manifest.json").exists(),
        "the manifest was held back"
    );
    Ok(())
}

#[test]
fn a_missing_release_zip_fails_the_run_before_the_until_state() -> Result<()> {
    let fixture: Fixture = Fixture::new()?;
    zip_folder(&fixture.base, &fixture.zip_key(BASE)?)?;
    let world: World = World::new(&fixture, HDiffPatch::from_env());
    let document: PathBuf = fixture.create_run(&[BASE], Platform::Windows64)?;
    let run: Run = world.run(&document, Some("Scanned"))?;
    let reason: &str = failure(&run).context("the run should fail")?;
    ensure!(
        reason.contains("no object") && reason.contains("the run was Requested"),
        "the first recheck already needs the zip: {reason}"
    );
    let (saved, cmds): (Run, Vec<Cmd>) = document::load(&document)?;
    ensure!(saved == run && cmds == vec![Cmd::Complete]);
    let again: Run = world.run(&document, None)?;
    ensure!(failure(&again) == Some(reason), "a finished run stays as it is");
    Ok(())
}

#[test]
fn the_command_line_pauses_at_until_and_resumes_to_completion() -> Result<()> {
    if tools()?.is_none() {
        return Ok(());
    }
    let fixture: Fixture = Fixture::new()?;
    fixture.upload_zips()?;
    let document: PathBuf = fixture.root.join("run.json");
    let document_text: &str = document.to_str().context("path")?;
    let work: PathBuf = fixture.root.join("work");
    let created: Output = delta_binary(&[
        "create-run",
        document_text,
        "--tag",
        RELEASE,
        "--platform",
        "windows64",
        "--zip",
        "Decentraland_windows64.zip",
        "--bucket-prefix",
        BUCKET_PREFIX,
        "--bases",
        &format!("[\"{BASE}\"]"),
        "--work",
        work.to_str().context("path")?,
    ])?;
    ensure!(created.status.success(), "{}", String::from_utf8_lossy(&created.stderr));
    let bucket: &str = fixture.bucket.to_str().context("path")?;

    let paused: Output = delta_binary(&["run", document_text, "--until", "Downloaded", "--local", bucket])?;
    ensure!(paused.status.success(), "{}", String::from_utf8_lossy(&paused.stderr));
    let (run, cmds): (Run, Vec<Cmd>) = document::load(&document)?;
    ensure!(run.state.name() == "Downloaded");
    ensure!(matches!(cmds.as_slice(), [Cmd::ExtractArchives { .. }]), "{cmds:?}");

    let repeated: Output = delta_binary(&["run", document_text, "--until", "Downloaded", "--local", bucket])?;
    ensure!(
        repeated.status.success(),
        "{}",
        String::from_utf8_lossy(&repeated.stderr)
    );
    ensure!(
        document::load(&document)? == (run, cmds),
        "a slice already reached does nothing"
    );

    let finished: Output = delta_binary(&["run", document_text, "--local", bucket])?;
    ensure!(
        finished.status.success(),
        "{}",
        String::from_utf8_lossy(&finished.stderr)
    );
    let (run, cmds): (Run, Vec<Cmd>) = document::load(&document)?;
    ensure!(run.state.name() == "Published" && cmds == vec![Cmd::Complete]);

    let summary: Output = delta_binary(&["summary", document_text])?;
    let text: String = String::from_utf8_lossy(&summary.stdout).into_owned();
    ensure!(text.contains(&format!("| {BASE} |")), "{text}");

    let again: Output = delta_binary(&["run", document_text, "--local", bucket])?;
    ensure!(again.status.success(), "a finished run completes at once");
    let unknown: Output = delta_binary(&["run", document_text, "--until", "Nowhere", "--local", bucket])?;
    ensure!(!unknown.status.success(), "an unknown state is refused");
    let stale: Output = delta_binary(&["run", document_text, "--until", "ReadyToPublish", "--local", bucket])?;
    ensure!(
        !stale.status.success(),
        "a pause before a stale release check is refused"
    );
    Ok(())
}
