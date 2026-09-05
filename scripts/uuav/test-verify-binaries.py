#!/usr/bin/env python3
"""Tests for verify-binaries.py: the detection path and --update toolchain handling.

Run: python3 scripts/uuav/test-verify-binaries.py

Each test drives the real script as a subprocess against a synthetic
repository, because the properties under test are end-to-end: corrupting a
committed artifact or a build input must turn the exit status non-zero
(DetectionTests - the property the lock exists for), and a relock must either
move targets.<t>.rust.toolchain or refuse, never writing a lock whose other
pins were refreshed around a stale toolchain identity (UpdateToolchainTests).
"""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

SCRIPT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                      "verify-binaries.py")
LOCK_REL = os.path.join("scripts", "uuav", "uuav-binaries.lock.json")
TARGET = "windows-x86_64"


def make_repo(toolchain: dict | None, manifest_version: str | None = None,
              toolchain_comment: list | None = None) -> str:
    root = tempfile.mkdtemp(prefix="uuav-lock-test-")
    os.makedirs(os.path.join(root, "scripts", "uuav"))
    os.makedirs(os.path.join(root, "native", "src"))
    os.makedirs(os.path.join(root, "native", "out"))
    with open(os.path.join(root, "native", "src", "lib.rs"), "w",
              encoding="utf-8") as handle:
        handle.write("pub fn probe() {}\n")

    if manifest_version is not None:
        # A [dependencies] version after the [package] table: the scanner must
        # read this manifest's own version, not the first one in the file.
        with open(os.path.join(root, "native", "Cargo.toml"), "w",
                  encoding="utf-8") as handle:
            handle.write(f'[package]\nname = "uuav-core"\n'
                         f'version = "{manifest_version}"\n\n'
                         f'[dependencies]\nlibc = "0.2"\n'
                         f'anyhow = {{ version = "9.9.9" }}\n')

    rust = {
        "target": "x86_64-pc-windows-gnu",
        "rustc": "rustc 0.0.0-previous",
        "crate_version": "0.0.0-previous",
        "repo_commit": "0" * 40,
        "source_digest": "0" * 64,
        "source_files": 1,
    }
    if toolchain is not None:
        rust["toolchain"] = toolchain
    if toolchain_comment is not None:
        rust["toolchain_comment"] = toolchain_comment
    lock = {
        "schema": 1,
        "rust_source": {"path": "native/src", "suffix": ".rs"},
        "build_inputs": {},
        "targets": {TARGET: {"runtime_dir": "native/out", "rust": rust,
                             "ffmpeg": {}, "artifacts": []}},
    }
    with open(os.path.join(root, LOCK_REL), "w", encoding="utf-8") as handle:
        json.dump(lock, handle, indent=2)
        handle.write("\n")
    return root


def run(root: str, *args: str) -> subprocess.CompletedProcess:
    return subprocess.run([sys.executable, SCRIPT, "--repo", root, *args],
                          capture_output=True, text=True)


def read_lock(root: str) -> dict:
    with open(os.path.join(root, LOCK_REL), encoding="utf-8") as handle:
        return json.load(handle)


def lock_text(root: str) -> str:
    with open(os.path.join(root, LOCK_REL), encoding="utf-8") as handle:
        return handle.read()


def host_identity(tool: str, cwd: str) -> str | None:
    try:
        result = subprocess.run([tool, "--version"], cwd=cwd,
                                capture_output=True, text=True)
    except OSError:
        return None
    output = (result.stdout + result.stderr).strip()
    return output.splitlines()[0].strip() if output else None


