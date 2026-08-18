#!/usr/bin/env python3
"""Gate B: did this runner reproduce the binaries committed to Git LFS?

Compares the cargo-produced artifacts a fresh canonical build has just deployed
against the sha256 scripts/uuav/uuav-binaries.lock.json records for them - the
hashes of the committed binaries themselves, which uuav-verify.yml re-asserts
on every pull request.

Byte-identical output needs an identical toolchain, and the two shipped targets
were built on two hosts whose rustc versions can differ from each other.
GitHub-hosted runners carry their own rustc, and their mingw and Xcode/ld64
versions move with the runner image. So the comparison only means anything when
the runner's toolchain is the one that produced the committed binaries, and the
expected identity is pinned in the lock at

    targets.<target>.rust.toolchain

as an object of component name -> exact identity string, matching the lines the
workflow's "Record the toolchain actually used" step writes to
toolchain-<os>.txt. Only the components the lock lists are compared; anything
else the runner records is context, not a pin.

Skipped, with a notice naming what differed and the hashes the fresh build
produced, when:

  - the lock pins no toolchain for this target (nothing to compare against yet)
  - a pinned component differs from what this runner recorded
  - native/src no longer matches the source digest this target's binaries were
    built from, so they could not reproduce on any toolchain; that mismatch is
    uuav-verify.yml's failure to raise, not this one's

Fails only when toolchain and source both match and the bytes do not - the one
case where somebody can actually do something about it.

Exit status: 0 reproduced or skipped, 1 did not reproduce, 2 bad usage.
"""

from __future__ import annotations

import argparse
import importlib.util
import json
import os
import sys

LOCK_REL = "scripts/uuav/uuav-binaries.lock.json"

IN_ACTIONS = os.environ.get("GITHUB_ACTIONS") == "true"


