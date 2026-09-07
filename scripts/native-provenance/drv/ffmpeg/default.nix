# ffmpeg — the seven FFmpeg shared libraries shipped in
# decentraland-explorer_Data/Plugins/x86_64/:
#
#   libavcodec.so.62    148dc8c4c4c390e896e7ca6f98245e2123b5c4d5861fe3a9b93ea377ddbaadee
#   libavdevice.so.62   3b7ff4a7e1d03cd30c03d2b0236a69c6505ee1cb596382982707836cad6940e7
#   libavfilter.so.11   1f977e85249220015fa3bdbdd0ec5f12e6748187fff1088c20832f77965c06e2
#   libavformat.so.62   1b8f52c4903a8ded71965e5882b6448c9a022117f96e9b809b09d427653a567c
#   libavutil.so.60     b63c45f14b3416a9a8c6d067714322bc3b50aa0ddc075ec82622916a1448d4f0
#   libswresample.so.6  6099e513fb35ab9daca93021e947b911a099c6ca41fc84aa37bea9fc31afb49b
#   libswscale.so.9     dab12cff4369a9f4f804251573094c1ba8fe62ba3b9f0b72ee7953987971946d
#
# SOURCE PROVENANCE (verified, embedded in the blobs themselves):
#   FFmpeg tag n8.1 ("FFmpeg version n8.1" in libavcodec/libavutil strings),
#   built by the vendor script linux-uuav:
#   Explorer/Assets/Plugins/UUAV/native/scripts/build-ffmpeg-linux.sh.
#   The shipped blobs embed the full configure line (strings | grep prefix):
#     --prefix=/buildenv/ffmpeg-build-prefix/.../native/.third_party/ffmpeg
#     --pkg-config-flags=--static --enable-shared --disable-static
#     --disable-programs --disable-doc --disable-devices --disable-alsa
#     --disable-sndio --disable-xlib --disable-libxcb --disable-bzlib
#     --disable-lzma --disable-vdpau --disable-cuda-llvm --disable-cuvid
#     --disable-nvdec --disable-nvenc --disable-opencl --disable-vulkan
#     --disable-amf --enable-vaapi --enable-libdrm --enable-openssl
#     --enable-libxml2
#   which matches the script's configure invocation verbatim (LGPL: no
#   --enable-gpl). OpenSSL 3.5.1 ("OpenSSL 3.5.1 1 Jul 2025" in
#   libavformat) and libxml2 2.13.8 statically linked (script-pinned
#   tarballs, -fPIC, no-shared); libva/libva-drm/libdrm host-provided by
#   bare soname; DT_RUNPATH=$ORIGIN patchelf'd onto every .so after
#   install. Shipped .comment: "GCC: (GNU) 15.3.0" (non-distro gcc).
#
# THIS DERIVATION (deliberately NOT byte-identity — per project decision):
#   Same FFmpeg source tag n8.1 (soname/ABI set .62/.62/.11/.62/.60/.6/.9
#   must match what the client dlopens) and the exact configure-flag set
#   above, built on CURRENT nixpkgs (gcc 15.3.0 stdenv), with the CURRENT
#   nixpkgs openssl/libxml2 SOURCES compiled static+PIC inside this
#   derivation the same way the vendor script builds its pinned tarballs
#   (openssl: no-shared -fPIC; libxml2: --disable-shared CFLAGS=-fPIC
#   --without-python/zlib/lzma). Dep versions therefore track nixpkgs
#   (openssl 3.6.x vs shipped 3.5.1, libxml2 2.15.x vs shipped 2.13.8),
#   so byte/`equivalent` verdicts are out of reach by design; the
#   provenance claim is: same source tag, same feature surface (configure
#   flags), same NEEDED policy, same $ORIGIN runpath, matching exported
#   dynamic symbol sets.
#
# PURITY: all three sources are fixed-output fetchers with explicit
# hashes (openssl/libxml2 reuse nixpkgs' own hash-locked .src); explicit
# deps; no buildFHSEnv, no LD_LIBRARY_PATH. x86_64-linux only.

