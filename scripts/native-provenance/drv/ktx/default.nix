# libktx_unity.so — shipped in decentraland-explorer_Data/Plugins/x86_64/ (and a
# byte-identical copy in Plugins/AnyCPU/).
#
#   shipped sha256 36962d135310df18d7f4dfceb2aa0754f78cc1368b2cd57dc7e3cb4e063cc312 (1738384 bytes)
#
# SOURCE PROVENANCE:
#   The shipped blob is byte-identical to Runtime/Plugins/x86_64/libktx_unity.so of
#   the official Unity package com.unity.cloud.ktx 3.6.3
#   (https://download.packages.unity.com/com.unity.cloud.ktx/-/com.unity.cloud.ktx-3.6.3.tgz)
#   Per that package's CHANGELOG, the Linux binary is "ktx-plugin" release 1.5.2
#   (github.com/Unity-Technologies/ktx-plugin — PRIVATE repo, 404), which per the
#   1.5.0 entry "contains KTX-Software 4.4.0"; 1.5.1/1.5.2 were Linux-only
#   rebuilds for older Ubuntu compatibility (shipped .comment: "GCC: (Ubuntu
#   7.5.0-3ubuntu1~18.04) 7.5.0" — an Ubuntu 18.04 CI toolchain, built outside nix).
#
#   ktx-plugin is the continuation of atteneder/KTX-Software-Unity: the shipped
#   export surface (21 ktx_* wrapper fns + ktx_basisu_* c-binding) matches
#   src/ktx_c_binding.cpp of KTX-Software-Unity exactly (v1.1.0 == main, verified).
#   Since ktx-plugin is private, this derivation pins:
#     wrapper: atteneder/KTX-Software-Unity v1.1.0 (last release, 2023-03-24)
#     core:    KhronosGroup/KTX-Software v4.4.0 (dropped into the wrapper's
#              KTX-Software submodule slot; the wrapper's own submodule points at
#              atteneder's fork, but 1.5.x uses upstream 4.4.0 per the changelog)
#
# FEATURE FLAGS (derived from the shipped export set, see exports.txt in the
# probe dir): read-only libktx (ktx_read: no CompressBasis/CompressAstc/astcenc
# exports), no GL/Vulkan upload (no ktxTexture_GLUpload/VkUpload), no etcdec
# (KTX_FEATURE_ETC_UNPACK=OFF; KtxUnity changelog: "Native binaries don't
# contain etcdec anymore"), static core linked into a MODULE ktx_unity.
#
# TOOLCHAIN POLICY (user requirement): current nixpkgs (26.11pre-git via
# NIX_PATH), current gcc/glibc. Byte-identity to the Ubuntu-18.04-gcc-7.5 blob
# is NOT a goal; the target is a clean build from the pinned source revs with
# an identical exported-symbol surface. Verified via compare.py (dynsym match).
#
# PURITY: fetchFromGitHub with explicit hashes, explicit deps, no buildFHSEnv,
# no LD_LIBRARY_PATH. x86_64-linux only.

{ pkgs ? import ../../nixpkgs.nix { } }:

let
  inherit (pkgs) lib stdenv fetchFromGitHub cmake ninja;

  # Unity wrapper glue (ktx_c_binding.cpp + CMake glue), v1.1.0 (last tag).
  wrapperSrc = fetchFromGitHub {
    owner = "atteneder";
    repo = "KTX-Software-Unity";
    rev = "v1.1.0"; # 55d8d09367598091de5a2a5acfa4883f41ae7776
    sha256 = "02hvvg1bxnb93wx1knkk5v6l3d7fndmvggjam048q1k486lxsr7r";
    # Do NOT fetchSubmodules: the KTX-Software submodule URL is a git@github.com
    # SSH URL (atteneder's fork); we substitute upstream v4.4.0 below instead.
  };

  # KTX-Software core, the version contained in ktx-plugin 1.5.x per the
  # com.unity.cloud.ktx changelog. All external/ deps (basisu, zstd 1.5.5,
  # dfdutils, astc-encoder) are vendored in-tree — no submodules needed
  # (only tests/cts is a submodule, unused here).
  ktxSrc = fetchFromGitHub {
    owner = "KhronosGroup";
    repo = "KTX-Software";
    rev = "v4.4.0"; # beef80159525d9fb7abb8645ea85f4c4f6842e8f
    sha256 = "01g8lgh1md3mwbhd4lyqazm1ayrrrdrjx4a1giy66mjva5sm2dvc";
  };

in stdenv.mkDerivation {
  pname = "libktx_unity";
  version = "1.1.0+ktx-4.4.0";

  src = wrapperSrc;

  nativeBuildInputs = [ cmake ninja pkgs.bash ];

  postUnpack = ''
    # Fill the KTX-Software submodule slot with upstream v4.4.0.
    rmdir "$sourceRoot/KTX-Software" 2>/dev/null || rm -rf "$sourceRoot/KTX-Software"
    cp -r ${ktxSrc} "$sourceRoot/KTX-Software"
    chmod -R u+w "$sourceRoot/KTX-Software"
  '';

  postPatch = ''
    patchShebangs KTX-Software
  '';

  cmakeBuildType = "Release";

  cmakeFlags = [
    "-DBUILD_SHARED_LIBS=OFF"          # static ktx_read linked into the MODULE
    "-DKTX_FEATURE_TESTS=OFF"
    "-DKTX_FEATURE_TOOLS=OFF"
    "-DKTX_FEATURE_VK_UPLOAD=OFF"      # shipped blob exports no Vulkan upload
    "-DKTX_FEATURE_GL_UPLOAD=OFF"      # shipped blob exports no GL upload
    "-DKTX_FEATURE_ETC_UNPACK=OFF"     # shipped blob contains no etcdec
    "-DKTX_UNITY_FEATURE_VULKAN=OFF"
    "-DKTX_UNITY_FEATURE_OPENGL=OFF"
  ];

  # Only the ktx_unity MODULE and its deps (ktx_read, obj_basisu_cbind);
  # skips the full `ktx` lib, astcenc and everything else.
  ninjaFlags = [ "ktx_unity" ];

  installPhase = ''
    runHook preInstall
    install -Dm644 libktx_unity.so $out/lib/libktx_unity.so
    runHook postInstall
  '';

  # Keep symbols comparable against the shipped (unstripped-dynsym) blob.
  dontStrip = true;

  meta = {
    description = "Unity native KTX/Basis Universal transcoding plugin (KTX-Software-Unity wrapper + KTX-Software 4.4.0), source-pinned rebuild of the Decentraland Linux explorer blob";
    homepage = "https://github.com/atteneder/KTX-Software-Unity";
    license = lib.licenses.asl20;
    platforms = [ "x86_64-linux" ];
  };
}