def _load_verify():
    """verify-binaries.py's digest helpers, imported despite the dash."""
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                        "verify-binaries.py")
    spec = importlib.util.spec_from_file_location("uuav_verify", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def notice(message: str) -> None:
    print(f"::notice::{message}" if IN_ACTIONS else f"NOTICE: {message}")


def error(message: str) -> None:
    print(f"::error::{message}" if IN_ACTIONS else f"FAIL: {message}")


def read_toolchain(path: str) -> dict:
    """The `component: identity` lines the workflow recorded."""
    recorded = {}
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            key, sep, value = line.partition(":")
            if sep:
                recorded[key.strip()] = value.strip()
    return recorded


def summarize(lines) -> None:
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary:
        return
    with open(summary, "a", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n\n")


def main() -> int:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("target", help="lock target, e.g. macos-universal")
    parser.add_argument("--toolchain", required=True, metavar="FILE",
                        help="the toolchain-<os>.txt this job recorded")
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
    if args.target not in lock["targets"]:
        print(f"FAIL: unknown target '{args.target}'; the lock defines "
              f"{', '.join(lock['targets'])}", file=sys.stderr)
        return 2
    if not os.path.exists(args.toolchain):
        print(f"FAIL: no recorded toolchain at {args.toolchain}", file=sys.stderr)
        return 2

    verify = _load_verify()
    spec = lock["targets"][args.target]
    cargo = [a for a in spec["artifacts"] if a.get("produced_by") == "cargo"]
    if not cargo:
        error(f"[{args.target}] the lock lists no produced_by=cargo artifact, so "
              f"there is nothing for this gate to reproduce")
        return 1

    fresh = {}
    for artifact in cargo:
        path = os.path.join(repo, artifact["path"])
        name = os.path.basename(artifact["path"])
        if not os.path.exists(path):
            error(f"[{args.target}] the build produced no {artifact['path']}")
            return 1
        fresh[name] = (verify.sha256_of(path), os.path.getsize(path), artifact)

    print(f"Fresh build vs {LOCK_REL} - {args.target}")
    for name, (digest, size, _) in fresh.items():
        print(f"  built    {name}  {digest}  ({size} bytes)")

    recorded = read_toolchain(args.toolchain)
    print("  runner   " + ", ".join(f"{k}={v}" for k, v in recorded.items()))

    hashes = ", ".join(f"{name} {digest}" for name, (digest, _, _) in fresh.items())

    expected = spec["rust"].get("toolchain")
    if not isinstance(expected, dict) or not expected:
        notice(f"[{args.target}] reproduction not checked: the lock pins no "
               f"toolchain for this target, so there is nothing to establish "
               f"that this runner is the host the committed binaries came from. "
               f"Pin targets.{args.target}.rust.toolchain in {LOCK_REL} with the "
               f"identities in {os.path.basename(args.toolchain)}. This runner "
               f"produced: {hashes}")
        summarize([f"### Gate B - reproduction ({args.target})", "",
                   "Skipped: the lock pins no toolchain for this target.", "",
                   "```", *(f"{k}: {v}" for k, v in recorded.items()), "```"])
        return 0

    expected = {key: value for key, value in expected.items() if key != "comment"}

    differing = [
        (component, identity, recorded.get(component, "<not recorded>"))
        for component, identity in expected.items()
        if recorded.get(component) != identity
    ]
    if differing:
        detail = "; ".join(f"{component}: lock pins '{pinned}', this runner has "
                           f"'{actual}'" for component, pinned, actual in differing)
        notice(f"[{args.target}] reproduction not checked: this runner's toolchain "
               f"is not the one that produced the committed binaries. {detail}. "
               f"A rebuild on a different toolchain differs for reasons nobody "
               f"here can act on, so the comparison is skipped rather than "
               f"failed. This runner produced: {hashes}")
        summarize([f"### Gate B - reproduction ({args.target})", "",
                   "Skipped: toolchain mismatch.", "",
                   "| component | lock | this runner |", "|---|---|---|",
                   *(f"| {c} | `{p}` | `{a}` |" for c, p, a in differing), "",
                   "| artifact | fresh sha256 |", "|---|---|",
                   *(f"| `{n}` | `{d}` |" for n, (d, _, _) in fresh.items())])
        return 0

    source = lock["rust_source"]
    actual_digest, files = verify.tree_sha256(
        os.path.join(repo, source["path"]), source["suffix"])
    if actual_digest != spec["rust"]["source_digest"]:
        notice(f"[{args.target}] reproduction not checked: {source['path']} is at "
               f"digest {actual_digest} ({files} files) but this target's binaries "
               f"were built from {spec['rust']['source_digest']}, so they cannot "
               f"reproduce on any toolchain. uuav-verify.yml fails on that. This "
               f"runner produced: {hashes}")
        summarize([f"### Gate B - reproduction ({args.target})", "",
                   f"Skipped: `{source['path']}` has moved on from the digest "
                   f"this target was built at.", ""])
        return 0

    mismatched = []
    for name, (digest, size, artifact) in fresh.items():
        if digest == artifact["sha256"]:
            print(f"  same     {name}")
            continue
        mismatched.append(
            f"{artifact['path']}\n"
            f"    committed sha256 {artifact['sha256']} ({artifact['bytes']} bytes)\n"
            f"    rebuilt   sha256 {digest} ({size} bytes)")

    pinned_table = [f"| {k} | `{v}` |" for k, v in expected.items()]
    if mismatched:
        for problem in mismatched:
            error(f"[{args.target}] the committed binary was not reproduced. This "
                  f"runner matches all {len(expected)} toolchain component(s) the "
                  f"lock pins, and {source['path']} is at the digest this target "
                  f"was built from, so the committed bytes should have come back. "
                  f"{problem}")
        summarize([f"### Gate B - reproduction ({args.target})", "",
                   "**Did not reproduce** on the pinned toolchain.", "",
                   "```", *mismatched, "```", "",
                   "| pinned component | identity |", "|---|---|", *pinned_table])
        return 1

    print(f"\nGate B PASS - {len(fresh)} cargo-produced artifact(s) reproduced the "
          f"committed bytes on the pinned toolchain.")
    summarize([f"### Gate B - reproduction ({args.target})", "",
               f"Reproduced {len(fresh)} cargo-produced artifact(s) byte-for-byte.", "",
               "| pinned component | identity |", "|---|---|", *pinned_table])
    return 0


if __name__ == "__main__":
    sys.exit(main())
