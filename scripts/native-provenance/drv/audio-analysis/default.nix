# audio-analysis.so — Rust cdylib doing audio spectrum analysis (fundsp),
# shipped in decentraland-explorer_Data/Plugins/x86_64/
#
#   audio-analysis.so  sha256 6b2fc43f493dc759267ce0b22c00a1c068fdb4c0a657902a997118c5c5860aa5  (544408 bytes)
#
# SOURCE PROVENANCE (vendored into ./src — see below for why):
#   repo: a local linux-uuav checkout  branch linux/uuav
#   commit 0e3fcab8862e2cd4f46a18555c008f1513d4aa92
#     "uuav: default UUAV media ON on Linux (pending in-Unity validation)"
#   path: native/audio-analysis/  (Cargo.toml + src/{lib.rs,cabi.rs})
#   Vendored (local copy) rather than fetchGit because the checkout is a
#   multi-GB Unity repo and the branch is NOT public — probed
#   raw.githubusercontent.com for decentraland/unity-explorer and
#   eordano/unity-explorer at linux/uuav{,-clean}: all 404. The sha256 asserts
#   below keep the vendored copies honest against the checkout.
#     Cargo.toml  8a10a5b1d5d2b893031706a05bbcb75f844b5a137ecaf6a21ccc744ca21aecc3
#     src/lib.rs  178f47d983a88ac174fa949eca943192e2a209337d1ae8ada47f3cb622b636f3
#     src/cabi.rs 9d3f08288ab5d87de2846e67e8d517994fbcda72f50fec4bf5e52b9afaefe091
#   The crate ships no Cargo.lock (gitignored upstream); ./src/Cargo.lock was
#   generated with cargo 1.97.1 (nixpkgs) on 2026-09-06 — 60 packages, fundsp
#   pinned 0.20.0 matching the blob's embedded registry path
#   index.crates.io-1949cf8c6b5b557f/fundsp-0.20.0. Transitive dep versions may
#   differ from whatever the original (unpublished) lockfile resolved.
#
# GOAL (per user direction): clean build from the in-tree source on CURRENT
# nixpkgs rustPlatform — byte-identity is explicitly NOT a goal. Verification
# bar: identical nm -D export set vs the shipped blob; compare.py "divergent"
# is ACCEPTED when dynsym_match=true and the remaining diffs are toolchain
# noise. Newer dep resolutions are a desired outcome.
#
# BUILD FINGERPRINT (probed from the shipped blob):
#   .comment: "GCC: (GNU) 15.3.0" + "rustc version 1.97.1 (8bab26f4f
#   2026-07-14) (built from a source tarball)" — a nix-built rustc 1.97.1; buildRustPackage ignores rust-toolchain.toml anyway and uses nixpkgs rustc,
#   which is the desired toolchain here.
#   Blob was built by scripts/uuav-style canonical harness under /buildenv
#   (embedded cargo home /buildenv/.cargo/registry); the nix build vendors deps
#   under /build/cargo-vendor-dir instead.
#
# VERDICT RATIONALE (2026-09-06, verdict.lock.json: "divergent", ACCEPTED):
#   dynsym_match=true — single export audio_analysis_analyze_audio_buffer at
#   the same address (0x8820); NEEDED set identical (libgcc_s/libm/libc/ld).
#   normalized_match=false from toolchain noise only:
#     - vendored-path string /buildenv/.cargo/registry/... vs
#       /build/cargo-vendor-dir/... (fft.rs panic path; different lengths
#       shift .rodata/.dynstr by a few bytes)
#     - nix ld emits both .hash and .gnu.hash; shipped has .gnu.hash only
#     - RUNPATH: nix runtime store paths present in built .so; FLAGS BIND_NOW
#       ordering differs
#     - crate metadata disambiguators (StableCrateId suffixes, e.g.
#       Cs8mtiOKjQzJ7 vs Cs5X2p9FDKdQl_14audio_analysis) differ because the -C
#       metadata inputs (workspace path, cargo invocation) differ — renames
#       ~27 internal symtab symbols and nudges .text by ~0x150 bytes.
#   Same rustc 1.97.1, same fundsp 0.20.0, same code otherwise.
#   Blob is NOT stripped (has .symtab) -> dontStrip.
#   NEEDED: libgcc_s.so.1 libm.so.6 libc.so.6 ld-linux-x86-64.so.2 — no extra
#   buildInputs required. Single export: audio_analysis_analyze_audio_buffer.
#   Crate builds libaudio_analysis.so; shipped name is audio-analysis.so —
#   installed under BOTH names ($out/lib/audio-analysis.so is the compare
#   target).
#
# Proper-nix: cargo deps via cargoLock (vendored fixed-output), explicit deps
# only; no buildFHSEnv, no LD_LIBRARY_PATH. x86_64-linux only.

{ pkgs ? import ../../nixpkgs.nix { } }:

let
  inherit (pkgs) rustPlatform;

  src = ./src;

  # Honesty asserts: vendored sources must match the linux-uuav checkout copies.
  assertHash = path: expected:
    let actual = builtins.hashFile "sha256" path;
    in if actual == expected then true
       else throw "vendored ${toString path} hash ${actual} != expected ${expected}";

  srcOk =
    assert assertHash (src + "/Cargo.toml") "8a10a5b1d5d2b893031706a05bbcb75f844b5a137ecaf6a21ccc744ca21aecc3";
    assert assertHash (src + "/src/lib.rs") "178f47d983a88ac174fa949eca943192e2a209337d1ae8ada47f3cb622b636f3";
    assert assertHash (src + "/src/cabi.rs") "9d3f08288ab5d87de2846e67e8d517994fbcda72f50fec4bf5e52b9afaefe091";
    true;

in
assert srcOk;
rustPlatform.buildRustPackage {
  pname = "audio-analysis";
  version = "0.1.0";

  inherit src;

  cargoLock.lockFile = ./src/Cargo.lock;

  # Shipped blob is a release cdylib with symtab intact.
  buildType = "release";
  dontStrip = true;

  # Only the cdylib matters; skip tests (none meaningful for provenance).
  doCheck = false;

  installPhase = ''
    runHook preInstall
    mkdir -p $out/lib
    cp target/x86_64-unknown-linux-gnu/release/libaudio_analysis.so $out/lib/ \
      || cp target/release/libaudio_analysis.so $out/lib/
    ln -s libaudio_analysis.so $out/lib/audio-analysis.so
    runHook postInstall
  '';

  meta = {
    description = "Decentraland explorer native audio analysis plugin (fundsp)";
    platforms = [ "x86_64-linux" ];
  };
}
