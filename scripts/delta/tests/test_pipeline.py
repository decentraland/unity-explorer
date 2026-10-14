"""Functional tests on small synthetic builds with the real HDiffPatch tools."""

import os
import shutil
import tarfile
import zipfile
from dataclasses import dataclass
from pathlib import Path

import pytest
from returns.pipeline import is_successful
from returns.result import Result, Success

from delta import pipeline
from delta.cli import main
from delta.domain import BaseResult, Manifest, Platform, Problem, RegularFile, Symlink
from delta.io import fs
from delta.io.hdiff import HDiffPatch
from delta.io.signatures import Verifier, check_signatures
from tests.builders import POSIX_ONLY, problem_of, symlink_member, value_of, write, zip_dir

BASE_TAG = "v1.0.0-alpha"
VERSION = "v1.1.0-alpha"
OLD_BUNDLE = "Data/aa/ui_assets_all_0123456789abcdef0123456789abcdef.bundle"
NEW_BUNDLE = "Data/aa/ui_assets_all_fedcba9876543210fedcba9876543210.bundle"
SYMLINKS = os.name != "nt"


def _poke(path: Path, offset: int, data: bytes) -> None:
    with path.open("r+b") as stream:
        stream.seek(offset)
        stream.write(data)


@dataclass(frozen=True)
class Builds:
    base: Path
    new: Path
    zip: Path


def _make_builds(root: Path) -> Builds:
    base, new = root / "base", root / "new"
    write(base / "same.txt", b"unchanged\n")
    write(base / "Game.dll", os.urandom(262144))
    write(base / OLD_BUNDLE, os.urandom(131072))
    write(base / "gone.txt", b"removed in new\n")
    write(base / "Data/boot.config", b"a=1\n")
    write(base / "Data/catalog.bin", os.urandom(4096))
    write(base / "Data/space dir/asset file.bin", os.urandom(65536))
    write(base / "run.sh", b"#!/bin/sh\necho hi\n")
    write(base / "Plugin.dll", b"old casing\n")
    write(base / "Decentraland.exe", b"x")
    shutil.copytree(base, new)
    # Case-only rename: on NTFS/APFS deleting the old name after writing the new one loses the file.
    (new / "Plugin.dll").unlink()
    write(new / "plugin.dll", b"new casing\n")
    (new / "gone.txt").unlink()
    _poke(new / "Game.dll", 1000, b"PATCHED-CODE")
    _poke(new / "Game.dll", 200000, b"MORE")
    (new / OLD_BUNDLE).rename(new / NEW_BUNDLE)
    _poke(new / NEW_BUNDLE, 5000, b"BUNDLE-CHANGE")
    write(new / "Data/boot.config", b"a=2\n")
    write(new / "Data/catalog.bin", os.urandom(4096))
    _poke(new / "Data/space dir/asset file.bin", 10, b"X")
    write(new / "added.txt", b"brand new\n")
    if SYMLINKS:
        (base / "run.sh").chmod(0o644)
        (new / "run.sh").chmod(0o755)
        (new / "link.txt").symlink_to("same.txt")
        # A real directory that becomes a symlink (.framework Versions/Current) and the reverse.
        write(base / "Fw/Versions/A/lib", b"lib a\n")
        write(new / "Fw/Versions/A/lib", b"lib a\n")
        write(base / "Fw/Versions/Current/lib", b"lib current\n")
        (new / "Fw/Versions/Current").symlink_to("A")
        (base / "Lib").symlink_to("Fw/Versions/A")
        write(new / "Lib/lib", b"lib dir\n")
    return Builds(base=base, new=new, zip=write(root / "Decentraland_test.zip", os.urandom(1024)))


@pytest.fixture(scope="module")
def builds(tmp_path_factory: pytest.TempPathFactory) -> Builds:
    return _make_builds(tmp_path_factory.mktemp("builds"))


@pytest.fixture(scope="module")
def made(builds: Builds, tools: HDiffPatch, tmp_path_factory: pytest.TempPathFactory) -> tuple[Path, BaseResult]:
    out = tmp_path_factory.mktemp("out")
    return out, value_of(pipeline.make_delta(builds.base, builds.new, BASE_TAG, out, tools))


@pytest.fixture(scope="module")
def manifest(builds: Builds, made: tuple[Path, BaseResult]) -> Manifest:
    return value_of(pipeline.build_manifest(VERSION, Platform.WINDOWS64, builds.new, builds.zip, (made[1],)))


