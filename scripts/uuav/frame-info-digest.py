#!/usr/bin/env python3
"""The `FrameInfo` freeze guard (layer 3b), checkable on a box that cannot build.

`uuav-adapter/build.rs` digests the frozen core's `FrameInfo` declaration text
and fails the adapter build when it moves. That is the only layer of the freeze
guard that catches a field *reorder* - size, align and every declared type
survive one - so it is the load-bearing one, and it runs only where the adapter
builds: macOS and Windows, with FFmpeg present. On Linux, and in CI, it is
unreachable.

This script is that guard's normalisation and digest, ported verbatim, plus the
two-way agreement `uuav-adapter/src/abi_guard.rs` const-asserts:

  1. the declaration in `native/src/frame_info.rs` digests to X;
  2. `uuav-abi`'s `FRAME_INFO_DECL_SHA256` is X;
  3. `uuav-adapter/build.rs`'s own `EXPECTED` copy is X.

All three must agree. Three copies rather than one because a build script cannot
depend on a path dependency of the crate it builds without also building it for
the host, which a cross-compile does not want - so the digest is duplicated, and
something has to check the duplicates.

This is a *port*, not the guard itself: it can drift from `build.rs`. It cannot
drift silently, because both compare against the same constant, so a divergence
shows up as this script disagreeing with a green macOS adapter build. The real
guard stays `scripts/uuav/abi-freeze-check.sh`, which edits the frozen source and
requires the adapter build to fail; this is the half of it that a Linux runner
can execute.

    python3 scripts/uuav/frame-info-digest.py            # check, exit 1 on drift
    python3 scripts/uuav/frame-info-digest.py --print    # just print the digest

Exit status: 0 all three agree, 1 they do not, 2 something is missing.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import re
import sys

PLUGIN = "Explorer/Assets/Plugins/UUAV"
FRAME_INFO = f"{PLUGIN}/native/src/frame_info.rs"
ABI_LIB = f"{PLUGIN}/native/crates/uuav-abi/src/lib.rs"
ADAPTER_BUILD = f"{PLUGIN}/native/crates/uuav-adapter/build.rs"

IN_ACTIONS = os.environ.get("GITHUB_ACTIONS") == "true"


def fail(message: str) -> None:
    print(f"::error::{message}" if IN_ACTIONS else f"FAIL: {message}")


def normalise(source: str) -> str | None:
    """Steps 1-3 of `uuav-adapter/build.rs`'s `normalise`, line for line.

    None when the block is not there at all, which is itself a freeze violation
    and must fail rather than digest the empty string.
    """
    lines = source.splitlines()

    declaration = None
    for index, line in enumerate(lines):
        if line.lstrip().startswith("pub struct FrameInfo"):
            declaration = index
            break
    if declaration is None:
        return None

    end = None
    for index in range(declaration + 1, len(lines)):
        if lines[index].startswith("}"):
            end = index
            break
    if end is None:
        return None

    run = 0
    for line in lines[:declaration]:
        run = run + 1 if line.lstrip().startswith("#[") else 0
    start = declaration - run
    if start < 0:
        return None

    words = []
    for line in lines[start:end + 1]:
        at = line.find("//")
        code = line if at < 0 else line[:at]
        words += code.split()
    return " ".join(words)


def constant(path: str, pattern: str) -> str | None:
    with open(path, encoding="utf-8") as handle:
        found = re.search(pattern, handle.read())
    return found.group(1) if found else None


def main() -> int:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--print", action="store_true", dest="show",
                        help="print the digest of the current declaration and exit 0")
    parser.add_argument("--repo", default=None,
                        help="repository root (default: two levels above this script)")
    args = parser.parse_args()

    repo = args.repo or os.path.dirname(os.path.dirname(os.path.dirname(
        os.path.abspath(__file__))))

    for relative in (FRAME_INFO, ABI_LIB, ADAPTER_BUILD):
        if not os.path.exists(os.path.join(repo, relative)):
            print(f"FAIL: missing {relative}", file=sys.stderr)
            return 2

    with open(os.path.join(repo, FRAME_INFO), encoding="utf-8") as handle:
        declaration = normalise(handle.read())
    if declaration is None:
        fail(f"no `pub struct FrameInfo {{ ... }}` block in {FRAME_INFO} - the "
             f"declaration the FRAME_INFO_DECL_SHA256 guard digests is gone")
        return 1

    actual = hashlib.sha256(declaration.encode()).hexdigest()
    if args.show:
        print(actual)
        return 0

    in_abi = constant(os.path.join(repo, ABI_LIB),
                      r'FRAME_INFO_DECL_SHA256\s*:\s*&str\s*=\s*"([0-9a-f]{64})"')
    in_build = constant(os.path.join(repo, ADAPTER_BUILD),
                        r'EXPECTED\s*:\s*&str\s*=\s*"([0-9a-f]{64})"')

    problems = []
    if in_abi is None:
        problems.append(f"no FRAME_INFO_DECL_SHA256 literal found in {ABI_LIB}")
    if in_build is None:
        problems.append(f"no EXPECTED literal found in {ADAPTER_BUILD}")
    if in_abi is not None and in_build is not None and in_abi != in_build:
        problems.append(
            f"the two copies of the digest disagree, so the guard is checking "
            f"against a stale expectation\n"
            f"    uuav-abi         {in_abi}\n"
            f"    adapter build.rs {in_build}")
    if in_abi is not None and actual != in_abi:
        problems.append(
            f"the frozen core's FrameInfo declaration changed\n"
            f"    {FRAME_INFO} digests to {actual}\n"
            f"    FRAME_INFO_DECL_SHA256 is  {in_abi}\n"
            f"    native/src is frozen; if the change is intended, update "
            f"FRAME_INFO_DECL_SHA256 in crates/uuav-abi/src/lib.rs, EXPECTED in "
            f"crates/uuav-adapter/build.rs, the golden offsets in "
            f"crates/uuav-abi/src/layout.rs and the FrameInfo declaration in "
            f"crates/uuav-abi/src/lib.rs together.")

    if problems:
        for problem in problems:
            fail(problem)
        return 1

    print(f"FrameInfo declaration digest {actual} - matches uuav-abi and "
          f"uuav-adapter/build.rs.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
