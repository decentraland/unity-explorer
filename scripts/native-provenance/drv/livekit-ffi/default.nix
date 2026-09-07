# liblivekit_ffi.so — LiveKit FFI (Rust cdylib) as shipped with the
# Decentraland Linux explorer.
#
# Lineage (probed from the shipped blob):
#   * "initializing ffi server v0.12.48" -> livekit-ffi 0.12.48
#   * upstream tag rust-sdks/livekit-ffi@0.12.48
#     -> commit e7faaea5ce6c70f0b4ea67e2e7db695f4e51237f (livekit/rust-sdks;
#        no decentraland fork of rust-sdks exists)
#   * cargo registry paths in the blob (cxx 1.0.194, bytes 1.11.0,
#     reqwest 0.12.28, log 0.4.29, regex 1.12.2, jiff 0.2.18, ...) all match
#     this tag's Cargo.lock exactly.
#   * shipped blob was built with rustc 1.93.1
#     (/rustc/01f6ddf7588f42ae2d7eb0a2f21d44e8e96674cf). Per project decision
#     byte-identity is NOT a goal here: we build the pinned source with the
#     current nixpkgs rust toolchain and verify the exported ABI instead.
#
# webrtc-sys normally *downloads* a prebuilt libwebrtc archive at build time
# (webrtc-sys/build/src/lib.rs, WEBRTC_TAG = "webrtc-0001d84-2"). We pre-fetch
# that exact archive as a fixed-output derivation and point the build at it
# via the documented LK_CUSTOM_WEBRTC env var, keeping the build pure.
#
# Note: the shipped blob contains NVIDIA NvCodec strings (built on a machine
# with CUDA headers; webrtc-sys/build.rs enables USE_NVIDIA_VIDEO_CODEC iff
# $CUDA_HOME/include/cuda.h exists). We deliberately build without CUDA —
# libcuda/libnvcuvid are dlopened lazy-load implibs either way and the
# exported dynsym surface (livekit_ffi_* + cxxbridge) is unaffected.
{ pkgs ? import ../../nixpkgs.nix { } }:

let
  inherit (pkgs) lib fetchFromGitHub fetchurl rustPlatform;

  version = "0.12.48";
  rev = "e7faaea5ce6c70f0b4ea67e2e7db695f4e51237f"; # tag rust-sdks/livekit-ffi@0.12.48

  src = fetchFromGitHub {
    owner = "livekit";
    repo = "rust-sdks";
    inherit rev;
    # submodules: livekit-protocol/protocol (proto files), yuv-sys/libyuv
    fetchSubmodules = true;
    hash = "sha256-x/sK9uwxk5WJO8S3sDRM/5M4psP1KuSUuXHW2Ieb9ZQ=";
  };

  # Prebuilt libwebrtc exactly as pinned by webrtc-sys-build at this rev
  # (pub const WEBRTC_TAG: &str = "webrtc-0001d84-2").
  webrtcZip = fetchurl {
    url = "https://github.com/livekit/rust-sdks/releases/download/webrtc-0001d84-2/webrtc-linux-x64-release.zip";
    hash = "sha256-akGuXN8n6o/ft+KuPRq9prdNiRe3e+rFxj7isEjij/0=";
  };

  # Unpacked layout expected by webrtc_sys_build::webrtc_dir():
  # <dir>/{webrtc.ninja,desktop_capture.ninja,lib/libwebrtc.a,include/...}
  webrtcPrebuilt = pkgs.runCommand "libwebrtc-prebuilt-webrtc-0001d84-2" {
    nativeBuildInputs = [ pkgs.unzip ];
  } ''
    unzip -q ${webrtcZip} -d unpacked
    mv unpacked/linux-x64-release $out
  '';
in
rustPlatform.buildRustPackage {
  pname = "livekit-ffi";
  inherit version src;

  cargoLock.lockFile = "${src}/Cargo.lock";

  buildAndTestSubdir = "livekit-ffi";

  nativeBuildInputs = [
    pkgs.pkg-config
    pkgs.protobuf # prost-build 0.14 needs an external protoc
    rustPlatform.bindgenHook # yuv-sys generates libyuv bindings via bindgen
  ];

  buildInputs = [
    pkgs.glib # glib-2.0/gobject-2.0/gio-2.0 probed by webrtc-sys build.rs
    pkgs.libva # headers only; libva/libva-drm are dlopened via implibs
  ];

  env = {
    # Documented webrtc-sys override: use our fixed-output prebuilt libwebrtc
    # instead of downloading it during the build.
    LK_CUSTOM_WEBRTC = webrtcPrebuilt;
    PROTOC = lib.getExe pkgs.protobuf;
  };

  # Workspace tests need network / audio devices; out of scope.
  doCheck = false;

  postInstall = ''
    # buildRustPackage installs the cdylib to $out/lib; keep only what we ship.
    test -f $out/lib/liblivekit_ffi.so
  '';

  meta = {
    description = "LiveKit FFI server (liblivekit_ffi.so) as bundled with the Decentraland Linux explorer";
    homepage = "https://github.com/livekit/rust-sdks";
    license = lib.licenses.asl20;
    platforms = [ "x86_64-linux" ];
  };
}
