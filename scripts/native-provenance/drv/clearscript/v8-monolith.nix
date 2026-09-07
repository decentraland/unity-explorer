# libv8_monolith.a — V8 14.7.173.23 with ClearScript 7.5.1 patches, built on
# the current nixpkgs clang toolchain. Split out of default.nix so wrapper /
# hide / installCheck iterations do NOT recompile V8 (~2200 ninja targets).
#
# Outputs:
#   $out/lib/libv8_monolith.a
#   $out/include/   — the PATCHED v8 include tree (V8Patch injects
#                     include/ClearScript/polyfill.h; we replace it with the
#                     guarded copy). The wrapper MUST compile against this,
#                     never the pristine v8 fetch, or the polyfill guard and
#                     ClearScript's API additions vanish.
#
# nix-toolchain-compat patches (patches/build-nix-compat.diff, on top of
# ClearScript's BuildPatch):
#   * drop -fno-lifetime-dse             (clang 23-only flag, we have clang 22)
#   * drop -fsanitize-ignore-for-ubsan-feature=array-bounds  (clang 23-only)
#   * NIX_V8_HOST_RPATH_FLAGS hook: gn links host tools (torque, mksnapshot)
#     with raw lld, bypassing the nix cc-wrapper -> they need explicit
#     -rpath to gcc-lib/glibc to run during the build.
# Plus polyfill-guarded.h: ClearScript's make_unique_for_overwrite polyfill
# collides with libstdc++ 15; the guarded copy (feature-test macro) replaces
# the injected file (in-place regex guarding corrupted namespace std once —
# never again).
#
# Source graph: V8's DEPS pins as fixed-output fetchgit (16 chromium-mirror
# repos + partition_alloc). Proper-nix: explicit hashes, no FHS, no
# LD_LIBRARY_PATH. x86_64-linux only.

{ pkgs ? import ../../nixpkgs.nix { } }:

let
  inherit (pkgs) lib stdenv fetchgit fetchFromGitHub symlinkJoin gn ninja python3;
  llvm = pkgs.llvmPackages_latest;

  # rusty-v8 pattern: a merged llvm tree gn's clang_base_path can point at.
  clangBasePath = symlinkJoin {
    name = "clearscript-v8-llvm-toolchain";
    paths = [ llvm.clang-unwrapped.lib llvm.clang llvm.llvm llvm.lld ];
    postBuild = ''
      dir="$out/lib/clang/${lib.versions.major llvm.clang.version}/lib/${stdenv.hostPlatform.config}/"
      mkdir -p "$dir"
      ln -s ${llvm.compiler-rt}/lib/linux/libclang_rt.builtins-* \
        "$dir/libclang_rt.builtins${stdenv.hostPlatform.extensions.staticLibrary}"
    '';
  };

  # ClearScript is an input here because V8Patch/BuildPatch/ICUPatch live in it.
  clearscript = fetchFromGitHub {
    owner = "ClearFoundry";
    repo = "ClearScript";
    rev = "7.5.1";
    sha256 = "0a29h5irbwpgrwxmcz5hrr71xqb4i135dfjsqpmx2fskyf5qdb8y";
  };

  cr = "https://chromium.googlesource.com";
  dep = url: rev: hash: fetchgit { inherit url rev hash; };

  # V8 14.7.173.23 + its DEPS pins (from ClearScript's V8Update.sh + V8 DEPS)
  v8src          = dep "${cr}/v8/v8.git" "adb62482a30b49d8e73626aacc79ef57b84ab5bf" "sha256-b3BVQhvQVcvINZnCgMPxma0ZNpJfA2d54aC5GYuRl0s=";
  dBuild         = dep "${cr}/chromium/src/build.git" "97e2bd1131c90d68561f34776219613b9dfc13ba" "sha256-Ee+N5G8HpoUnqJRsncOIgqK1oDtgTR5BFfllN1wVlfA=";
  dBuildtools    = dep "${cr}/chromium/src/buildtools.git" "fef8d6947fa54202e8a00c58d5ac8fd15780bc33" "sha256-5Kfuh9KZMF2jLpKry7q9SI63P2KV6epc1zWh9C3qJkY=";
  dAbseil        = dep "${cr}/chromium/src/third_party/abseil-cpp.git" "526428fa2488f4b10406f697108659bc1619a97b" "sha256-zDFrRjDaHAPtIS8c6nWoZa/D5pS1R1beqcKqLy6cNL0=";
  dIcu           = dep "${cr}/chromium/deps/icu.git" "ee5f27adc28bd3f15b2c293f726d14d2e336cbd5" "sha256-UQWSAekvYc1bTEAEQTPdeB406Uqb0mptpnGRZSaLewo=";
  dGoogletest    = dep "${cr}/external/github.com/google/googletest.git" "4fe3307fb2d9f86d19777c7eb0e4809e9694dde7" "sha256-gJhv3DQQSP5BQ6GmDobq42/Gkx4AbOg/ZS80bM0WpEw=";
  dJinja2        = dep "${cr}/chromium/src/third_party/jinja2.git" "c3027d884967773057bf74b957e3fea87e5df4d7" "sha256-RhNDCE9d9ik/YNF0CSrSoBvpeGi04y3ChIY2c66lJpo=";
  dPartitionAlloc= dep "${cr}/chromium/src/base/allocator/partition_allocator.git" "46efd4d0ef7727ead34d1ee15fb4e10eb1574a98" "sha256-L7nntupkpZyzXv2jhwcA6KtHWZYX7KI/1SDLdHyWK7o=";
  dMarkupsafe    = dep "${cr}/chromium/src/third_party/markupsafe.git" "4256084ae14175d38a3ff7d739dca83ae49ccec6" "sha256-mYsC/xZHpAbP/US2VRAfCYm0JeJ03is38S9s2KuA9PI=";
  dFastFloat     = dep "${cr}/external/github.com/fastfloat/fast_float.git" "cb1d42aaa1e14b09e1452cfdef373d051b8c02a4" "sha256-CG5je117WYyemTe5PTqznDP0bvY5TeXn8Vu1Xh5yUzQ=";
  dDragonbox     = dep "${cr}/external/github.com/jk-jeon/dragonbox.git" "beeeef91cf6fef89a4d4ba5e95d47ca64ccb3a44" "sha256-j6swuGgYGfiFcK3iqd4EKTeU92rZHKTbF5T1fcak/ko=";
  dFp16          = dep "${cr}/external/github.com/Maratyszcza/FP16.git" "3d2de1816307bac63c16a297e8c4dc501b4076df" "sha256-CR7h1d9RFE86l6btk4N8vbQxy0KQDxSMvckbiO87JEg=";
  dHighway       = dep "${cr}/external/github.com/google/highway.git" "84379d1c73de9681b54fbe1c035a23c7bd5d272d" "sha256-HNrlqtAs1vKCoSJ5TASs34XhzjEbLW+ISco1NQON+BI=";
  dSimdutf       = dep "${cr}/chromium/src/third_party/simdutf" "f7356eed293f8208c40b3c1b344a50bd70971983" "sha256-l0VPVhfabaMx0oJphFjA9S1LVtavWFZ4w3btW7avCDY=";
  dCpuFeatures   = dep "${cr}/external/github.com/google/cpu_features.git" "936b9ab5515dead115606559502e3864958f7f6e" "sha256-E8LoVzhe+TAmARWZTSuINlsVhzpUJMxPPCGe/dHZcyA=";
  dFuzztest      = dep "${cr}/chromium/src/third_party/fuzztest.git" "3c8b741ed69e60949a481e3ff86c7933f65cfc2d" "sha256-2+1ZHKLhSJpeWgWLJ6sm3SN2l2P54Q2wR6m3dk22sGo=";
  dZlib          = dep "${cr}/chromium/src/third_party/zlib.git" "b80f1d1e5256ac25f6aea3f31f13d458981cb1f9" "sha256-1WoeIPVnuy89qF0n4NYRS7DQeuHBgWorUM3bAOzOj/4=";

  gccLib = pkgs.gcc.cc.lib;

