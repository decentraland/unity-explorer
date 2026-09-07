# librust_eth.so — Decentraland rust-ethereum signing/verification cdylib.
#
# Shipped blob: decentraland-explorer_Data/Plugins/x86_64/librust_eth.so
#   sha256 23f7fab8b1a187ef3d5a7808f946aa396056e2f3092bcfa324538927b037eab3 (609944 bytes)
#   Byte-identical to the upstream release artifact rust-eth-natives-v0.2.3.zip
#   (native/linux/librust_eth.so), i.e. the shipped blob IS the GitHub-Actions
#   release build of decentraland/rust-ethereum tag v0.2.3.
#
# Upstream: https://github.com/decentraland/rust-ethereum
#   tag v0.2.3 = rev dc9994994637d30b0a61e2354eb2bbb8e963fa33
#   CI recipe (.github/workflows/release.yml):
#     cargo build --release --target x86_64-unknown-linux-gnu -p rust_eth
#
# Toolchain note (expected nondeterminism vs shipped):
#   Shipped .comment: "rustc version 1.95.0 (59807616e 2026-04-14)",
#   linker LLD 22.1.2, built on ubuntu-latest (GCC 13.3.0 runtime bits),
#   cargo registry under /home/runner/.cargo. Current nixpkgs ships rustc
#   1.97.1, so a byte-identical rebuild is not achievable without pinning
#   rustc 1.95.0 (not present in this nixpkgs); the rebuild targets
#   functional equivalence (same exports, same crate set per Cargo.lock).
{ pkgs ? import ../../nixpkgs.nix { } }:

pkgs.rustPlatform.buildRustPackage rec {
  pname = "rust_eth";
  version = "0.2.3";

  src = pkgs.fetchFromGitHub {
    owner = "decentraland";
    repo = "rust-ethereum";
    rev = "dc9994994637d30b0a61e2354eb2bbb8e963fa33"; # tag v0.2.3
    sha256 = "sha256-CzLp6T73eCTN8WhW20bZ87CCZAW+Yyy6PeMAVxHwISU=";
  };

  # Vendor exactly what Cargo.lock pins (all crates.io, no git deps).
  cargoLock.lockFile = "${src}/Cargo.lock";

  buildType = "release";
  cargoBuildFlags = [ "-p" "rust_eth" ];

  # Unit tests exercise signing round-trips; keep them as a build-time check.
  cargoTestFlags = [ "-p" "rust_eth" ];

  # buildRustPackage installs cdylibs into $out/lib automatically.

  meta = with pkgs.lib; {
    description = "Rust-backed Ethereum signing/verification FFI library for Decentraland";
    homepage = "https://github.com/decentraland/rust-ethereum";
    license = licenses.asl20;
    platforms = [ "x86_64-linux" ];
  };
}
