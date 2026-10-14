//! End to end: synthetic builds through every workflow, with real `hdiffz`/`hpatchz` and a
//! folder standing in for the bucket and the CDN. Without `HDiffPatch` the tests are skipped
//! locally and fail in CI (`$CI` set).

use std::collections::BTreeMap;
use std::fs::{self, File};
use std::io::Write as _;
use std::path::{Path, PathBuf};
use std::process::{Command, Output};

use anyhow::{Context, Result, ensure};

use delta::capabilities::Differ;
use delta::publishing::contract::{DeltaDto, FileDto, ManifestDto};
use delta::publishing::values::ZipName;
use delta::releases::tag::{Platform, Tag};
use delta::run::stage::{DropReason, Requested, Run, Stage, Target};
use delta::run::workflows::start;
use delta::run::{Advanced, Capabilities, Workflow, advance};
use delta::shell::hdiff::HDiffPatch;
use delta::shell::local::{FixedReleases, FixedSigner, FolderCdn, FolderStorage};

const RELEASES: &str = "releases";
const TARGET: &str = "v1.1.0-alpha";
const BASE: &str = "v1.0.0-alpha";
const OLD_BUNDLE: &str = "Data/aa/ui_assets_all_0123456789abcdef0123456789abcdef.bundle";
const NEW_BUNDLE: &str = "Data/aa/ui_assets_all_fedcba9876543210fedcba9876543210.bundle";

const ORDER: [Workflow; 11] = [
    Workflow::ConfirmUnpublished,
    Workflow::ClearStale,
    Workflow::Download,
    Workflow::Extract,
    Workflow::Inspect,
    Workflow::Encode,
    Workflow::Verify,
    Workflow::Assemble,
    Workflow::Recheck,
    Workflow::UploadPatches,
    Workflow::PublishManifest,
];

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
    target: PathBuf,
}

impl Fixture {
    fn new() -> Result<Fixture> {
        let dir: tempfile::TempDir = tempfile::tempdir()?;
        let root: PathBuf = dir.path().to_path_buf();
        let (base, target): (PathBuf, PathBuf) = make_builds(&root.join("builds"))?;
        let bucket: PathBuf = root.join("bucket");
        Ok(Fixture {
            _dir: dir,
            root,
            bucket,
            base,
            target,
        })
    }

    fn zip_name() -> Result<ZipName> {
        Ok(ZipName::try_new("Decentraland_windows64.zip".to_owned())?)
    }

    fn zip_key(&self, tag: &str) -> Result<PathBuf> {
        Ok(self
            .bucket
            .join(RELEASES)
            .join(tag)
            .join(Fixture::zip_name()?.to_string()))
    }

    /// Publishes the builds' zips into the bucket, as the release build does.
    fn upload_zips(&self) -> Result<()> {
        zip_folder(&self.target, &self.zip_key(TARGET)?)?;
        zip_folder(&self.base, &self.zip_key(BASE)?)
    }

    fn start(&self, bases: &[&str], platform: Platform) -> Result<Run> {
        let mut tags: Vec<Tag> = Vec::new();
        for base in bases {
            tags.push(base.parse()?);
        }
        let target: Target = Target {
            tag: TARGET.parse()?,
            platform,
            zip: Fixture::zip_name()?,
            releases: RELEASES.to_owned(),
            work: self.root.join("work"),
        };
        let requested: Requested = start(target, tags)?;
        Ok(Run::start(requested))
    }

    fn prefix(&self) -> PathBuf {
        self.bucket.join(RELEASES).join(TARGET).join("delta").join("windows64")
    }
}

struct World {
    releases: FixedReleases,
    storage: FolderStorage,
    cdn: FolderCdn,
    differ: HDiffPatch,
    signer: FixedSigner,
}

impl World {
    fn new(fixture: &Fixture, differ: HDiffPatch) -> World {
        World {
            releases: FixedReleases::default(),
            storage: FolderStorage {
                root: fixture.bucket.clone(),
            },
            cdn: FolderCdn {
                root: fixture.bucket.clone(),
            },
            differ,
            signer: FixedSigner::default(),
        }
    }