{ pkgs ? import ../../nixpkgs.nix { } }:

let
  inherit (pkgs) lib stdenv fetchFromGitHub;

  ffmpegSrc = fetchFromGitHub {
    owner = "FFmpeg";
    repo = "FFmpeg";
    rev = "n8.1";
    sha256 = "17fy3rbi6mq0kdbnakyfzl3c04b8fx4cja6il5a8w4ny5f2a3lhm";
  };

  opensslSrc = pkgs.openssl.src;
  opensslVersion = pkgs.openssl.version;
  libxml2Src = pkgs.libxml2.src;
  libxml2Version = pkgs.libxml2.version;

  # ------------------------------------------------------------------
  # LAYERED DEPS (cached): static openssl/libxml2 as their own drvs so
  # iterating on the ffmpeg layer never rebuilds them (same pattern as
  # drv/clearscript's v8Monolith split).
  # ------------------------------------------------------------------
  opensslStatic = stdenv.mkDerivation {
    pname = "openssl-static-pic";
    version = opensslVersion;
    src = opensslSrc;
    nativeBuildInputs = [ pkgs.perl ];
    configurePhase = ''
      perl ./Configure linux-x86_64 no-shared no-apps no-docs no-tests \
        --prefix=$out --libdir=lib -fPIC
    '';
    enableParallelBuilding = true;
    installPhase = "make install_sw > /dev/null";
    dontFixup = true;
    meta.platforms = [ "x86_64-linux" ];
  };

  libxml2Static = stdenv.mkDerivation {
    pname = "libxml2-static-pic";
    version = libxml2Version;
    src = libxml2Src;
    nativeBuildInputs = [ pkgs.cmake ];
    # minimal: the DASH demuxer only parses UTF-8 XML
    cmakeFlags = [
      "-DCMAKE_INSTALL_LIBDIR=lib"
      "-DCMAKE_POSITION_INDEPENDENT_CODE=ON"
      "-DBUILD_SHARED_LIBS=OFF"
      "-DLIBXML2_WITH_PYTHON=OFF"
      "-DLIBXML2_WITH_ZLIB=OFF"
      "-DLIBXML2_WITH_LZMA=OFF"
      "-DLIBXML2_WITH_ICU=OFF"
      "-DLIBXML2_WITH_TESTS=OFF"
      "-DLIBXML2_WITH_PROGRAMS=OFF"
    ];
    # Both include roots on purpose (same as the vendor script): the DASH
    # demuxer includes <libxml/parser.h> while ffmpeg's configure probe
    # compiles <libxml2/libxml/xmlversion.h>.
    # Also: cmake bakes an ABSOLUTE includedir into a template that already
    # prepends ''${prefix}, yielding ''${prefix}//nix/store/.../include —
    # a nonexistent doubled path that makes ffmpeg's configure probe fail.
    postInstall = ''
      sed -i 's|^includedir=.*|includedir=''${prefix}/include|' $out/lib/pkgconfig/libxml-2.0.pc
      sed -i 's|^Cflags: .*|& -I''${includedir}|' $out/lib/pkgconfig/libxml-2.0.pc
    '';
    dontFixup = true;
    meta.platforms = [ "x86_64-linux" ];
  };

