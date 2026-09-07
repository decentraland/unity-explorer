# Draco Unity native plugins: libdracodec_unity.so + libdracoenc_unity.so
#
# Shipped blobs (Decentraland Linux explorer):
#   Plugins/AnyCPU/libdracodec_unity.so  sha256 4f137c51e9ccf0d9db6ca25d8b0070ccae6e5169f7a93468326c67ada3efbfeb (647120)
#   Plugins/x86_64/libdracoenc_unity.so  sha256 d9670d1ede0bf31bab8928f3c825b0c3ce4ed1b4bcba8fdcedc7dee39a14d64a (864840)
#
# Provenance chain (verified):
#   * Explorer manifest.json pins com.atteneder.draco @ 4.0.2 (OpenUPM).
#     The UPM tarball ships these exact bytes (prior probe:
#     an earlier out-of-tree build record).
#   * github.com/atteneder/DracoUnity is the package source repo; its git-lfs
#     pointer files at tag v4.0.2 carry the exact shipped sha256 oids.
#     Those oids were introduced by commit 718b1169 "Updating binaries"
#     (2021-09-11 22:18 +0200).
#   * The natives are built from the draco fork github.com/atteneder/draco.
#     Matching same-day source commit: 427fc7e631707762c362706c4e72d3c025793e9e
#     ("Merge pull request #3 from atteneder/unity-point-clouds-v2",
#     2021-09-11 21:16 +0200, now on branch legacy/unity-2).
#   * Original CI recipe (.github/workflows/unity.yml at that rev), job
#     linux_legacy on ubuntu-18.04 (GCC 7.5.0 — matches shipped .comment
#     "GCC: (Ubuntu 7.5.0-3ubuntu1~18.04) 7.5.0"):
#       cmake . -G Ninja -B build_linux_64
#         -DCMAKE_BUILD_TYPE=MinSizeRel
#         -DDRACO_UNITY_PLUGIN=ON
#         -DDRACO_GLTF=ON
#         -DDRACO_BACKWARDS_COMPATIBILITY=OFF
#       cmake --build build_linux_64 --target dracodec_unity -j
#       cmake --build build_linux_64 --target dracoenc_unity -j
#
# Policy (2026-09-06): byte-identity is NOT the goal. Clean build from the
# pinned source rev on CURRENT nixpkgs / current gcc; expect verdict
# "divergent" with dynsym_match=true (toolchain noise only).
{ pkgs ? import ../../nixpkgs.nix { } }:

pkgs.stdenv.mkDerivation {
  pname = "draco-unity";
  version = "unity-2021-09-11-427fc7e6";

  src = pkgs.fetchFromGitHub {
    owner = "atteneder";
    repo = "draco";
    rev = "427fc7e631707762c362706c4e72d3c025793e9e";
    sha256 = "1lsznb7p564bb6nlhjqil9y6fcc8drsb45m4p2akvbpspdrsqxjv";
  };

  nativeBuildInputs = [ pkgs.cmake pkgs.ninja ];

  # 2021-era draco relies on transitive <stdint.h> includes that newer
  # libstdc++ (gcc 13+) no longer provides. Toolchain-compat only; no
  # functional change.
  postPatch = ''
    for f in \
      src/draco/io/ply_property_writer.h \
      src/draco/io/ply_reader.h \
      src/draco/io/file_utils.h \
      src/draco/io/ply_property_reader.h \
      src/draco/io/parser_utils.h \
      src/draco/core/hash_utils.h \
      src/draco/core/vector_d.h \
      src/draco/core/decoder_buffer.h \
      src/draco/core/varint_encoding.h \
      src/draco/core/math_utils.h \
      src/draco/core/encoder_buffer.h \
      src/draco/core/data_buffer.h \
      src/draco/core/divide.h \
      src/draco/core/varint_decoding.h \
      src/draco/core/cycle_timer.h \
      src/draco/core/bit_utils.h \
      src/draco/core/macros.h
    do
      sed -i '1i #include <cstdint>' "$f"
    done
  '';

  # CMakeLists at this rev predates CMP0077-style option handling in
  # newer cmake; keep the original flag set from the CI recipe.
  cmakeFlags = [
    "-DCMAKE_BUILD_TYPE=MinSizeRel"
    "-DDRACO_UNITY_PLUGIN=ON"
    "-DDRACO_GLTF=ON"
    "-DDRACO_BACKWARDS_COMPATIBILITY=OFF"
  ];

  # Only the two unity plugin modules are wanted.
  ninjaFlags = [ "dracodec_unity" "dracoenc_unity" ];

  # The shipped blobs are unstripped MODULE libs; keep symtab for comparison.
  dontStrip = true;

  installPhase = ''
    runHook preInstall
    mkdir -p $out/lib
    cp libdracodec_unity.so $out/lib/
    cp libdracoenc_unity.so $out/lib/
    runHook postInstall
  '';

  meta = with pkgs.lib; {
    description = "Draco 3D compression Unity native plugins (decoder + encoder) from the atteneder fork, as shipped in com.atteneder.draco 4.0.2";
    homepage = "https://github.com/atteneder/draco";
    license = licenses.asl20;
    platforms = [ "x86_64-linux" ];
  };
}