    fn step(&self, run: Run, workflow: Workflow) -> Run {
        let caps: Capabilities<'_> = Capabilities {
            releases: &self.releases,
            storage: &self.storage,
            cdn: &self.cdn,
            differ: &self.differ,
            signer: &self.signer,
        };
        let advanced: Advanced = advance(run, workflow, &caps);
        advanced.run
    }

    /// Runs the workflows in order, stopping at a failure.
    fn steps(&self, mut run: Run, workflows: &[Workflow]) -> Run {
        for workflow in workflows {
            run = self.step(run, *workflow);
            if matches!(run.stage, Stage::Failed { .. }) {
                break;
            }
        }
        run
    }
}

fn failure(run: &Run) -> Option<(&str, &str)> {
    match &run.stage {
        Stage::Failed { at, reason } => Some((at.as_str(), reason.as_str())),
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
    let run: Run = world.steps(fixture.start(&[BASE], Platform::Windows64)?, &ORDER);
    ensure!(failure(&run).is_none(), "run failed: {:?}", failure(&run));
    ensure!(run.stage.name() == "Published");
    ensure!(run.drops.is_empty());

    let manifest: ManifestDto = read_manifest(&fixture)?;
    ensure!(manifest.version == TARGET && manifest.platform == "windows64");
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
    ensure!(game.patch == format!("from/{BASE}/Game.dll.hdiff"));
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
        caching == "Immutable\nRevalidated\n",
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
    let run: Run = world.steps(fixture.start(&[BASE], Platform::Windows64)?, &ORDER);
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
    let run: Run = world.steps(
        fixture.start(&[BASE, corrupt, missing, unsafe_zip], Platform::Windows64)?,
        &ORDER,
    );
    ensure!(failure(&run).is_none(), "run failed: {:?}", failure(&run));
    let reasons: BTreeMap<String, &DropReason> = run.drops.iter().map(|d| (d.base.to_string(), &d.reason)).collect();
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

#[test]
fn changed_signatures_drop_the_base_and_an_empty_run_fails() -> Result<()> {
    let Some(differ) = tools()? else { return Ok(()) };
    let fixture: Fixture = Fixture::new()?;
    fixture.upload_zips()?;
    let mut world: World = World::new(&fixture, differ);
    world.signer = FixedSigner {
        invalid_under: Some(fixture.root.join("work").join("rebuild")),
    };
    let run: Run = world.steps(fixture.start(&[BASE], Platform::Windows64)?, &ORDER);
    let (at, reason): (&str, &str) = failure(&run).context("the run should fail")?;
    ensure!(
        at == "verify" && reason.contains("no base left") && reason.contains("signatures changed"),
        "{at}: {reason}"
    );
    ensure!(!fixture.prefix().join("manifest.json").exists());
    Ok(())
}

#[test]
fn a_published_target_is_refused() -> Result<()> {
    let fixture: Fixture = Fixture::new()?;
    let mut world: World = World::new(&fixture, HDiffPatch::from_env());
    world.releases = FixedReleases {
        published: vec![TARGET.parse()?],
        listed: Vec::new(),
    };
    let run: Run = world.step(
        fixture.start(&[BASE], Platform::Windows64)?,
        Workflow::ConfirmUnpublished,
    );
    let (at, reason): (&str, &str) = failure(&run).context("the run should fail")?;
    ensure!(at == "confirm-unpublished" && reason.contains("is published"));
    Ok(())
}

#[test]
fn a_zip_replaced_during_the_run_is_not_uploaded() -> Result<()> {
    let Some(differ) = tools()? else { return Ok(()) };
    let fixture: Fixture = Fixture::new()?;
    fixture.upload_zips()?;
    let world: World = World::new(&fixture, differ);
    let assembled: Run = world.steps(
        fixture.start(&[BASE], Platform::Windows64)?,
        ORDER.get(..8).context("order")?,
    );
    ensure!(assembled.stage.name() == "Assembled", "{:?}", failure(&assembled));
    write(&fixture.zip_key(TARGET)?, b"a later build of the same tag")?;
    let run: Run = world.step(assembled, Workflow::Recheck);
    let (at, reason): (&str, &str) = failure(&run).context("the run should fail")?;
    ensure!(
        at == "recheck" && reason.contains("changed in the bucket"),
        "{at}: {reason}"
    );
    Ok(())
}

#[test]
fn a_workflow_out_of_order_fails_the_run_and_a_failed_run_stays_failed() -> Result<()> {
    let fixture: Fixture = Fixture::new()?;
    let world: World = World::new(&fixture, HDiffPatch::from_env());
    let run: Run = world.step(fixture.start(&[BASE], Platform::Windows64)?, Workflow::Encode);
    let (at, reason): (String, String) = failure(&run)
        .map(|(a, r)| (a.to_owned(), r.to_owned()))
        .context("should fail")?;
    ensure!(
        at == "encode" && reason.contains("encode follows Inspected, but the run is Requested"),
        "{reason}"
    );
    let again: Run = world.step(run, Workflow::ConfirmUnpublished);
    ensure!(
        failure(&again) == Some((at.as_str(), reason.as_str())),
        "the first failure is kept"
    );
    Ok(())
}

#[cfg(unix)]
#[test]
fn a_macos_zip_holding_build_tar_is_extracted_with_links_and_modes() -> Result<()> {
    let Some(differ) = tools()? else { return Ok(()) };
    let fixture: Fixture = Fixture::new()?;
    for (tag, build) in [(TARGET, &fixture.target), (BASE, &fixture.base)] {
        let staging: PathBuf = fixture.root.join("tar").join(tag);
        fs::create_dir_all(&staging)?;
        let mut tar: tar::Builder<File> = tar::Builder::new(File::create(staging.join("build.tar"))?);
        tar.follow_symlinks(false);
        tar.append_dir_all("build", build)?;
        tar.finish()?;
        zip_folder(&staging, &fixture.zip_key(tag)?)?;
    }
    let world: World = World::new(&fixture, differ);
    let run: Run = world.steps(
        fixture.start(&[BASE], Platform::Macos)?,
        ORDER.get(..5).context("order")?,
    );
    let Stage::Inspected(inspected) = &run.stage else {
        anyhow::bail!("not inspected: {:?}", failure(&run))
    };
    ensure!(inspected.build.root.ends_with("build"));
    let run_sh: Option<&delta::builds::tree::Node> = inspected.build.tree.get(&"run.sh".parse()?);
    ensure!(matches!(run_sh, Some(delta::builds::tree::Node::File { mode, .. }) if mode.to_string() == "755"));
    Ok(())
}

fn delta_binary(args: &[&str]) -> Result<Output> {
    Ok(Command::new(env!("CARGO_BIN_EXE_delta")).args(args).output()?)
}

#[test]
fn the_command_line_runs_each_workflow_on_the_document() -> Result<()> {
    if tools()?.is_none() {
        return Ok(());
    }
    let fixture: Fixture = Fixture::new()?;
    fixture.upload_zips()?;
    let document: PathBuf = fixture.root.join("run.json");
    let document_text: &str = document.to_str().context("path")?;
    let work: PathBuf = fixture.root.join("work");
    let started: Output = delta_binary(&[
        "start",
        document_text,
        "--tag",
        TARGET,
        "--platform",
        "windows64",
        "--zip",
        "Decentraland_windows64.zip",
        "--releases",
        RELEASES,
        "--bases",
        &format!("[\"{BASE}\"]"),
        "--work",
        work.to_str().context("path")?,
    ])?;
    ensure!(started.status.success(), "{}", String::from_utf8_lossy(&started.stderr));
    let bucket: &str = fixture.bucket.to_str().context("path")?;
    for workflow in ORDER {
        let name: String = workflow.to_string();
        let output: Output = delta_binary(&["step", &name, document_text, "--local", bucket])?;
        ensure!(
            output.status.success(),
            "{name}: {}",
            String::from_utf8_lossy(&output.stderr)
        );
    }
    let summary: Output = delta_binary(&["summary", document_text])?;
    let text: String = String::from_utf8_lossy(&summary.stdout).into_owned();
    ensure!(text.contains(&format!("| {BASE} |")), "{text}");
    let out_of_order: Output = delta_binary(&["step", "encode", document_text, "--local", bucket])?;
    ensure!(!out_of_order.status.success(), "a step on a finished run must fail");
    Ok(())
}