in stdenv.mkDerivation {
  pname = "ffmpeg-dcl-linux";
  version = "n8.1";

  src = ffmpegSrc;

  nativeBuildInputs = with pkgs; [ pkg-config nasm perl python3 patchelf ];
  # libva/libdrm dev files: --enable-vaapi/--enable-libdrm resolve them via
  # pkg-config; at runtime they stay host-provided bare-soname NEEDED entries.
  buildInputs = with pkgs; [ libva libdrm ];

  # mirrors the vendor script's pin for reproducible generated timestamps
  SOURCE_DATE_EPOCH = "1704067200";

  postPatch = ''
    # git-describe of tag n8.1 (what the vendor's shallow clone produced)
    # instead of the RELEASE-file fallback "8.1"
    echo n8.1 > VERSION
  '';

  configurePhase = ''
    runHook preConfigure

    export PKG_CONFIG_PATH="${opensslStatic}/lib/pkgconfig:${libxml2Static}/lib/pkgconfig''${PKG_CONFIG_PATH:+:$PKG_CONFIG_PATH}"

    # ---- FFmpeg: the vendor script's configure line, verbatim except
    # --prefix (recorded in the shipped blobs' configuration string) ----
    ./configure \
      --prefix=$out \
      --pkg-config-flags="--static" \
      --enable-shared \
      --disable-static \
      --disable-programs \
      --disable-doc \
      --disable-devices \
      --disable-alsa \
      --disable-sndio \
      --disable-xlib \
      --disable-libxcb \
      --disable-bzlib \
      --disable-lzma \
      --disable-vdpau \
      --disable-cuda-llvm \
      --disable-cuvid \
      --disable-nvdec \
      --disable-nvenc \
      --disable-opencl \
      --disable-vulkan \
      --disable-amf \
      --enable-vaapi \
      --enable-libdrm \
      --enable-openssl \
      --enable-libxml2

    runHook postConfigure
  '';

  enableParallelBuilding = true;

  # $ORIGIN runpath on every real .so, exactly like the vendor script's
  # post-install patchelf pass (replaces the nix ld-wrapper store rpaths:
  # the FFmpeg set resolves siblings from the plugin folder and host
  # libva/libdrm from the default search path). dontPatchELF keeps the
  # stdenv rpath-shrinker from second-guessing it.
  dontPatchELF = true;
  postFixup = ''
    find $out/lib -name 'lib*.so*' -type f | while IFS= read -r lib; do
      patchelf --set-rpath '$ORIGIN' "$lib"
    done
  '';

  doInstallCheck = true;
  installCheckPhase = ''
    # all seven sonames the client dlopens must exist
    for so in libavcodec.so.62 libavdevice.so.62 libavfilter.so.11 \
              libavformat.so.62 libavutil.so.60 libswresample.so.6 \
              libswscale.so.9; do
      [ -e "$out/lib/$so" ] || { echo "FATAL: missing $so" >&2; exit 1; }
    done

    # vendor script's regression gate: shipped libraries may depend only on
    # each other and on host-universal libraries
    allowed='^(libavcodec|libavdevice|libavfilter|libavformat|libavutil|libswresample|libswscale|libva|libva-drm|libc|libm|libpthread|libdl|librt|libgcc_s|libz|libdrm|ld-linux-x86-64)\.so'
    leaked=0
    for lib in $(find $out/lib -name 'lib*.so*' -type f); do
      [ "$(patchelf --print-rpath "$lib")" = '$ORIGIN' ] \
        || { echo "FATAL: $lib runpath != \$ORIGIN" >&2; exit 1; }
      for needed in $(patchelf --print-needed "$lib"); do
        echo "$needed" | grep -qE "$allowed" \
          || { echo "FATAL: $(basename $lib) links non-portable: $needed" >&2; leaked=1; }
      done
    done
    [ "$leaked" -eq 0 ]

    # --enable-vaapi must have actually resolved the host libva
    patchelf --print-needed $out/lib/libavutil.so.60 | grep -qx 'libva.so.2' \
      || { echo "FATAL: libavutil has no libva.so.2 NEEDED" >&2; exit 1; }
    echo "installCheck OK: 7 sonames, \$ORIGIN runpaths, NEEDED gate, vaapi"
  '';

  meta = {
    description = "FFmpeg n8.1 (LGPL, shared, static openssl/libxml2) — rebuild of the 7 Decentraland Linux explorer plugin libraries";
    homepage = "https://ffmpeg.org";
    license = lib.licenses.lgpl21Plus;
    platforms = [ "x86_64-linux" ];
    notes = "openssl ${opensslVersion} / libxml2 ${libxml2Version} from the pinned nixpkgs (shipped: 3.5.1 / 2.13.8)";
  };
}