# --- scanning ---------------------------------------------------------------------------


def test_scan_lists_files_with_relative_forward_slash_paths(builds: Builds) -> None:
    entries = value_of(fs.scan_tree(builds.new)).by_path()
    same = entries["same.txt"]
    assert isinstance(same, RegularFile)
    assert same.size == 10
    assert "Data/space dir/asset file.bin" in entries


def test_scan_is_deterministic(builds: Builds) -> None:
    assert fs.scan_tree(builds.new) == fs.scan_tree(builds.new)


@POSIX_ONLY
def test_scan_records_symlinks_and_the_executable_bit(builds: Builds) -> None:
    entries = value_of(fs.scan_tree(builds.new)).by_path()
    assert entries["link.txt"] == Symlink(path="link.txt", symlink="same.txt")
    run = entries["run.sh"]
    assert isinstance(run, RegularFile)
    assert run.mode == "755"


@POSIX_ONLY
@pytest.mark.parametrize(
    ("link", "target"),
    [
        ("sub/link", "../../outside"),
        ("sub/link", "/etc/passwd"),
        ("sub/through", "up/../.."),
        ("sub/dangling", "up/../../outside/file"),
        ("sub/inject", "x\nF\tevil\t1\tabc\t755"),
        ("sub/backslash", "..\\..\\x"),
    ],
)
def test_scan_rejects_symlinks_that_can_leave_the_build(tmp_path: Path, link: str, target: str) -> None:
    (tmp_path / "sub").mkdir()
    (tmp_path / "sub/up").symlink_to("..")
    (tmp_path / link).symlink_to(target)
    assert not is_successful(fs.scan_tree(tmp_path))


@POSIX_ONLY
def test_scan_accepts_symlinks_inside_the_build(tmp_path: Path) -> None:
    (tmp_path / "sub").mkdir()
    (tmp_path / "sub/up").symlink_to("..")
    (tmp_path / "sub/link").symlink_to("../sibling")
    assert len(value_of(fs.scan_tree(tmp_path)).root) == 2


@POSIX_ONLY
def test_scan_rejects_unsafe_file_names(tmp_path: Path) -> None:
    write(tmp_path / "a\\b.dll", b"x")
    assert "unsupported character" in problem_of(fs.scan_tree(tmp_path))


# --- make-delta -------------------------------------------------------------------------


def test_make_delta_writes_the_base_result(made: tuple[Path, BaseResult]) -> None:
    out, result = made
    assert BaseResult.model_validate_json((out / f"base-{BASE_TAG}.json").read_text()) == result


def test_make_delta_plans_every_change(builds: Builds, made: tuple[Path, BaseResult]) -> None:
    delta = made[1].delta
    patched = {p.path: p for p in delta.patches}
    whole = {w.path for w in delta.whole}
    assert patched["Game.dll"].from_path == "Game.dll"
    assert patched["Game.dll"].from_sha256 == value_of(fs.sha256_file(builds.base / "Game.dll"))
    assert patched[NEW_BUNDLE].from_path == OLD_BUNDLE
    assert "Data/space dir/asset file.bin" in patched.keys() | whole
    assert {"added.txt", "Data/catalog.bin", "plugin.dll"} <= whole
    assert "Data/boot.config" in patched
    assert {OLD_BUNDLE, "gone.txt", "Plugin.dll"} <= set(delta.deleted)
    assert "same.txt" not in patched.keys() | whole


def test_make_delta_writes_patches_under_from_tag(made: tuple[Path, BaseResult]) -> None:
    out, result = made
    game = next(p for p in result.delta.patches if p.path == "Game.dll")
    patch = out / "from" / BASE_TAG / "Game.dll.hdiff"
    assert game.patch == f"from/{BASE_TAG}/Game.dll.hdiff"
    assert game.patch_size == patch.stat().st_size
    assert game.patch_sha256 == value_of(fs.sha256_file(patch))
    assert game.patch_size < 262144 // 10


# --- manifest ---------------------------------------------------------------------------


def test_manifest_describes_the_new_build_and_zip(builds: Builds, manifest: Manifest) -> None:
    assert manifest.version == VERSION
    assert manifest.zip.path == "Decentraland_test.zip"
    assert manifest.zip.size == 1024
    assert manifest.zip.sha256 == value_of(fs.sha256_file(builds.zip))
    assert manifest.files == value_of(fs.scan_tree(builds.new))
    assert list(manifest.bases) == [BASE_TAG]