def make_detection_repo() -> str:
    """A repo with one cargo artifact, one enforced and one pending input.

    Digests start empty and are filled by a real `--update` run, so the tests
    exercise the same write-then-verify cycle the lock lives through.
    """
    root = tempfile.mkdtemp(prefix="uuav-lock-test-")
    os.makedirs(os.path.join(root, "scripts", "uuav"))
    os.makedirs(os.path.join(root, "native", "src"))
    os.makedirs(os.path.join(root, "native", "out"))
    with open(os.path.join(root, "native", "src", "lib.rs"), "w",
              encoding="utf-8") as handle:
        handle.write("pub fn probe() {}\n")
    with open(os.path.join(root, "native", "out", "plugin.bin"), "wb") as handle:
        handle.write(bytes(range(256)) * 16)
    with open(os.path.join(root, "native", "config.toml"), "w",
              encoding="utf-8") as handle:
        handle.write("[build]\ntarget-dir = \".target\"\n")
    with open(os.path.join(root, "native", "notes.toml"), "w",
              encoding="utf-8") as handle:
        handle.write("tracked = true\n")

    lock = {
        "schema": 1,
        "rust_source": {"path": "native/src", "suffix": ".rs"},
        "build_inputs": {
            "cargo_config": {"kind": "file", "path": "native/config.toml",
                             "sha256": ""},
            "notes": {"kind": "file", "path": "native/notes.toml",
                      "sha256": "",
                      "pending": "tracked from birth, pinned at first relock"},
        },
        "targets": {TARGET: {
            "runtime_dir": "native/out",
            "rust": {"target": "x86_64-pc-windows-gnu",
                     "rustc": "rustc 0.0.0-test",
                     "source_digest": "", "source_files": 0},
            "ffmpeg": {"configure_expected": []},
            "artifacts": [{"path": "native/out/plugin.bin", "sha256": "",
                           "bytes": 0, "produced_by": "cargo"}],
        }},
    }
    with open(os.path.join(root, LOCK_REL), "w", encoding="utf-8") as handle:
        json.dump(lock, handle, indent=2)
        handle.write("\n")
    return root


class DetectionTests(unittest.TestCase):
    """Flipping a byte anywhere the lock covers must turn the exit non-zero."""

    def setUp(self):
        self.root = make_detection_repo()
        result = run(self.root, "--update")
        self.assertEqual(result.returncode, 0,
                         "the baseline relock must succeed: "
                         + result.stdout + result.stderr)
        verify = run(self.root)
        self.assertEqual(verify.returncode, 0,
                         "a freshly relocked repo must verify clean: "
                         + verify.stdout + verify.stderr)

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def artifact(self) -> str:
        return os.path.join(self.root, "native", "out", "plugin.bin")

    def test_flipped_artifact_byte_fails_and_names_the_artifact(self):
        with open(self.artifact(), "r+b") as handle:
            handle.seek(100)
            original = handle.read(1)
            handle.seek(100)
            handle.write(bytes([original[0] ^ 0xFF]))
        result = run(self.root)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("plugin.bin", result.stdout)
        self.assertIn("sha256", result.stdout)

    def test_size_changing_corruption_fails(self):
        with open(self.artifact(), "ab") as handle:
            handle.write(b"\x00")
        result = run(self.root)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("plugin.bin", result.stdout)

    def test_missing_artifact_fails(self):
        os.remove(self.artifact())
        result = run(self.root)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("missing shipped binary", result.stdout)

    def test_edited_build_input_fails_without_a_relock(self):
        with open(os.path.join(self.root, "native", "config.toml"), "a",
                  encoding="utf-8") as handle:
            handle.write("rustflags = [\"-C\", \"opt-level=3\"]\n")
        result = run(self.root)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("changed but the binaries were not relocked",
                      result.stdout)

    def test_edited_rust_source_fails_without_a_relock(self):
        with open(os.path.join(self.root, "native", "src", "lib.rs"), "a",
                  encoding="utf-8") as handle:
            handle.write("pub fn extra() {}\n")
        result = run(self.root)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("native/src", result.stdout)

    def test_pending_input_is_reported_but_never_fails(self):
        with open(os.path.join(self.root, "native", "notes.toml"), "a",
                  encoding="utf-8") as handle:
            handle.write("edited = true\n")
        result = run(self.root)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("PENDING", result.stdout)

    def test_unlisted_binary_in_runtime_dir_fails(self):
        with open(os.path.join(self.root, "native", "out", "stray.dll"),
                  "wb") as handle:
            handle.write(b"MZ unlisted")
        result = run(self.root)
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("stray.dll", result.stdout)
        self.assertIn("no entry in the lock", result.stdout)

    def test_unlisted_binary_fails_even_on_update(self):
        with open(os.path.join(self.root, "native", "out", "stray"),
                  "wb") as handle:
            handle.write(b"\x7fELF unlisted extensionless helper")
        result = run(self.root, "--update")
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        self.assertIn("stray", result.stdout)

    def test_non_shippable_files_in_runtime_dir_are_ignored(self):
        out = os.path.join(self.root, "native", "out")
        for name in ("plugin.bin.meta", "doctor.sh"):
            with open(os.path.join(out, name), "w", encoding="utf-8") as handle:
                handle.write("not a binary\n")
        result = run(self.root)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)


