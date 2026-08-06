#!/usr/bin/env python3
"""Verify the committed UUAV native binaries against scripts/uuav/uuav-binaries.lock.json.

Six independent checks, each of which fails the run on its own:

  artifacts       sha256 of every shipped .dylib/.dll/executable matches the lock
  build-inputs    sha256 of every manifest, cargo config and workspace source
                  tree that feeds a shipped binary matches the lock, so a
                  change to a build input that was not followed by a rebuild is
                  caught. Three kinds:
                    file                 sha256 of one file
                    tree                 order-independent digest over a source
                                         tree, filtered by suffix and with
                                         directories pruned by name
                    cargo-lock-closure   digest over the packages Cargo.lock
                                         resolves for the *shipped* roots only,
                                         so a dependency bump in a workspace
                                         member that ships nothing does not
                                         demand a two-platform rebuild of a
                                         byte-identical binary
                  An input carrying "pending" is reported but never fails: it
                  is tracked from the moment it exists and pinned at the
                  relock that first ships a binary built from it.
  rust-source     the digest of native/src matches what this target's
                  cargo-built binaries were built from - the crate has no
                  upstream revision to pin, so the source digest is the pin
  ffmpeg-builder  the FFmpeg build script is unchanged *in substance*, and the
                  lock's configure_expected still matches the configure line
                  that script actually invokes. builder_sha256 is taken over the
                  script with its comments stripped and its whitespace
                  collapsed, so reflowing a comment does not demand a
                  15-minute rebuild. Only checked for targets whose FFmpeg is
                  built from source (macOS); the Windows FFmpeg is a pinned
                  third-party release asset with no builder script.
  provenance      the FFmpeg configure line embedded in each shipped FFmpeg
                  library matches the one the lock recorded for that target
  drift           each target's configure_observed matches its
                  configure_expected, unless the target sets drift_acknowledged

`--update` refreshes the machine-derived fields (artifact hashes and sizes,
build-input hashes, source digest, configure_observed) in place. It never
touches the pinned upstream identifiers - tag, commit, release asset, asset
hash - those are edited by a human when the pin deliberately moves, and it
never clears a build input's "pending" key, because promoting an input from
tracked to enforced is exactly that kind of deliberate move.

targets.<t>.rust.crate_version and .repo_commit are re-derived by --update too,
from native/Cargo.toml and `git rev-parse HEAD`, but no check compares them.
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
uuav-<TARGET>.sha256; used by the build workflow, where artifact hashes are
expected to differ (see Explorer/Assets/Plugins/UUAV/README.md) but the
configure line is not.

Exit status: 0 all checks passed, 1 at least one check failed, 2 bad usage.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys

LOCK_REL = "scripts/uuav/uuav-binaries.lock.json"

CONFIGURE_START = b"--prefix="
CONFIGURE_MARKER = "--enable-shared"
PRINTABLE = re.compile(rb"[\x20-\x7e]")

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


def parse_cargo_lock(path: str) -> dict:
    """Cargo.lock's `[[package]]` records, keyed by (name, version).

    Hand-parsed rather than via a TOML library: the file is machine-written and
    rigidly shaped, and this script is a gate that must run on whatever python3
    a runner happens to have, without tomllib and without a pip install.
    """
    packages = {}
    current = None
    in_deps = False

    def flush():
        nonlocal current, in_deps
        if current is not None:
            packages[(current["name"], current["version"])] = current
        current, in_deps = None, False

    with open(path, encoding="utf-8") as handle:
        for raw in handle:
            line = raw.strip()
            if line == "[[package]]":
                flush()
                current = {"name": "", "version": "", "checksum": "", "deps": []}
                continue
            if current is None:
                continue
            if line.startswith("["):
                flush()
                continue
            if in_deps:
                if line.startswith("]"):
                    in_deps = False
                else:
                    current["deps"].append(line.strip(",").strip('"'))
                continue
            if line == "dependencies = [":
                in_deps = True
                continue
            for key in ("name", "version", "checksum"):
                if line.startswith(f"{key} = "):
                    current[key] = line.split("=", 1)[1].strip().strip('"')
    flush()
    return packages


def package_version(path: str) -> str | None:
    """The `[package] version` of one Cargo.toml, or None if it has none.

    Hand-parsed for the same reason parse_cargo_lock is: no tomllib, no pip.
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


