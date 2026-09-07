# Native binary provenance

The Linux player ships prebuilt native plugins under
`Explorer/Assets/Plugins/`. This directory holds a Nix derivation per binary
that rebuilds it from pinned sources, plus a comparison tool and a lock file
recording how each rebuild relates to the committed artifact.

## Layout

- `nixpkgs.nix` — the single pinned nixpkgs every derivation defaults to, a
  hash-locked `builtins.fetchTarball` of one revision, imported with
  `config = {}` and `overlays = []` so no ambient channel, `NIXPKGS_CONFIG` or
  user overlay can reach a rebuild. Pass an explicit `pkgs` to override it.
- `drv/<name>/default.nix` — one derivation per binary family. Every source is
  a hash-pinned fetcher, or, where no upstream repository exists, a subtree of
  this repository imported with `builtins.path`.
- `drv/<name>/NOTES.md` — why a rebuild diverges from the committed binary,
  where it does.
- `scripts/compare.py` — normalises two ELF files with `objcopy`, diffs their
  dynamic symbol tables, and writes a verdict.
- `scripts/merge_locks.py` — merges the per-derivation verdicts into
  `PROVENANCE.lock`.

## Rebuilding

```sh
nix-build scripts/native-provenance/drv/<name>/default.nix -o result-<name>
python3 scripts/native-provenance/scripts/compare.py \
    --built result-<name>/lib/<file> \
    --shipped Explorer/Assets/Plugins/<path>/<file>
python3 scripts/native-provenance/scripts/merge_locks.py
```

## Verdicts

- `reproduced` — the rebuild is byte-identical to the committed binary.
- `divergent` with `dynsym_match: true` — same sources and same exported ABI;
  the bytes differ only through toolchain details (build paths, codegen
  version, symbol ordering). This is the expected result for anything not
  built with a bit-identical toolchain.
- `divergent` with `dynsym_match: false` — needs a `NOTES.md` entry
  explaining the export-table difference before it can be trusted.

Two symbol tables that are both empty compare equal, so a binary that exports
nothing (the helper executable) is graded on its exports like any other. An
absent or unreadable table is a different thing and is reported separately:
`dynsym_count` carries `null` for that side and `dynsym_unreadable` names it,
which keeps "exports nothing" distinct from "could not be read".

## Known limits

Byte-identity is only claimed for `c-shims` and `enet`, and only on
x86_64-linux. Everything else grades `divergent` with `dynsym_match: true` by
design: the vendor builds used toolchains that are not reconstructable here
(`rust-eth` wants rustc 1.95.0, `livekit-ffi` rustc 1.93.1, the FFmpeg set a
non-distro GCC), so the provenance claim is same source tag plus same exported
ABI, not identical bytes.

`enet` deliberately does not use the shared pin. It needs gcc 11.4.0 and
binutils 2.38 to reproduce the shipped PLT, so it keeps its own two older
nixpkgs pins; that is a requirement of byte-identity, not an oversight.

The FFmpeg derivation statically links the OpenSSL and libxml2 sources taken
from the shared pin, which fixes them at 3.6.3 and 2.15.3. The shipped blobs
used 3.5.1 and 2.13.8, so those versions still differ from the vendor build —
now deterministically rather than drifting with a channel.

`audio-analysis` still carries a local `src/Cargo.lock` because the crate
ships none upstream; `rust-audio` carries one too, hash-asserted at
evaluation. Every other Rust derivation now takes its lock from inside the
hash-pinned source tree, so the lock is covered by the source hash.