in stdenv.mkDerivation {
  pname = "v8-monolith-clearscript";
  version = "14.7.173.23+cs-7.5.1";

  dontUnpack = false;
  srcs = [ ]; # tree assembled manually below

  nativeBuildInputs = [ gn ninja python3 llvm.clang llvm.lld pkgs.binutils ];

  # gn/ninja setup hooks hijack configure/build/install phases (gn's hook ran
  # `gn gen` at the build root before our tree assembly -> "Can't find source
  # root"). We drive gn/ninja manually from the correct directory.
  dontUseGnConfigure = true;
  dontUseNinjaBuild = true;
  dontUseNinjaInstall = true;
  dontUseNinjaCheck = true;

  patches = [ ]; # applied manually against subtrees in postPatch

  compatDiff = ./patches/build-nix-compat.diff;
  polyfillGuarded = ./patches/polyfill-guarded.h;

  unpackPhase = ''
    runHook preUnpack
    mkdir v8 && cd v8
    cp -r --no-preserve=mode,ownership ${v8src}/. .
    place() { rm -rf "$2"; mkdir -p "$(dirname "$2")"; cp -r --no-preserve=mode,ownership "$1" "$2"; }
    place ${dBuild}          build
    place ${dBuildtools}     buildtools
    place ${dAbseil}         third_party/abseil-cpp
    place ${dIcu}            third_party/icu
    place ${dGoogletest}     third_party/googletest/src
    place ${dJinja2}         third_party/jinja2
    place ${dPartitionAlloc} third_party/partition_alloc
    place ${dMarkupsafe}     third_party/markupsafe
    place ${dFastFloat}      third_party/fast_float/src
    place ${dDragonbox}      third_party/dragonbox/src
    place ${dFp16}           third_party/fp16/src
    place ${dHighway}        third_party/highway/src
    place ${dSimdutf}        third_party/simdutf
    place ${dCpuFeatures}    third_party/cpu_features/src
    place ${dFuzztest}       third_party/fuzztest
    place ${dZlib}           third_party/zlib

    # gclient artifacts gn expects
    cat > build/config/gclient_args.gni <<'EOF'
# Generated from DEPS (gclient_gn_args)
checkout_src_internal = false
EOF
    mkdir -p third_party/rust-toolchain
    echo 'rustc 1.86.0 (adcb3d3b4 2025-03-31)' > third_party/rust-toolchain/VERSION
    cd ..
    runHook postUnpack
  '';

  postPatch = ''
    cd v8
    # ClearScript's own V8 patches
    patch -p1 --no-backup-if-mismatch < ${clearscript}/V8/V8Patch.txt
    ( cd build && patch -p1 --no-backup-if-mismatch < ${clearscript}/V8/BuildPatch.txt )
    ( cd third_party/icu && patch -p1 --no-backup-if-mismatch < ${clearscript}/V8/ICUPatch.txt )
    # nix toolchain compat (clang 22 flags + host-tool rpath hook)
    ( cd build && patch -p1 --no-backup-if-mismatch < $compatDiff ) || \
    ( cd build && patch -p0 --no-backup-if-mismatch < $compatDiff )

    # libstdc++ 15 already defines make_unique_for_overwrite; V8Patch injects
    # include/ClearScript/polyfill.h and v8's bigint.h uses it. Replace with
    # the pre-guarded copy (verified in the scratch build).
    cp $polyfillGuarded include/ClearScript/polyfill.h
    cd ..
  '';

  configurePhase = ''
    runHook preConfigure
    cd v8
    export NIX_V8_HOST_RPATH_FLAGS="-Wl,-rpath,${gccLib}/lib -Wl,-rpath,${pkgs.glibc}/lib"
    gn gen out/x64/Release --args='
      fatal_linker_warnings=false is_cfi=false is_component_build=false
      is_debug=false target_cpu="x64" use_clang_modules=false
      use_custom_libcxx=false use_thin_lto=false
      v8_embedder_string="-ClearScript" v8_enable_fuzztest=false
      v8_enable_pointer_compression=false
      v8_enable_31bit_smis_on_64bit_arch=false
      v8_enable_temporal_support=false v8_monolithic=true
      v8_use_external_startup_data=false v8_target_cpu="x64"
      use_sysroot=false use_glib=false
      clang_base_path="${clangBasePath}"
      clang_version="${lib.versions.major llvm.clang.version}"
      clang_use_chrome_plugins=false treat_warnings_as_errors=false'
    grep -q "${gccLib}" out/x64/Release/obj/torque.ninja || {
      echo "FATAL: host-tool rpath hook did not land in torque.ninja" >&2; exit 1; }
    cd ..
    runHook postConfigure
  '';

  buildPhase = ''
    runHook preBuild
    # keep the host-tool rpath hook alive if ninja re-runs gn gen mid-build
    export NIX_V8_HOST_RPATH_FLAGS="-Wl,-rpath,${gccLib}/lib -Wl,-rpath,${pkgs.glibc}/lib"
    ( cd v8 && ninja -C out/x64/Release v8_monolith -j $NIX_BUILD_CORES )
    runHook postBuild
  '';

  installPhase = ''
    runHook preInstall
    install -Dm644 v8/out/x64/Release/obj/libv8_monolith.a $out/lib/libv8_monolith.a
    # PATCHED include tree — the wrapper compiles against this, never the
    # pristine v8 fetch (polyfill.h + ClearScript API additions live here).
    mkdir -p $out
    cp -r v8/include $out/include
    runHook postInstall
  '';

  dontStrip = true;
  dontPatchELF = true;
  dontFixup = true; # static archive + headers; nothing to patch

  meta = {
    description = "V8 monolith (14.7.173.23) with ClearScript 7.5.1 patches, nix clang toolchain";
    platforms = [ "x86_64-linux" ];
  };
}