def cargo_lock_closure(path: str, roots) -> tuple[str, int]:
    """Digest over the packages *roots* actually resolve to, and their count.

    The workspace has four members, only two of which produce a shipped
    artifact (uuav-client ships the `uuav` cdylib, uuav-server ships the
    uuav-helper executable; uuav-core and uuav-ipc are linked into them as
    rlibs and enter the closure through them). Digesting the whole lock file
    would make a dependency added to a member that ships nothing look
    identical to one added to the core, and the only remedy the lock offers is
    "rebuild both platforms and relock" - a macOS and a Windows build of a
    byte-identical binary. Walking the closure of the shipping roots instead
    means the digest moves exactly when the set of crates compiled into a
    shipped binary moves.

    Raises ValueError when a root is not in the lock, so a renamed package
    fails loudly instead of silently digesting a smaller closure.
    """
    packages = parse_cargo_lock(path)
    by_name = {}
    for (name, version), record in packages.items():
        by_name.setdefault(name, []).append((version, record))

    pending = []
    for root in roots:
        if root not in by_name:
            raise ValueError(
                f"{os.path.basename(path)} has no package named '{root}'; the "
                f"lock's build_inputs roots name a package that no longer exists")
        pending += [record for _version, record in by_name[root]]

    seen = {}
    while pending:
        record = pending.pop()
        key = (record["name"], record["version"])
        if key in seen:
            continue
        seen[key] = record
        for dep in record["deps"]:
            parts = dep.split()
            name = parts[0]
            wanted = parts[1] if len(parts) > 1 else None
            for version, candidate in by_name.get(name, []):
                if wanted is None or version == wanted:
                    pending.append(candidate)

    digest = hashlib.sha256()
    for key in sorted(seen):
        record = seen[key]
        digest.update(
            f"{record['name']} {record['version']} "
            f"{record['checksum'] or 'path'}\n".encode())
    return digest.hexdigest(), len(seen)


_CONFIGURE_DROP_PREFIX = ("--prefix=", "--arch=", "--cc=", "--target-os=")
_CONFIGURE_DROP_EXACT = ("--enable-cross-compile", "--help")


def normalize_configure(tokens) -> str:
    """A configure invocation reduced to its sorted set of meaningful flags.

    Quotes are removed (FFmpeg quotes comma-valued args when it bakes the line
    into a library; the build script does not) and machine/toolchain flags are
    dropped, so a line read from a dylib and the same intent read from the
    build script reduce to the identical string.
    """
    if isinstance(tokens, str):
        tokens = tokens.split()
    flags = set()
    for raw in tokens:
        word = raw.replace("'", "").replace('"', "")
        if not word.startswith("--"):
            continue
        if word in _CONFIGURE_DROP_EXACT or word.startswith(_CONFIGURE_DROP_PREFIX):
            continue
        flags.add(word)
    return " ".join(sorted(flags))


def embedded_configures(path: str) -> list[str]:
    """The distinct normalized FFmpeg configure sets baked into a library.

    Normally one entry: a universal binary carries one configure line per
    architecture slice, but they differ only in the machine/toolchain flags
    normalize_configure drops, so both slices reduce to the same set. Two
    distinct entries mean the slices were genuinely built differently, which
    is a fault the caller reports.
    """
    with open(path, "rb") as handle:
        blob = handle.read()

    found_lines = []
    start = 0
    while True:
        found = blob.find(CONFIGURE_START, start)
        if found == -1:
            return sorted(set(found_lines))
        end = found
        while end < len(blob) and PRINTABLE.match(blob[end:end + 1]):
            end += 1
        candidate = blob[found:end].decode("ascii", "replace")
        if CONFIGURE_MARKER in candidate:
            found_lines.append(normalize_configure(candidate))
        start = end if end > found else found + 1


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


PROBEABLE_COMPONENTS = ("rustc", "cargo", "clang", "gcc", "windows_sdk",
                        "xcode", "sdk")


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
                f"'Explorer/Assets/Plugins/UUAV/**'")
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
# executables by extension, plus extensionless files (the macOS uuav-helper).
# Everything else there (.meta, .sh) is not loaded by the player.
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
    if kind == "cargo-lock-closure":
        return cargo_lock_closure(path, spec["roots"])
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


def strip_shell_comments(line: str) -> str:
    """One line of shell with its comments removed.

    Quote-aware, because a `#` is only a comment when the shell would read it
    as one: `sed -n 's/^#define ...'` style commands carry a literal `#`
    inside single quotes and dropping it would change what the script does
    while leaving the digest unmoved - the one failure mode a normalised digest
    must not have.

    The `` `# ... ` `` idiom - a command substitution whose whole body is a
    comment, so it expands to nothing - is how a builder can comment *inside* a
    backslash-continued configure invocation. It is removed as the no-op it is.
    """
    out: list[str] = []
    quote = None
    index = 0
    while index < len(line):
        char = line[index]
        if quote:
            out.append(char)
            if char == "\\" and quote == '"' and index + 1 < len(line):
                index += 1
                out.append(line[index])
            elif char == quote:
                quote = None
            index += 1
            continue
        if char == "\\" and index + 1 < len(line):
            out.append(char)
            out.append(line[index + 1])
            index += 2
            continue
        if char in "'\"":
            quote = char
            out.append(char)
            index += 1
            continue
        if char == "`":
            close = line.find("`", index + 1)
            if close != -1 and line[index + 1:close].lstrip().startswith("#"):
                index = close + 1
                continue
            out.append(char)
            index += 1
            continue
        if char == "#" and (not out or out[-1].isspace()):
            break
        out.append(char)
        index += 1
    return "".join(out)