def test_manifest_with_no_bases(builds: Builds) -> None:
    assert value_of(pipeline.build_manifest(VERSION, Platform.MACOS, builds.new, builds.zip, ())).bases == {}


# --- verify -----------------------------------------------------------------------------


def test_verify_rebuilds_the_new_build(
    builds: Builds,
    made: tuple[Path, BaseResult],
    manifest: Manifest,
    tools: HDiffPatch,
    tmp_path: Path,
) -> None:
    rebuild = value_of(pipeline.verify_delta(builds.base, made[0], manifest, BASE_TAG, tmp_path / "rebuild", tools))
    assert (rebuild / "added.txt").is_file()
    assert not (rebuild / "gone.txt").exists()
    assert not (rebuild / OLD_BUNDLE).exists()
    if SYMLINKS:
        assert (rebuild / "link.txt").is_symlink()
        assert os.access(rebuild / "run.sh", os.X_OK)
        assert (rebuild / "Fw/Versions/Current").is_symlink()
        assert not (rebuild / "Lib").is_symlink()
        assert (rebuild / "Lib/lib").is_file()
        assert (rebuild / "Fw/Versions/A/lib").read_bytes() == b"lib a\n"


def test_verify_refuses_a_non_empty_rebuild_dir(
    builds: Builds,
    made: tuple[Path, BaseResult],
    manifest: Manifest,
    tools: HDiffPatch,
    tmp_path: Path,
) -> None:
    write(tmp_path / "rebuild/x", b"x")
    verified = pipeline.verify_delta(builds.base, made[0], manifest, BASE_TAG, tmp_path / "rebuild", tools)
    assert "not empty" in problem_of(verified)


def test_verify_fails_for_an_unknown_base(
    builds: Builds,
    made: tuple[Path, BaseResult],
    manifest: Manifest,
    tools: HDiffPatch,
    tmp_path: Path,
) -> None:
    verified = pipeline.verify_delta(builds.base, made[0], manifest, "v0.9.0-alpha", tmp_path / "rebuild", tools)
    assert "no base" in problem_of(verified)


def test_verify_fails_on_a_tampered_patch(
    builds: Builds,
    made: tuple[Path, BaseResult],
    manifest: Manifest,
    tools: HDiffPatch,
    tmp_path: Path,
) -> None:
    tampered = tmp_path / "out"
    shutil.copytree(made[0], tampered, symlinks=True)
    _poke(tampered / "from" / BASE_TAG / "Game.dll.hdiff", 40, b"ZZZZ")
    verified = pipeline.verify_delta(builds.base, tampered, manifest, BASE_TAG, tmp_path / "rebuild", tools)
    assert "sha256 mismatch" in problem_of(verified)


def test_verify_fails_on_a_different_base(
    builds: Builds,
    made: tuple[Path, BaseResult],
    manifest: Manifest,
    tools: HDiffPatch,
    tmp_path: Path,
) -> None:
    modified = tmp_path / "base"
    shutil.copytree(builds.base, modified, symlinks=True)
    _poke(modified / "Game.dll", 5, b"LOCAL-EDIT")
    assert not is_successful(pipeline.verify_delta(modified, made[0], manifest, BASE_TAG, tmp_path / "rebuild", tools))


# --- extract ----------------------------------------------------------------------------


def test_extract_a_flat_zip_to_its_root(builds: Builds, tmp_path: Path) -> None:
    root = value_of(fs.extract_build(zip_dir(builds.new, tmp_path / "new.zip"), tmp_path / "x"))
    assert root == (tmp_path / "x").resolve()
    assert fs.scan_tree(root) == fs.scan_tree(builds.new)


@POSIX_ONLY
def test_extract_build_tar_inside_the_zip(builds: Builds, tmp_path: Path) -> None:
    staging = tmp_path / "staging"
    shutil.copytree(builds.new, staging / "build", symlinks=True)
    with tarfile.open(staging / "build.tar", "w") as tar:
        tar.add(staging / "build", "build")
    shutil.rmtree(staging / "build")
    root = value_of(fs.extract_build(zip_dir(staging, tmp_path / "tar.zip"), tmp_path / "x"))
    assert root == (tmp_path / "x/build").resolve()
    assert fs.scan_tree(root) == fs.scan_tree(builds.new)


