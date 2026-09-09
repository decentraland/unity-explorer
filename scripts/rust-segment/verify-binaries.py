#!/usr/bin/env python3
"""Verify the committed RustSegment native binaries against scripts/rust-segment/rust-segment-binaries.lock.json.

A sibling of scripts/uuav/verify-binaries.py for the analytics crate under
Explorer/Assets/Plugins/RustSegment/.native, minus the FFmpeg provenance checks
that crate has no use for. Four independent checks, each of which fails the run
on its own:

  artifacts       sha256 of every shipped .dylib/.dll/executable matches the lock
  build-inputs    sha256 of every manifest, cargo config and workspace source
                  tree that feeds a shipped binary matches the lock, so a
                  change to a build input that was not followed by a rebuild is
                  caught. Two kinds:
                    file                 sha256 of one file (Cargo.lock is one:
                                         a single crate ships, so every locked
                                         package reaches the binary)
                    tree                 order-independent digest over a source
                                         tree, filtered by suffix and with
                                         directories pruned by name
                  An input carrying "pending" is reported but never fails: it
                  is tracked from the moment it exists and pinned at the
                  relock that first ships a binary built from it.
  rust-source     the digest of .native/src matches what this target's
                  cargo-built binaries were built from - the crate has no
                  upstream revision to pin, so the source digest is the pin
  runtime-dir     every shippable binary in a target's runtime dir is named by
                  the lock, so a file dropped next to the listed ones cannot
                  reach players with no recorded hash

`--update` refreshes the machine-derived fields (artifact hashes and sizes,
build-input hashes, source digest) in place. It never clears a build input's
"pending" key, because promoting an input from tracked to enforced is a
deliberate human move.

targets.<t>.rust.crate_version and .repo_commit are re-derived by --update too,
from .native/Cargo.toml and `git rev-parse HEAD`, but no check compares them.
They describe the relock rather than pin it: a crate version bump already
reaches the lock through cargo_manifest's digest, and repo_commit is false one
commit after it is written, so failing on either would fail correct runs. They
are derived rather than hand-copied only so they cannot quietly contradict the
binaries beside them.

targets.<t>.rust.toolchain - the host identities Gate B (reproduces-lock.py)
pins byte reproduction against - moves with the relock, not silently: relocking
leaves it as it is only when every pinned component matches this host, verified
with the same commands the build workflow's 'Record the toolchain actually
used' step runs. Otherwise --update refuses, because rewriting every other pin
while keeping the old toolchain identity would leave Gate B either skipping
forever or claiming a reproduction the relock itself just invalidated. To
relock a target built elsewhere (or on a changed toolchain), pass
`--toolchain toolchain-<os>.txt` - the file that workflow step records - and
the pin is rewritten from it along with everything else.

A target with no toolchain pin at all is the same instruction seen from the
start: `--toolchain` gives it its first one, taken from every component of the
record probe_toolchain_component() knows how to check again later, and drops any
"toolchain_comment" saying it has none. Without the flag it relocks unpinned and
Gate B goes on skipping it, which is what an unpinned target means.

`--update --only TARGET` relocks one target and leaves every other target's
pins alone. The two platforms are built on two different machines, so the
common case is that only one of them has just been rebuilt; relocking both
from a tree only one was built from would record, of the target that was not
rebuilt, a pin that is simply false. The shared build_inputs are global and
are still refreshed, because they belong to no single target - a target whose
binaries predate them stays red on its own per-target pins.

`--report TARGET` compares a freshly built target against the lock and writes
rust-segment-<TARGET>.sha256; used by the build workflow, where artifact hashes
are expected to differ from the committed ones until the relock (see
Explorer/Assets/Plugins/RustSegment/README.md).

Exit status: 0 all checks passed, 1 at least one check failed, 2 bad usage.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import sys

LOCK_REL = "scripts/rust-segment/rust-segment-binaries.lock.json"

IN_ACTIONS = os.environ.get("GITHUB_ACTIONS") == "true"


def fail(message: str) -> None:
    print(f"::error::{message}" if IN_ACTIONS else f"FAIL: {message}")


def note(message: str) -> None:
    print(f"  {message}")


def sha256_of(path: str) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def tree_sha256(root: str, suffix, prune=()) -> tuple[str, int]:
    """Order-independent digest over every *suffix* file under *root*.

    Hashing the Rust sources, not just Cargo.toml/Cargo.lock, is what makes an
    edit to a workspace source tree that was never followed by a rebuild
    visible: the shipped binary's own hash still matches the lock in that
    case, because nobody rebuilt it.

    *suffix* is one extension or a list of them. Directory names in *prune* are
    skipped wherever they occur - `tests`, `examples` and the build
    directories, none of which end up inside a shipped binary, so including
    them would turn the lock red on edits that cannot change what ships.
    """
    suffixes = (suffix,) if isinstance(suffix, str) else tuple(suffix)
    pruned = set(prune)
    entries = []
    for base, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in pruned]
        for name in sorted(files):
            if not name.endswith(suffixes):
                continue
            full = os.path.join(base, name)
            rel = os.path.relpath(full, root).replace(os.sep, "/")
            entries.append(f"{rel} {sha256_of(full)}")
    digest = hashlib.sha256()
    for line in sorted(entries):
        digest.update(line.encode() + b"\n")
    return digest.hexdigest(), len(entries)


def package_version(path: str) -> str | None:
    """The `[package] version` of one Cargo.toml, or None if it has none.

    Hand-parsed: this script is a gate that must run on whatever python3 a
    runner happens to have, without tomllib and without a pip install.
    Only the [package] table is read, so a [dependencies] entry pinning some
    crate's version cannot be mistaken for this manifest's own.
    """
    in_package = False
    try:
        with open(path, encoding="utf-8") as handle:
            for raw in handle:
                line = raw.strip()
                if line.startswith("["):
                    in_package = line == "[package]"
                    continue
                if in_package and line.startswith("version = "):
                    return line.split("=", 1)[1].strip().strip('"')
    except OSError:
        return None
    return None


def head_commit(repo: str) -> str | None:
    """The commit a relock is being recorded at, or None outside a checkout."""
    try:
        result = subprocess.run(["git", "-C", repo, "rev-parse", "HEAD"],
                                capture_output=True, text=True)
    except OSError:
        return None
    commit = result.stdout.strip()
    return commit if result.returncode == 0 and commit else None


def refresh_provenance_notes(repo: str, lock: dict, spec: dict) -> None:
    """Re-derive the two fields that describe, rather than pin, this relock.

    crate_version and repo_commit are documentation: nothing compares them, and
    nothing should. A version bump already reaches the lock through
    cargo_manifest's digest, and repo_commit is false one commit after it is
    written, so checking either would fail runs that are correct. What they must
    not do is contradict the relock that wrote them - the pair drifting apart
    from the binaries beside them (crate_version 0.2.0 against a 0.3.0
    workspace) is what made them worth deriving instead of hand-copying.
    """
    manifest = os.path.dirname(os.path.join(repo, lock["rust_source"]["path"]))
    version = package_version(os.path.join(manifest, "Cargo.toml"))
    if version:
        spec["rust"]["crate_version"] = version

    commit = head_commit(repo)
    if commit:
        spec["rust"]["repo_commit"] = commit


LFS_POINTER_PREFIX = b"version https://git-lfs.github.com/spec/v1"


def is_lfs_pointer(path: str) -> bool:
    """An unfetched LFS file is a ~130 byte text stub, not the binary."""
    with open(path, "rb") as handle:
        return handle.read(len(LFS_POINTER_PREFIX)) == LFS_POINTER_PREFIX


def read_toolchain_file(path: str) -> dict:
    """The `component: identity` lines a toolchain-<os>.txt records."""
    recorded = {}
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            key, sep, value = line.partition(":")
            if sep:
                recorded[key.strip()] = value.strip()
    return recorded


def _first_line(argv, cwd) -> str | None:
    try:
        result = subprocess.run(argv, cwd=cwd, capture_output=True, text=True)
    except OSError:
        return None
    output = (result.stdout + result.stderr).strip()
    return output.splitlines()[0].strip() if output else None


PROBEABLE_COMPONENTS = ("rustc", "cargo", "clang", "gcc", "msvc", "windows_sdk",
                        "xcode", "sdk")

VSWHERE = os.path.join(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)"),
                       "Microsoft Visual Studio", "Installer", "vswhere.exe")


def msvc_tools_version() -> str | None:
    """The VC++ toolset link.exe comes from, as `msvc: 14.44.35207`.

    The shipped DLL is linked by the MSVC linker, which rustc locates itself
    without a vcvars shell - so nothing in the environment names the toolset
    and WindowsSDKVersion is unset. vswhere is the one stable way to find the
    installation, and Microsoft.VCToolsVersion.default.txt inside it names the
    default toolset the same way the build workflow's 'Record the toolchain
    actually used' step reads it.
    """
    if not os.path.exists(VSWHERE):
        return None
    install = _first_line([VSWHERE, "-latest", "-products", "*", "-requires",
                           "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
                           "-property", "installationPath"], None)
    if not install:
        return None
    marker = os.path.join(install, "VC", "Auxiliary", "Build",
                          "Microsoft.VCToolsVersion.default.txt")
    try:
        with open(marker, encoding="utf-8") as handle:
            version = handle.read().strip()
    except OSError:
        return None
    return version or None


def probe_toolchain_component(component: str, native_dir: str) -> str | None:
    """This host's identity for one pinned component, or None if unprobeable.

    Each probe is the command the build workflow's 'Record the toolchain
    actually used' step runs for the same component, so a value compared or
    written here is the value Gate B will later compare against.
    """
    if component in ("rustc", "cargo", "clang"):
        return _first_line([component, "--version"], native_dir)
    if component == "gcc":
        return _first_line(["gcc", "--version"], native_dir)
    if component == "msvc":
        return msvc_tools_version()
    if component == "windows_sdk":
        version = os.environ.get("WindowsSDKVersion")
        return version.rstrip("\\") if version else None
    if component == "xcode":
        try:
            result = subprocess.run(["xcodebuild", "-version"], cwd=native_dir,
                                    capture_output=True, text=True)
        except OSError:
            return None
        return " ".join(result.stdout.split()) or None
    if component == "sdk":
        version = _first_line(["xcrun", "--sdk", "macosx", "--show-sdk-version"],
                              native_dir)
        build = _first_line(["xcrun", "--sdk", "macosx",
                             "--show-sdk-build-version"], native_dir)
        return f"macosx{version} ({build})" if version and build else None
    return None


def seed_toolchain_pin(spec: dict, toolchain_file: str) -> None:
    """Give a target its first targets.<target>.rust.toolchain, from the build.

    A target with no pin is one Gate B has never been able to check: it skips,
    naming the file whose identities would let it. Every recorded component the
    build reached is worth pinning - for x86_64-pc-windows-gnu the mingw gcc
    reached the bytes as directly as rustc did, which is why
    .cargo/config.toml pins its path - so the pin starts as wide as the record.

    Only components probe_toolchain_component() knows, though: a pinned
    component no host can probe makes every later relock that does not carry a
    toolchain file refuse with 'cannot probe', so seeding one would trap the
    next rebuild. macOS records `ld` for that reason and does not pin it.
    """
    recorded = read_toolchain_file(toolchain_file)
    spec["rust"]["toolchain"] = {component: identity
                                 for component, identity in recorded.items()
                                 if component in PROBEABLE_COMPONENTS}
    # Whatever this said, it said the target had no pin.
    spec["rust"].pop("toolchain_comment", None)


def resolve_toolchain_pin(target: str, spec: dict, toolchain_file: str | None,
                          native_dir: str) -> list[str]:
    """Move or hold targets.<target>.rust.toolchain for a relock; never stale it.

    Without this, --update rewrote every machine-derived pin while silently
    keeping the old toolchain identity: a relock on a different rustc / mingw /
    SDK left Gate B pinned to a host that did not produce the new binaries, so
    it either 'skipped' forever or claimed a reproduction the relock had just
    invalidated. Returns problems; on success with a file, mutates the pin.
    """
    pinned = spec["rust"].get("toolchain")
    components = {key: value for key, value in pinned.items() if key != "comment"} \
        if isinstance(pinned, dict) else {}

    if not components:
        # No pin to move. With a recorded build to take one from, this relock is
        # the moment the target gets its first; without one it relocks unpinned,
        # exactly as before, and Gate B keeps skipping.
        if toolchain_file:
            seed_toolchain_pin(spec, toolchain_file)
            seeded = spec["rust"]["toolchain"]
            if "rustc" in seeded:
                spec["rust"]["rustc"] = seeded["rustc"]
        return []

    if toolchain_file:
        recorded = read_toolchain_file(toolchain_file)
        missing = sorted(set(components) - set(recorded))
        if missing:
            return [f"[{target}] {os.path.basename(toolchain_file)} records no "
                    f"'{', '.join(missing)}' line(s), but the lock pins them; a "
                    f"partial identity cannot establish what built these binaries"]
        for component in components:
            pinned[component] = recorded[component]
        if "rustc" in recorded:
            spec["rust"]["rustc"] = recorded["rustc"]
        return []

    differing = []
    for component, identity in sorted(components.items()):
        actual = probe_toolchain_component(component, native_dir)
        if actual is None:
            differing.append(f"{component}: lock pins '{identity}', this host "
                             f"cannot probe it")
        elif actual != identity:
            differing.append(f"{component}: lock pins '{identity}', this host "
                             f"has '{actual}'")
    if not differing:
        return []
    detail = "\n    ".join(differing)
    return [f"[{target}] refusing to relock on a toolchain that is not the one "
            f"the lock pins - the old identity would stay in rust.toolchain and "
            f"Gate B would pin a reproduction this relock just invalidated\n"
            f"    {detail}\n"
            f"    Relock on the pinned toolchain, or pass --toolchain "
            f"toolchain-<os>.txt (the file the build workflow's 'Record the "
            f"toolchain actually used' step writes) to move the pin with the "
            f"relock."]


def check_artifacts(repo: str, target: str, spec: dict, update: bool) -> list[str]:
    problems = []
    for artifact in spec["artifacts"]:
        path = os.path.join(repo, artifact["path"])
        if not os.path.exists(path):
            problems.append(
                f"[{target}] missing shipped binary: {artifact['path']}")
            continue

        if is_lfs_pointer(path):
            problems.append(
                f"[{target}] {artifact['path']} is an unfetched Git LFS pointer, "
                f"not the binary.\n"
                f"    Run: git lfs pull --include "
                f"'Explorer/Assets/Plugins/RustSegment/SegmentServerWrap/Libraries/**'")
            continue

        actual_sha = sha256_of(path)
        actual_bytes = os.path.getsize(path)
        if update:
            artifact["sha256"] = actual_sha
            artifact["bytes"] = actual_bytes
            continue

        if actual_sha != artifact["sha256"]:
            problems.append(
                f"[{target}] {artifact['path']}\n"
                f"    expected sha256 {artifact['sha256']} ({artifact['bytes']} bytes)\n"
                f"    actual   sha256 {actual_sha} ({actual_bytes} bytes)")
    return problems


# What counts as a shippable binary inside a runtime dir: native libraries and
# executables by extension, plus extensionless files (a macOS executable has no
# extension). Everything else there (.meta) is not loaded by the player.
SHIPPABLE_SUFFIXES = (".dll", ".dylib", ".exe")


def check_runtime_dir_completeness(repo: str, target: str, spec: dict) -> list[str]:
    """Every shippable binary in the runtime dir must be named by the lock.

    check_artifacts() walks the lock; this walks the directory. Without it the
    lock is an allowlist with no completeness check - a binary dropped into
    the shipped plugin folder next to the listed ones would reach end users
    with no recorded hash and this script would never mention it.

    Runs in --update mode too: a relock can refresh hashes of listed
    artifacts, but only a human can decide that a new binary belongs in the
    shipping set, so an unlisted one always fails.
    """
    runtime_dir = os.path.join(repo, spec["runtime_dir"])
    if not os.path.isdir(runtime_dir):
        return [f"[{target}] missing runtime dir: {spec['runtime_dir']}"]

    listed = {os.path.basename(artifact["path"]) for artifact in spec["artifacts"]}
    problems = []
    for name in sorted(os.listdir(runtime_dir)):
        if not os.path.isfile(os.path.join(runtime_dir, name)):
            continue
        extension = os.path.splitext(name)[1].lower()
        if extension not in SHIPPABLE_SUFFIXES and extension != "":
            continue
        if name not in listed:
            problems.append(
                f"[{target}] {spec['runtime_dir']}/{name} is in the shipped "
                f"plugin folder but has no entry in the lock - its provenance "
                f"is unverified.\n"
                f"    Add it to this target's artifacts and relock with "
                f"--update, or remove it from the folder.")
    return problems


def build_input_digest(path: str, spec: dict) -> tuple[str, int | None]:
    """The digest of one build input, by kind. Returns (digest, file count)."""
    kind = spec.get("kind", "file")
    if kind == "file":
        return sha256_of(path), None
    if kind == "tree":
        return tree_sha256(path, spec["suffix"], spec.get("prune", ()))
    raise ValueError(f"unknown build-input kind '{kind}'")


def check_build_inputs(repo: str, lock: dict, update: bool) -> list[str]:
    problems = []
    for name, spec in lock["build_inputs"].items():
        path = os.path.join(repo, spec["path"])
        exists = os.path.isdir(path) if spec.get("kind") == "tree" \
            else os.path.exists(path)
        if not exists:
            problems.append(f"[build-inputs] missing: {spec['path']}")
            continue

        try:
            actual, count = build_input_digest(path, spec)
        except ValueError as error:
            problems.append(f"[build-inputs] {name}: {error}")
            continue

        if "pending" in spec:
            if update:
                spec["sha256"] = actual
                if count is not None:
                    spec["entries"] = count
            suffix = f", {count} entries" if count is not None else ""
            note(f"[build-inputs] {name}: PENDING {actual[:12]}{suffix} - "
                 f"{spec['pending']}")
            continue

        if update:
            spec["sha256"] = actual
            if count is not None:
                spec["entries"] = count
            continue

        if actual != spec["sha256"]:
            problems.append(
                f"[build-inputs] {name} changed but the binaries were not relocked\n"
                f"    {spec['path']}\n"
                f"    expected sha256 {spec['sha256']}\n"
                f"    actual   sha256 {actual}\n"
                f"    Rebuild the affected target and re-run this script with --update.")
    return problems


def check_rust_source(repo: str, lock: dict, target: str, spec: dict,
                      update: bool) -> list[str]:
    """Tie this target's binaries to the native/src state that produced them.

    The crate has no upstream repository - it is built out of this repo - so
    there is no revision to pin. The digest of the source tree is the pin.
    Recording it per target rather than once globally is deliberate: the two
    platforms are built on two different machines and can easily end up
    shipping binaries from different source states.
    """
    source = lock["rust_source"]
    root = os.path.join(repo, source["path"])
    if not os.path.isdir(root):
        return [f"[{target}] missing Rust source tree: {source['path']}"]

    actual, files = tree_sha256(root, source["suffix"])
    if update:
        spec["rust"]["source_digest"] = actual
        spec["rust"]["source_files"] = files
        return []

    if actual != spec["rust"]["source_digest"]:
        return [f"[{target}] {source['path']} changed but this target's binaries "
                f"were not rebuilt and relocked\n"
                f"    expected digest {spec['rust']['source_digest']} "
                f"({spec['rust']['source_files']} files)\n"
                f"    actual   digest {actual} ({files} files)\n"
                f"    The shipped binaries no longer correspond to native/src."]
    return []


def report_fresh_build(repo: str, lock: dict, target: str) -> int:
    """Compare a just-built target against the lock and write its SHA256SUMS.

    Used by the build workflow. A fresh build is only expected to be
    byte-identical to what is committed on the toolchain the lock pins, and
    that comparison is Gate B's (reproduces-lock.py); here a differing hash is
    reported, not failed. What fails is an artifact the build did not produce.
    """
    spec = lock["targets"][target]
    lines, mismatches, missing = [], 0, 0

    for artifact in spec["artifacts"]:
        path = os.path.join(repo, artifact["path"])
        name = os.path.basename(artifact["path"])
        if not os.path.exists(path):
            print(f"  MISSING  {name}")
            missing += 1
            continue
        actual = sha256_of(path)
        state = "same" if actual == artifact["sha256"] else "DIFFERS"
        mismatches += state == "DIFFERS"
        print(f"  {state:8} {name}  {actual}")
        lines.append(f"{actual}  {name}")

    sums_path = os.path.join(repo, f"rust-segment-{target}.sha256")
    with open(sums_path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")

    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as handle:
            handle.write(f"### {target}\n\n")
            handle.write(f"{len(lines)} artifacts, {mismatches} differing from the "
                         f"committed hashes (expected until the relock - see "
                         f"Explorer/Assets/Plugins/RustSegment/README.md).\n\n")

    if missing:
        fail(f"[{target}] {missing} artifact(s) missing after the build")
        return 1

    print(f"\n{len(lines)} artifacts built, {mismatches} differ from the committed "
          f"hashes. Wrote {os.path.basename(sums_path)}.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--update", action="store_true",
                        help="rewrite machine-derived fields from the working tree")
    parser.add_argument("--only", metavar="TARGET", default=None,
                        help="with --update, relock only this target; every "
                             "other target's pins are left untouched")
    parser.add_argument("--toolchain", metavar="FILE", default=None,
                        help="with --update --only: the toolchain-<os>.txt the "
                             "build recorded; rewrites the target's "
                             "rust.toolchain pin from it")
    parser.add_argument("--report", metavar="TARGET", default=None,
                        help="compare a freshly built target against the lock and "
                             "write rust-segment-<TARGET>.sha256 (used by the build workflow)")
    parser.add_argument("--repo", default=None,
                        help="repository root (default: two levels above this script)")
    args = parser.parse_args()

    repo = args.repo or os.path.dirname(os.path.dirname(os.path.dirname(
        os.path.abspath(__file__))))
    lock_path = os.path.join(repo, LOCK_REL)
    if not os.path.exists(lock_path):
        print(f"FAIL: no lock file at {lock_path}", file=sys.stderr)
        return 2

    with open(lock_path, encoding="utf-8") as handle:
        lock = json.load(handle)

    if args.report:
        if args.report not in lock["targets"]:
            print(f"FAIL: unknown target '{args.report}'; the lock defines "
                  f"{', '.join(lock['targets'])}", file=sys.stderr)
            return 2
        print(f"Fresh build vs {LOCK_REL} - {args.report}")
        return report_fresh_build(repo, lock, args.report)

    if args.only and args.only not in lock["targets"]:
        print(f"FAIL: unknown target '{args.only}'; the lock defines "
              f"{', '.join(lock['targets'])}", file=sys.stderr)
        return 2
    if args.only and not args.update:
        print("FAIL: --only is only meaningful with --update", file=sys.stderr)
        return 2
    if args.toolchain and not (args.update and args.only):
        print("FAIL: --toolchain is only meaningful with --update --only TARGET",
              file=sys.stderr)
        return 2
    if args.toolchain and not os.path.exists(args.toolchain):
        print(f"FAIL: no recorded toolchain at {args.toolchain}", file=sys.stderr)
        return 2

    if args.update:
        native_dir = os.path.dirname(os.path.join(repo, lock["rust_source"]["path"]))
        refusals = []
        for target, spec in lock["targets"].items():
            if args.only in (None, target):
                refusals += resolve_toolchain_pin(target, spec, args.toolchain,
                                                  native_dir)
        if refusals:
            for refusal in refusals:
                print(f"FAIL: {refusal}", file=sys.stderr)
            return 2

    print(f"RustSegment native binary verification ({LOCK_REL})")

    problems = check_build_inputs(repo, lock, args.update)
    for target, spec in lock["targets"].items():
        note(f"{target}: {len(spec['artifacts'])} artifacts, built from "
             f".native/src {spec['rust']['source_digest'][:12]}")
        update_target = args.update and args.only in (None, target)
        if args.update and not update_target:
            note(f"{target}: not relocked (--only {args.only})")
            continue
        problems += check_artifacts(repo, target, spec, update_target)
        problems += check_runtime_dir_completeness(repo, target, spec)
        problems += check_rust_source(repo, lock, target, spec, update_target)
        if update_target:
            refresh_provenance_notes(repo, lock, spec)

    if args.update:
        # newline="\n": the lock is read on every platform and its diff should
        # not depend on which one rewrote it.
        with open(lock_path, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(lock, handle, indent=2)
            handle.write("\n")
        print(f"Updated {LOCK_REL} from the working tree.")
        still_pending = [name for name, spec in lock["build_inputs"].items()
                         if "pending" in spec]
        if still_pending:
            print(f"{len(still_pending)} build input(s) are still PENDING and "
                  f"therefore still cannot fail this script: "
                  f"{', '.join(still_pending)}.")
            print("Delete their \"pending\" key in the relock commit that first "
                  "ships a binary built from them.")
        if problems:
            for problem in problems:
                fail(problem)
            return 1
        return 0

    if problems:
        print()
        for problem in problems:
            fail(problem)
        print()
        print(f"{len(problems)} problem(s). The committed native binaries do not match "
              f"{LOCK_REL}.")
        print("If the change is intended, rebuild the affected target and run:")
        print(f"    python3 scripts/rust-segment/verify-binaries.py --update")
        return 1

    print("All checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