def normalize_build_script(path: str) -> str:
    """The FFmpeg build script reduced to what can change the FFmpeg it builds.

    Comments dropped, each line's whitespace runs collapsed to one space, blank
    lines removed - so that a comment edit in the builder is not a false
    positive.

    Line structure is kept - the collapse is per line, not across the file -
    because in shell `a<newline>b` and `a b` are two different programs, and a
    digest that conflated them could be equal for two builders that are not.

    A line left holding only a continuation backslash once its comment is gone
    joins nothing to nothing, so it goes too; otherwise deleting one commented
    line out of a continued invocation would still move the digest.

    Unlike rust_source, which stays a text-exact digest: a source tree digest
    is cheap to recompute at relock time, and normalising Rust the same way
    would buy nothing.
    """
    lines = []
    with open(path, encoding="utf-8") as handle:
        for raw in handle:
            code = " ".join(strip_shell_comments(raw).split())
            if code and code != "\\":
                lines.append(code)
    return "\n".join(lines)


def build_script_sha256(path: str) -> str:
    return hashlib.sha256(normalize_build_script(path).encode()).hexdigest()


def configure_from_script(path: str) -> list[str]:
    """The FFmpeg configure flags the build script invokes.

    Parsed rather than duplicated so configure_expected cannot quietly drift
    away from the script that is the source of truth for it. The universal
    macOS build factors the invocation into a build_arch() function, so both
    the flags on the `configure` line and any *FLAGS=( ... ) array feeding it
    are collected into one sorted set. Per-slice and toolchain-specific flags
    (--prefix, --arch, --cc) and the configure probe's --help are dropped:
    they vary by slice and are normalized out of the line read back from a
    built library too. A stable superset is enough here - any edit to the
    script is caught independently by its builder_sha256.
    """
    with open(path, encoding="utf-8") as handle:
        text = handle.read()

    tokens: list[str] = []

    invocation = r"""(?:\./|["']?\$\{?\w+\}?/)?configure\b((?:[^\n]*\\\n)*[^\n]*)"""
    for match in re.finditer(invocation, text):
        tokens += match.group(1).replace("\\\n", " ").split()

    for match in re.finditer(r"\w*FLAGS=\(([^)]*)\)", text):
        tokens += match.group(1).split()

    normalized = normalize_configure(tokens)
    return [normalized] if normalized else []


def check_ffmpeg_builder(repo: str, target: str, spec: dict, update: bool) -> list[str]:
    """A target built from source must record the build script that produced it."""
    ffmpeg = spec["ffmpeg"]
    if "builder_sha256" not in ffmpeg:
        return []

    path = os.path.join(repo, ffmpeg["builder"])
    if not os.path.exists(path):
        return [f"[{target}] missing FFmpeg build script: {ffmpeg['builder']}"]

    problems = []
    actual = build_script_sha256(path)
    from_script = configure_from_script(path)

    if update:
        ffmpeg["builder_sha256"] = actual
        if from_script:
            ffmpeg["configure_expected"] = from_script
        return []

    if actual != ffmpeg["builder_sha256"]:
        problems.append(
            f"[{target}] the FFmpeg build script changed but FFmpeg was not "
            f"rebuilt and relocked\n"
            f"    {ffmpeg['builder']}\n"
            f"    expected sha256 {ffmpeg['builder_sha256']}\n"
            f"    actual   sha256 {actual}\n"
            f"    (digest is over the script's substance - comments stripped, "
            f"whitespace collapsed - so this is a real recipe change.)")

    if not from_script:
        problems.append(
            f"[{target}] could not parse a ./configure invocation out of "
            f"{ffmpeg['builder']} - the lock's configure_expected can no longer "
            f"be checked against the script")
    elif from_script != ffmpeg["configure_expected"]:
        problems.append(
            f"[{target}] the lock's configure_expected does not match the "
            f"configure line(s) in {ffmpeg['builder']}\n"
            f"    script {from_script}\n"
            f"    lock   {ffmpeg['configure_expected']}")
    return problems


