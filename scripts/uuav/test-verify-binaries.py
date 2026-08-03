#!/usr/bin/env python3
"""Tests for verify-binaries.py's --update toolchain handling.

Run: python3 scripts/uuav/test-verify-binaries.py

Each test drives the real script as a subprocess against a synthetic
repository, because the property under test is end-to-end: a relock must
either move targets.<t>.rust.toolchain or refuse, and must never write a lock
whose other pins were refreshed around a stale toolchain identity.
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


def make_repo(toolchain: dict | None) -> str:
    root = tempfile.mkdtemp(prefix="uuav-lock-test-")
    os.makedirs(os.path.join(root, "scripts", "uuav"))
    os.makedirs(os.path.join(root, "native", "src"))
    with open(os.path.join(root, "native", "src", "lib.rs"), "w",
              encoding="utf-8") as handle:
        handle.write("pub fn probe() {}\n")

    rust = {
        "target": "x86_64-pc-windows-msvc",
        "rustc": "rustc 0.0.0-previous",
        "source_digest": "0" * 64,
        "source_files": 1,
    }
    if toolchain is not None:
        rust["toolchain"] = toolchain
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


class UpdateToolchainTests(unittest.TestCase):
    def setUp(self):
        self.roots = []

    def tearDown(self):
        for root in self.roots:
            shutil.rmtree(root, ignore_errors=True)

    def repo(self, toolchain):
        root = make_repo(toolchain)
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
        """A pinned component this host cannot probe (cl on non-Windows) is a
        refusal, not a silent keep."""
        pin = {"rustc": "rustc 999.999.999", "cl": "Microsoft cl 19.44"}
        if os.name == "nt":
            pin = {"rustc": "rustc 999.999.999",
                   "xcode": "Xcode 26.6 Build version 17F113"}
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
            "windows_sdk: 10.0.99999.0",
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
        self.assertNotIn("windows_sdk", rust["toolchain"],
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
