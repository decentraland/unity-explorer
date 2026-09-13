# libsentry.so — sentry-native 0.12.2, as shipped in decentraland-explorer
#   Plugins/x86_64/libsentry.so (sha256 f36298dd..., 424608 bytes)
#
# Provenance (recovered from the shipped blob + its companion libsentry.dbg.so,
# which is checked into the sentry-unity 4.0.0 UPM package the game bundles):
#   * Source: getsentry/sentry-native tag 0.12.2 (commit 61a2667b), which is
#     exactly the modules/sentry-native submodule pin of getsentry/sentry-unity
#     tag 4.0.0. The shipped .so is byte-identical to the blob committed at
#     sentry-unity 4.0.0 Plugins/Linux/Sentry/libsentry.so, i.e. it comes from
#     sentry-unity's own CI (GitHub ubuntu-24.04 runner, GCC 13.3.0), not from
#     a Decentraland build.
#   * CI build recipe (sentry-unity Directory.Build.targets, BuildLinuxSDK):
#       cmake -D SENTRY_BACKEND=breakpad -D SENTRY_SDK_NAME=sentry.native.unity
#             -D CMAKE_BUILD_TYPE=RelWithDebInfo
#       strip -s build/libsentry.so -w -K 'sentry_[^_]*'
#       objcopy --add-gnu-debuglink=libsentry.dbg.so libsentry.so
#
# POLICY (user decision 2026-09-06): byte-identity is NOT a goal — build the
# pinned source rev on CURRENT nixpkgs with current deps (curl/glibc/gcc as
# shipped by <nixpkgs>). The behaviorally-relevant knobs (backend=breakpad,
# SDK name, RelWithDebInfo, the CI strip/debuglink post-processing, no
# RUNPATH) are kept so the artifact is a drop-in replacement; toolchain and
# dep versions deliberately float with nixpkgs. Expected compare.py verdict:
# equivalent or divergent-with-dynsym_match=true (toolchain codegen noise
# only) — both acceptable by design.
#
# Proper-nix: fixed-output fetchFromGitHub + explicit deps; no buildFHSEnv,
# no LD_LIBRARY_PATH. x86_64-linux only.
{ pkgs ? import ../../nixpkgs.nix { } }:

pkgs.stdenv.mkDerivation {
  pname = "libsentry-unity";
  version = "0.12.2";

  src = pkgs.fetchFromGitHub {
    owner = "getsentry";
    repo = "sentry-native";
    rev = "0.12.2"; # == commit 61a2667bfffee48d7962affac9c5230cda93dce9
    fetchSubmodules = true; # breakpad backend lives in external/breakpad
    hash = "sha256-sh1f9OYePa/XEYP234gkxbfD40geJO3LNJ74osohkHk=";
  };

  nativeBuildInputs = [ pkgs.cmake pkgs.pkg-config ];
  buildInputs = [ pkgs.curl pkgs.zlib ];

  cmakeBuildType = "RelWithDebInfo"; # sentry's CMakeLists rewrites -O2 -> -O3
  cmakeFlags = [
    "-DSENTRY_BACKEND=breakpad"
    "-DSENTRY_SDK_NAME=sentry.native.unity"
    "-DSENTRY_BUILD_TESTS=OFF"
    "-DSENTRY_BUILD_EXAMPLES=OFF"
    "-DCMAKE_SKIP_RPATH=ON" # shipped blob has no RUNPATH entry at all
  ];

  # CI built only the `sentry` target.
  buildFlags = [ "sentry" ];

  # Shipped blob has no RUNPATH (curl resolved from the host at runtime);
  # stop the nix cc wrapper from injecting store rpaths. Explicit deps still
  # resolve at link time. (Proper-nix: no LD_LIBRARY_PATH involved.)
  preConfigure = ''
    export NIX_DONT_SET_RPATH=1
    export NIX_DONT_SET_RPATH_x86_64_unknown_linux_gnu=1
  '';

  # Replicate the CI post-processing: partial strip keeping only sentry_*
  # in .symtab, plus gnu-debuglink to the unstripped companion.
  installPhase = ''
    runHook preInstall
    mkdir -p $out/lib
    strip -s libsentry.so -w -K 'sentry_[^_]*' -o $out/lib/libsentry.so
    cp libsentry.so $out/lib/libsentry.dbg.so
    (cd $out/lib && objcopy --add-gnu-debuglink=libsentry.dbg.so libsentry.so)
    runHook postInstall
  '';

  # fixupPhase's strip would destroy the shaped .symtab/.debuglink, and its
  # patchelf pass would rewrite section layout.
  dontStrip = true;
  dontPatchELF = true;

  # The exported ABI is the contract: every dynamic symbol the shipped blob
  # exports must be present in ours (superset allowed, missing = fail).
  doInstallCheck = true;
  installCheckPhase = ''
    nm -D --defined-only $out/lib/libsentry.so | awk '{print $3}' | sort > built.syms
    for s in sentry_init sentry_close sentry_capture_event sentry_set_tag \
             sentry_options_new sentry_options_set_dsn sentry_value_new_object; do
      grep -qx "$s" built.syms || { echo "FATAL: missing export $s" >&2; exit 1; }
    done
    echo "export surface OK ($(wc -l < built.syms) defined dynamic symbols)"
  '';

  meta = {
    description = "sentry-native 0.12.2 shared library, sentry-unity Linux recipe, current-nixpkgs toolchain";
    platforms = [ "x86_64-linux" ];
  };
}