class UpdateToolchainTests(unittest.TestCase):
    def setUp(self):
        self.roots = []

    def tearDown(self):
        for root in self.roots:
            shutil.rmtree(root, ignore_errors=True)

    def repo(self, toolchain, **kwargs):
        root = make_repo(toolchain, **kwargs)
        self.roots.append(root)
        return root

    def write_toolchain_file(self, root, lines):
        path = os.path.join(root, "toolchain-test.txt")
        with open(path, "w", encoding="utf-8") as handle:
            handle.write("\n".join(lines) + "\n")
        return path

    def test_update_refuses_mismatched_toolchain(self):
        """A relock on a host whose rustc differs from the pin must refuse and
        leave the lock byte-identical, not rewrite everything around a stale
        toolchain identity."""
        root = self.repo({"rustc": "rustc 999.999.999 (0000000 2099-01-01)",
                          "cargo": "cargo 999.999.999 (0000000 2099-01-01)"})
        before = lock_text(root)
        result = run(root, "--update", "--only", TARGET)
        self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
        self.assertIn("rust.toolchain", result.stderr)
        after = lock_text(root)
        self.assertEqual(before, after,
                         "a refused relock must not touch the lock")

    def test_update_refuses_unprobeable_component(self):
        """A pinned component this host cannot probe (windows_sdk outside a VS
        environment, xcode on non-macOS) is a refusal, not a silent keep."""
        pin = {"rustc": "rustc 999.999.999", "xcode": "Xcode 99.9"}
        if sys.platform == "darwin":
            pin = {"rustc": "rustc 999.999.999",
                   "windows_sdk": "10.0.99999.0"}
        root = self.repo(pin)
        result = run(root, "--update", "--only", TARGET)
        self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
        self.assertIn("cannot probe", result.stderr)

    def test_update_with_toolchain_file_moves_the_pin(self):
        root = self.repo({"rustc": "rustc 0.0.0-previous",
                          "cargo": "cargo 0.0.0-previous",
                          "comment": ["kept verbatim"]})
        path = self.write_toolchain_file(root, [
            "rustc: rustc 9.9.9-test (aaaaaaa 2026-01-01)",
            "cargo: cargo 9.9.9-test (bbbbbbb 2026-01-01)",
            "gcc: gcc (test) 99.9.0",
        ])
        result = run(root, "--update", "--only", TARGET, "--toolchain", path)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        rust = read_lock(root)["targets"][TARGET]["rust"]
        self.assertEqual(rust["toolchain"]["rustc"],
                         "rustc 9.9.9-test (aaaaaaa 2026-01-01)")
        self.assertEqual(rust["toolchain"]["cargo"],
                         "cargo 9.9.9-test (bbbbbbb 2026-01-01)")
        self.assertEqual(rust["toolchain"]["comment"], ["kept verbatim"],
                         "the pin's comment is not a component")
        self.assertNotIn("gcc", rust["toolchain"],
                         "extra recorded components are context, not new pins")
        self.assertEqual(rust["rustc"], "rustc 9.9.9-test (aaaaaaa 2026-01-01)",
                         "the top-level rustc identity moves with the pin")

    def test_update_with_incomplete_toolchain_file_refuses(self):
        root = self.repo({"rustc": "rustc 0.0.0-previous",
                          "cargo": "cargo 0.0.0-previous"})
        before = lock_text(root)
        path = self.write_toolchain_file(root, ["rustc: rustc 9.9.9-test"])
        result = run(root, "--update", "--only", TARGET, "--toolchain", path)
        self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
        self.assertIn("cargo", result.stderr)
        after = lock_text(root)
        self.assertEqual(before, after)

    def test_update_on_matching_host_proceeds(self):
        probe_root = self.repo(None)
        native = os.path.join(probe_root, "native")
        rustc = host_identity("rustc", native)
        cargo = host_identity("cargo", native)
        if not rustc or not cargo:
            self.skipTest("no rustc/cargo on this host")
        root = self.repo({"rustc": rustc, "cargo": cargo})
        result = run(root, "--update", "--only", TARGET)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        rust = read_lock(root)["targets"][TARGET]["rust"]
        self.assertEqual(rust["toolchain"], {"rustc": rustc, "cargo": cargo})
        self.assertNotEqual(rust["source_digest"], "0" * 64,
                            "the relock itself still ran")

    def test_update_without_pin_needs_no_toolchain(self):
        root = self.repo(None)
        result = run(root, "--update", "--only", TARGET)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertNotEqual(
            read_lock(root)["targets"][TARGET]["rust"]["source_digest"],
            "0" * 64)

    def test_update_with_toolchain_file_seeds_a_missing_pin(self):
        """A target that has never been pinned gets its first pin from the
        recorded build, not silently nothing.

        This is the path the lock's own instructions describe for
        windows-x86_64. It used to return early on 'no components to move',
        so the relock rewrote every other pin and left rust.rustc reading
        'unknown - not recorded ...' - Gate B skipping forever, with the
        command that was supposed to fix it reporting success."""
        root = self.repo(None, toolchain_comment=["no toolchain pin yet"])
        path = self.write_toolchain_file(root, [
            "rustc: rustc 9.9.9-test (aaaaaaa 2026-01-01)",
            "cargo: cargo 9.9.9-test (bbbbbbb 2026-01-01)",
            "gcc: gcc (test) 99.9.0",
        ])
        result = run(root, "--update", "--only", TARGET, "--toolchain", path)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        rust = read_lock(root)["targets"][TARGET]["rust"]
        self.assertEqual(rust["toolchain"], {
            "rustc": "rustc 9.9.9-test (aaaaaaa 2026-01-01)",
            "cargo": "cargo 9.9.9-test (bbbbbbb 2026-01-01)",
            "gcc": "gcc (test) 99.9.0",
        }, "a first pin is as wide as the record, gcc included")
        self.assertEqual(rust["rustc"], "rustc 9.9.9-test (aaaaaaa 2026-01-01)")
        self.assertNotIn("toolchain_comment", rust,
                         "the note saying there is no pin outlived the pin")

    def test_seeded_pin_skips_components_no_host_can_probe(self):
        """`ld` is recorded by the macOS build and pinned by nobody: pinning a
        component probe_toolchain_component() cannot check would make every
        later relock without a toolchain file refuse with 'cannot probe'."""
        root = self.repo(None)
        path = self.write_toolchain_file(root, [
            "rustc: rustc 9.9.9-test",
            "ld: @(#)PROGRAM:ld PROJECT:ld-1234",
        ])
        result = run(root, "--update", "--only", TARGET, "--toolchain", path)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        toolchain = read_lock(root)["targets"][TARGET]["rust"]["toolchain"]
        self.assertEqual(toolchain, {"rustc": "rustc 9.9.9-test"})

    def test_update_records_the_crate_version(self):
        root = self.repo(None, manifest_version="0.3.0")
        result = run(root, "--update", "--only", TARGET)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(
            read_lock(root)["targets"][TARGET]["rust"]["crate_version"],
            "0.3.0", "the version is read from [package], not [dependencies]")

    def test_update_leaves_crate_version_alone_without_a_manifest(self):
        """Derived, never invented: no manifest to read means no rewrite."""
        root = self.repo(None)
        result = run(root, "--update", "--only", TARGET)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(
            read_lock(root)["targets"][TARGET]["rust"]["crate_version"],
            "0.0.0-previous")

    def test_update_records_the_head_commit(self):
        root = self.repo(None)
        git = ("git", "-C", root, "-c", "user.name=t", "-c", "user.email=t@t")
        for argv in ((*git, "init", "--quiet"),
                     (*git, "add", "-A"),
                     (*git, "commit", "--quiet", "--no-gpg-sign", "-m", "fixture")):
            done = subprocess.run(argv, capture_output=True, text=True)
            if done.returncode != 0:
                self.skipTest(f"git fixture unavailable: {done.stderr.strip()}")
        head = subprocess.run((*git, "rev-parse", "HEAD"), capture_output=True,
                              text=True).stdout.strip()
        result = run(root, "--update", "--only", TARGET)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(
            read_lock(root)["targets"][TARGET]["rust"]["repo_commit"], head)

    def test_update_leaves_repo_commit_alone_outside_a_checkout(self):
        root = self.repo(None)
        result = run(root, "--update", "--only", TARGET)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(
            read_lock(root)["targets"][TARGET]["rust"]["repo_commit"], "0" * 40)

    def test_toolchain_flag_requires_update_and_only(self):
        root = self.repo(None)
        path = self.write_toolchain_file(root, ["rustc: rustc 9.9.9-test"])
        for args in (("--toolchain", path),
                     ("--update", "--toolchain", path)):
            result = run(root, *args)
            self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
            self.assertIn("--toolchain", result.stderr)


if __name__ == "__main__":
    unittest.main(verbosity=2)