def check_provenance(repo: str, target: str, spec: dict, update: bool) -> list[str]:
    """Every shipped FFmpeg library must report the configure line the lock records."""
    problems = []
    recorded = spec["ffmpeg"].get("configure_observed")
    ffmpeg_artifacts = [a for a in spec["artifacts"] if a["produced_by"] == "ffmpeg"]

    seen = {}
    for artifact in ffmpeg_artifacts:
        path = os.path.join(repo, artifact["path"])
        if os.path.exists(path):
            seen[artifact["path"]] = embedded_configures(path)

    empty = [path for path, value in seen.items() if not value]
    for path in empty:
        problems.append(
            f"[{target}] {path} carries no FFmpeg configure line - "
            f"it is not an FFmpeg build")

    distinct = {tuple(value) for value in seen.values() if value}
    if len(distinct) > 1:
        problems.append(
            f"[{target}] the shipped FFmpeg libraries do not all come from one "
            f"build; {len(distinct)} different configure sets found across "
            f"{len(ffmpeg_artifacts)} libraries")
        return problems

    actual = sorted(next(iter(distinct), ()))
    if update:
        spec["ffmpeg"]["configure_observed"] = actual
        return problems

    if actual and actual != recorded:
        problems.append(
            f"[{target}] all {len(seen) - len(empty)} shipped FFmpeg libraries were "
            f"configured differently than the lock records\n"
            f"    expected {recorded}\n"
            f"    actual   {actual}")
    return problems


def check_drift(target: str, spec: dict) -> list[str]:
    """The build recipe and the shipped build must describe the same FFmpeg."""
    ffmpeg = spec["ffmpeg"]
    expected = ffmpeg.get("configure_expected")
    observed = ffmpeg.get("configure_observed")
    if expected == observed:
        return []

    if ffmpeg.get("drift_acknowledged"):
        print(f"  [{target}] KNOWN DRIFT (acknowledged in the lock): "
              f"{ffmpeg.get('drift_note', '').strip()}")
        return []

    return [f"[{target}] the shipped FFmpeg does not match the build recipe\n"
            f"    recipe   {expected}\n"
            f"    shipped  {observed}\n"
            f"    Rebuild from the recipe, or set drift_acknowledged with a "
            f"drift_note explaining why the difference is intended."]


def report_fresh_build(repo: str, lock: dict, target: str) -> int:
    """Compare a just-built target against the lock and write its SHA256SUMS.

    Used by the build workflow. A fresh build is not expected to be
    byte-identical to what is committed - ad-hoc codesigning and non-stable
    compiler output both defeat that - so a differing artifact hash is
    reported, not failed. What is failed is a fresh build whose FFmpeg does
    not carry the configure line the recipe specifies: that means the build
    did not follow the pinned recipe at all.
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

    sums_path = os.path.join(repo, f"uuav-{target}.sha256")
    with open(sums_path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")

    expected = spec["ffmpeg"].get("configure_expected")
    built = None
    for artifact in spec["artifacts"]:
        if artifact["produced_by"] != "ffmpeg":
            continue
        path = os.path.join(repo, artifact["path"])
        if os.path.exists(path):
            built = embedded_configures(path)
            break

    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as handle:
            handle.write(f"### {target}\n\n")
            handle.write(f"{len(lines)} artifacts, {mismatches} differing from the "
                         f"committed hashes (expected - see "
                         f"Explorer/Assets/Plugins/UUAV/README.md).\n\n")

    if missing:
        fail(f"[{target}] {missing} artifact(s) missing after the build")
        return 1
    if built is not None and expected is not None and built != expected:
        fail(f"[{target}] the freshly built FFmpeg does not carry the configure "
             f"line the recipe specifies\n"
             f"    recipe {expected}\n"
             f"    built  {built}")
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
                             "write uuav-<TARGET>.sha256 (used by the build workflow)")
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

    print(f"UUAV native binary verification ({LOCK_REL})")

    problems = check_build_inputs(repo, lock, args.update)
    for target, spec in lock["targets"].items():
        note(f"{target}: {len(spec['artifacts'])} artifacts, FFmpeg "
             f"{spec['ffmpeg'].get('tag') or spec['ffmpeg'].get('upstream_describe')}, "
             f"core from native/src {spec['rust']['source_digest'][:12]}")
        update_target = args.update and args.only in (None, target)
        if args.update and not update_target:
            note(f"{target}: not relocked (--only {args.only})")
            continue
        problems += check_artifacts(repo, target, spec, update_target)
        problems += check_runtime_dir_completeness(repo, target, spec)
        problems += check_rust_source(repo, lock, target, spec, update_target)
        problems += check_ffmpeg_builder(repo, target, spec, update_target)
        problems += check_provenance(repo, target, spec, update_target)
        if update_target:
            refresh_provenance_notes(repo, lock, spec)
        if not args.update:
            problems += check_drift(target, spec)

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
        print(f"    python3 scripts/uuav/verify-binaries.py --update")
        return 1

    print("All checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
