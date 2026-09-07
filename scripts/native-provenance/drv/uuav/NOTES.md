# uuav — verdict notes (2026-09-06)

Goal (revised mid-task): clean build on current nixpkgs (26.11pre-git), NOT
byte-identity. Newer deps desired; `divergent` acceptable when dynsym matches
and residual diffs are toolchain noise.

Built: /nix/store/382ngnrlgvmqrfz81pi680ay12218apv-uuav-0.4.0-0e3fcab
Source: Explorer/Assets/Plugins/UUAV/native in this repository, originally from
HEAD 0e3fcab8862e2cd4f46a18555c008f1513d4aa92 (no public repo/branch for the
native tree; builtins.path with sha256 assertions on Cargo.toml/Cargo.lock/
.cargo/config.toml/rust-toolchain.toml).

Toolchain parity (incidental, not required): nixpkgs rustc IS 1.97.1
(8bab26f4f 2026-07-14) — the exact stamp in the shipped .comment. Shipped
blobs were themselves nix-built (nix-store RUNPATH tails, GCC 15.3.0,
glibc 2.42-67 — same store hashes as current nixpkgs); the only unresolvable
input is the shipped `/nix/store/qrjj...-shell/lib` RUNPATH entry (a dev-shell
path not present on this host) and the `/buildenv/.cargo/registry` vendor
prefix (rustup/cargo-registry layout vs nix cargo-vendor-dir).

FFmpeg: nixpkgs ffmpeg_8 (8.1.2) instead of upstream's bespoke n8.1
(scripts/build-ffmpeg-linux.sh). NEEDED sets match the shipped blobs exactly
(libavcodec.so.62 / libavformat.so.62 / libavutil.so.60 / libswresample.so.6).

Per-artifact verdicts (drv/uuav/verdict.lock.json):

| artifact        | verdict   | dynsym                | residual diff |
|-----------------|-----------|-----------------------|---------------|
| uuav-libuuav    | divergent | MATCH                 | toolchain noise |
| uuav-libuuav-core | divergent | MATCH               | toolchain noise |
| uuav-helper     | divergent | MATCH (see note)      | toolchain noise |

uuav-helper `dynsym_match: false` in the lock is a compare.py artifact: BOTH
binaries export zero dynamic symbols (`nm -D --defined-only` empty on each;
full `readelf --dyn-syms` tables diff clean), and compare.py's
`bool(ss and sb and ss == sb)` is False for two empty lists. Manually
verified: dynamic symbol surfaces identical for all three artifacts, and the
full export list diff (name+type) is byte-identical for both .so files.

Residual diff characterization (why not `equivalent`):
- embedded panic paths: /buildenv/.cargo/registry/... vs
  /build/cargo-vendor-dir/... (string lengths shift .rodata/.dynstr)
- RUNPATH: shipped has an extra dev-shell store entry; built has the
  ffmpeg-8.1.2-lib entry instead
- .text within 0.12% (663649→663729 / 518077→516877 / 751375→750943):
  codegen drift from ffmpeg-sys-next bindgen against 8.1.2 vs 8.1 headers
  plus the path-length differences above
- glibc/GCC symbol version requirements identical (readelf -V diff clean)

Stripping: upstream release profile sets strip="symbols", but the nixpkgs
cargo hook force-sets CARGO_PROFILE_RELEASE_STRIP=false; stripAllList
restores the policy (sizes land within 0.15% of shipped).