def test_extract_refuses_a_non_empty_dir(builds: Builds, tmp_path: Path) -> None:
    write(tmp_path / "x/y", b"y")
    assert "not empty" in problem_of(fs.extract_build(zip_dir(builds.new, tmp_path / "new.zip"), tmp_path / "x"))


@pytest.mark.parametrize(
    "members",
    [
        [("lnk", "../escape"), ("lnk/pwned", None)],
        [("lnk", "sub"), ("lnk/pwned", None)],
        [("lnk", "/absolute")],
        [("a/../../escape", None)],
    ],
)
def test_extract_refuses_zips_that_write_outside_the_out_folder(
    tmp_path: Path,
    members: list[tuple[str, str | None]],
) -> None:
    archive = tmp_path / "evil.zip"
    with zipfile.ZipFile(archive, "w") as zipped:
        for name, link_target in members:
            if link_target is None:
                zipped.writestr(name, b"pwned")
            else:
                zipped.writestr(symlink_member(name), link_target)
    assert not is_successful(fs.extract_build(archive, tmp_path / "out" / "x"))
    assert not (tmp_path / "out" / "escape").exists()
    assert not (tmp_path / "out" / "x" / "lnk").exists()


# --- signatures and the per-base pipeline -----------------------------------------------


def _verify_all(_: Path) -> Result[bool, Problem]:
    return Success(True)  # noqa: FBT003 (the verdict)


def _verify_except_rebuilt(artifact: Path) -> Result[bool, Problem]:
    return Success("rebuild" not in artifact.parts)


def test_signatures_list_every_exe(builds: Builds) -> None:
    checks = value_of(check_signatures(Platform.WINDOWS64, builds.new, _verify_all))
    assert [(c.artifact, c.valid) for c in checks] == [("Decentraland.exe", True)]


def test_delta_for_base(builds: Builds, tools: HDiffPatch, tmp_path: Path) -> None:
    base_zip = zip_dir(builds.base, tmp_path / "base.zip")
    out = tmp_path / "out"

    def run(base: str, archive: Path, verify: Verifier) -> Result[BaseResult, Problem]:
        inputs = pipeline.BaseInputs(VERSION, Platform.WINDOWS64, builds.new, builds.zip, base, archive, out)
        return pipeline.delta_for_base(inputs, tools, verify)

    value_of(run(BASE_TAG, base_zip, _verify_all))
    assert (out / "from" / BASE_TAG).is_dir()
    assert (out / f"base-{BASE_TAG}.json").is_file()

    signatures_differ = run("v0.9.0-alpha", base_zip, _verify_except_rebuilt)
    assert "signature verification of the rebuilt build differs" in problem_of(signatures_differ)
    assert not (out / "from/v0.9.0-alpha").exists()
    assert not (out / "base-v0.9.0-alpha.json").exists()

    corrupt = write(tmp_path / "corrupt.zip", base_zip.read_bytes()[:100])
    assert "cannot extract" in problem_of(run("v0.8.0-alpha", corrupt, _verify_all))
    assert (out / f"base-{BASE_TAG}.json").is_file()


# --- command line -----------------------------------------------------------------------


def test_cli_end_to_end(builds: Builds, tools: HDiffPatch, tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    os.environ["HDIFFZ"], os.environ["HPATCHZ"] = tools.hdiffz, tools.hpatchz
    out = tmp_path / "out"
    assert main(["make-delta", str(builds.base), str(builds.new), BASE_TAG, str(out)]) == 0
    capsys.readouterr()
    base_result = out / f"base-{BASE_TAG}.json"
    assert main(["manifest", VERSION, "windows64", str(builds.new), str(builds.zip), str(base_result)]) == 0
    manifest_file = write(tmp_path / "manifest.json", capsys.readouterr().out.encode())
    rebuild = tmp_path / "rebuild"
    assert main(["verify", str(builds.base), str(out), str(manifest_file), BASE_TAG, str(rebuild)]) == 0
    assert capsys.readouterr().out.strip() == str(rebuild.resolve())
    assert main(["summary", str(manifest_file), "v0.9.0-alpha"]) == 0
    assert f"| {BASE_TAG} |" in capsys.readouterr().out


def test_cli_reports_errors_with_exit_code_1(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    assert main(["tree", str(tmp_path / "missing")]) == 1
    assert "::error::not a directory" in capsys.readouterr().err
    assert main(["make-delta", str(tmp_path), str(tmp_path), "a/b", str(tmp_path)]) == 1
    assert "not a release tag: 'a/b'" in capsys.readouterr().err
