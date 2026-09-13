# rust-audio — the microphone-capture cdylib shipped as
#   decentraland-explorer_Data/Plugins/x86_64/librust_audio.so
#   shipped sha256 a5886ad89c19551e8c8697f4ec66fd3992ee8df19bac2c78435885f64f66c55d (864384 bytes)
#
# SOURCE PROVENANCE (vendored into ./src — see below for why):
#   Crate "rust-audio" 0.1.0 from the LiveKit unity SDK fork used by the
#   Decentraland Linux client:
#     repo: client-sdk-unity-linux (a fork of the
#     RustAudio/rust-audio crate inside livekit/client-sdk-unity), path
#     RustAudio/rust-audio/, present at commit 4f02bac2c11a10e956b86a77e8ff0625f82c3817
#     ("feat(linux): ship liblivekit_ffi.so + librust_audio.so ...").
#   The same tree is also recoverable from the published unity package;
#   src/lib.rs + Cargo.{toml,lock} are byte-identical between the
#   two. No canonical public URL exists for this exact revision, so the three
#   build-relevant files are vendored here with sha256 assertions (same pattern
#   as drv/c-shims).
#
# LINEAGE FACTS (probed from the shipped blob):
#   - .comment: "rustc version 1.97.1 (8bab26f4f 2026-07-14)" + "GCC: (GNU) 15.3.0"
#   - dep panic paths under /buildenv/.cargo/registry/.../{anyhow-1.0.98,
#     arc-swap-1.7.1, cpal-0.16.0, crossbeam-channel-0.5.15, dashmap-6.1.0,
#     lazy_static-1.5.0, alsa-0.9.1, ...} — exactly the vendored Cargo.lock set.
#   - NEEDED: libasound.so.2 libgcc_s.so.1 libc.so.6 ld-linux-x86-64.so.2
#   - RUNPATH carries nix store paths (built in a nix-shell with alsa-lib).
#   - The previously shipped librust_audio.so
#     differs from the shipped blob in only 216 bytes, all of them registry-path
#     text (/buildenv vs the operator's home) — same source, same lock, same toolchain.
#
# GOAL: clean rebuild from the pinned source on CURRENT nixpkgs with current
# rustc/deps (byte-identity with the shipped blob is explicitly NOT a goal).
# Verification = identical dynamic-symbol exports (nm -D) vs shipped +
# compare.py verdict recorded in verdict.lock.json; "divergent" is acceptable
# when dynsym matches and the remaining diffs are toolchain-only.
#
# Proper-nix: cargo deps vendored purely via rustPlatform.importCargoLock from
# the asserted Cargo.lock (all 95 deps carry crates.io checksums); explicit
# buildInputs; no buildFHSEnv, no LD_LIBRARY_PATH. x86_64-linux only.

{ pkgs ? import ../../nixpkgs.nix { } }:

let
  inherit (pkgs) lib rustPlatform pkg-config alsa-lib;

  srcAssert = path: hash:
    assert builtins.hashFile "sha256" path == hash; path;

  cargoToml = srcAssert ./src/Cargo.toml
    "e0afa245ad4f60631dab3e59458366120860dc6392823b99efcb1b9057d9388d";
  cargoLock = srcAssert ./src/Cargo.lock
    "c42bf095a3b39b256226b3efd9135acd838d2a7114c4abb1ad7c2b393a7ac470";
  libRs = srcAssert ./src/src/lib.rs
    "98c85e26d9e13641c508e38b0cb307e2f004cd304147aa4bc930ba91af9b299d";

  src = pkgs.runCommand "rust-audio-src" {} ''
    mkdir -p $out/src
    cp ${cargoToml} $out/Cargo.toml
    cp ${cargoLock} $out/Cargo.lock
    cp ${libRs}     $out/src/lib.rs
  '';

in
rustPlatform.buildRustPackage {
  pname = "rust-audio";
  version = "0.1.0";

  inherit src;

  cargoLock.lockFile = cargoLock;

  nativeBuildInputs = [ pkg-config ];
  buildInputs = [ alsa-lib ];

  # cdylib only — no tests in the crate, and `cargo test` would try to build
  # a test harness for a cdylib-only target.
  doCheck = false;

  # Keep the artifact inspectable like the shipped (unstripped) blob;
  # stripping wouldn't affect the dynsym verification but this keeps the
  # comparison surface maximal.
  dontStrip = true;

  postInstall = ''
    # buildRustPackage installs cdylibs to $out/lib; assert it happened.
    test -f $out/lib/librust_audio.so
  '';

  meta = with lib; {
    description = "rust-audio: cpal/ALSA microphone capture FFI cdylib from the LiveKit unity SDK (Decentraland Linux client), rebuilt from pinned source";
    platforms = [ "x86_64-linux" ];
    license = licenses.asl20;
  };
}
