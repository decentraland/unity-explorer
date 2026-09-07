# UUAV native plugins: libuuav.so (uuav-client cdylib), libuuav_core.so
# (uuav-core cdylib), uuav-helper (uuav-server bin).
#
# Source of record: this repository, Explorer/Assets/Plugins/UUAV/native.
#   uuav-core has no separate upstream repository, so the Rust workspace is
#   versioned here and imported with builtins.path + filter (native/ subtree
#   only; .target/.third_party/.ffmpeg-* build litter excluded). The key-file
#   sha256 assertions below cover Cargo.toml, Cargo.lock, .cargo/config.toml
#   and rust-toolchain.toml; the .rs sources are pinned by the git commit.
#
# Toolchain: rust-toolchain.toml pins 1.97.1; current nixpkgs (26.11pre-git)
# rustc IS 1.97.1 (8bab26f4f 2026-07-14, "built from a source tarball") - the
# same stamp found in the shipped blobs' .comment. rust-toolchain.toml is
# ignored by nixpkgs cargo (no rustup); no bypass needed.
#
# FFmpeg: upstream links a bespoke FFmpeg n8.1 (scripts/build-ffmpeg-linux.sh,
# LGPL shared, static OpenSSL/libxml2). Per the revised goal this derivation
# links nixpkgs ffmpeg_8 (8.1.2) instead - ABI-compatible soname family
# (libavcodec.so.62 / libavformat.so.62 / libavutil.so.60 / libswresample.so.6
# = the shipped NEEDED set). Byte-identity is not a goal; dynsym parity is.
#
# The in-tree .cargo/config.toml sets FFMPEG_DIR=.third_party/ffmpeg and
# target-dir=.target; both are stripped in postPatch (ffmpeg-sys-next falls
# back to pkg-config, and the nixpkgs cargo hooks expect target/). The linux
# rustflags (-Wl,-rpath,$ORIGIN, --build-id=none) are kept - $ORIGIN matches
# the shipped RUNPATH head and build-id=none matches the shipped blobs
# (upstream disables it for their own reproducibility gates).
{ pkgs ? import ../../nixpkgs.nix { } }:

let
  inherit (pkgs) lib rustPlatform pkg-config patchelf ffmpeg_8;

  checkout = ../../../..;  # repo root: the UUAV native source lives in this repository
  nativeRel = "Explorer/Assets/Plugins/UUAV/native";

  # sha256 pins for the audited source state (raw file hashes; upstream's own
  # uuav-binaries.lock.json pins the same files, but its Cargo.lock entry is a
  # closure-narrowed digest, hence the different value there). Evaluation
  # fails if the checkout drifts.
  keyFileHashes = {
    "Cargo.toml" = "8892b44f30ca061ff36a825ed4ab89f18ecef7398875e0fe741c68be8eef53dc";
    "Cargo.lock" = "f8061b08cddfa5cc4f63086ffd48e1c868ad7239523740f62cc7c4eea350a43f";
    ".cargo/config.toml" = "67b77d9cc0130d2f5fccf0163260b919c70f064ef765dbe93e1c56ab793a44e6";
    "rust-toolchain.toml" = "2d9f13db1ad0cda305e163d91c9484eaf22ab2d9004f0b64138bd65993108f10";
  };
  assertHashes = lib.all
    (rel:
      let got = builtins.hashFile "sha256" (checkout + "/${nativeRel}/${rel}");
      in got == keyFileHashes.${rel}
        || throw "drv/uuav: ${nativeRel}/${rel} hash drifted: ${got}")
    (builtins.attrNames keyFileHashes);

  src = assert assertHashes; builtins.path {
    path = checkout + "/${nativeRel}";
    name = "uuav-native-src";
    filter = path: type:
      let base = baseNameOf path;
      in base != ".target" && base != ".third_party"
         && base != ".ffmpeg-src" && base != ".ffmpeg-build";
  };
in
rustPlatform.buildRustPackage {
  pname = "uuav";
  version = "0.4.0-0e3fcab";
  inherit src;

  cargoLock.lockFile = "${src}/Cargo.lock";

  nativeBuildInputs = [ pkg-config patchelf rustPlatform.bindgenHook ];
  buildInputs = [ ffmpeg_8 ];

  postPatch = ''
    # Drop FFMPEG_DIR (vendored prefix absent; use pkg-config -> nixpkgs
    # ffmpeg_8) and target-dir=.target (nixpkgs cargo hooks expect target/).
    # Keep only the x86_64-linux rustflags.
    cat > .cargo/config.toml <<'EOF'
    [target.x86_64-unknown-linux-gnu]
    rustflags = [
        "-C", "link-arg=-Wl,-rpath,$ORIGIN",
        "-C", "link-arg=-Wl,--build-id=none",
    ]
    EOF
  '';

  cargoBuildFlags = [ "--workspace" ];

  # examples/smoke wants a live GPU/helper; not part of the shipped surface
  doCheck = false;

  # Upstream's release profile sets strip="symbols", but the nixpkgs cargo
  # build hook force-exports CARGO_PROFILE_RELEASE_STRIP=false and defers to
  # stdenv's fixup strip. stripAll (-s) here reproduces upstream's policy:
  # drop .symtab/.strtab/debug, keep the allocated dynsym.
  stripAllList = [ "bin" "lib" ];

  postInstall = ''
    # cargoInstallHook covers bin/uuav-helper and lib/*.so; sanity-assert the
    # full shipped artifact set is present
    for f in $out/lib/libuuav.so $out/lib/libuuav_core.so $out/bin/uuav-helper; do
      [ -e "$f" ] || { echo "missing expected artifact $f" >&2; exit 1; }
    done

    # The helper ships beside the player on arbitrary distros: it must use the
    # standard loader, not this build's nix store glibc, and resolve its
    # bundled ffmpeg libraries from its own directory.
    patchelf --set-interpreter /lib64/ld-linux-x86-64.so.2 --set-rpath '$ORIGIN' \
      $out/bin/uuav-helper
    interp=$(patchelf --print-interpreter $out/bin/uuav-helper)
    [ "$interp" = /lib64/ld-linux-x86-64.so.2 ] || { echo "helper interpreter not portable: $interp" >&2; exit 1; }
  '';

  meta = {
    description = "Decentraland UUAV native video plugins (libuuav, libuuav_core, uuav-helper)";
    platforms = [ "x86_64-linux" ];
  };
}
