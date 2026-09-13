# enet — libenet.so shipped in decentraland-explorer_Data/Plugins/x86_64/
#
#   libenet.so  sha256 8269fcb7ec49d2da4d31fa93baa6833a5ac16de604229a2197a80bfb49f6c537
#
# SOURCE PROVENANCE (verified byte-identical chain):
#   The shipped blob == Pulse repo src/DCLPulse/runtimes/linux-x64/native/libenet.so
#   (commit 3ad969f8 "Integrate ENet (from https://github.com/SoftwareGuy/ENet-CSharp)")
#   == the official GitHub release asset libenet-release-linux-x86_64.zip of ALL FOUR
#   v2.4.9pre autobuilds (autobuild-a065496/-7e7833d/-d36acf2/-14c4dc5 ship the same .so).
#   This is the ENet-CSharp lineage (SoftwareGuy fork of nxrighthere/ENet-CSharp),
#   NOT vanilla lsalzman enet: enet_linked_version()=0x20409 (2.4.9), exports
#   enet_crc64 + enet_array_is_zeroed. Pinned rev: 14c4dc5 (v2.4.9pre6, the last;
#   Source/Native/ is identical across all four pre tags).
#
# BUILD FINGERPRINT (probed; the shipped blob was built by the repo's
# .github/workflows/MasterBuild.yaml on ubuntu-latest = Ubuntu 22.04):
#   .comment "GCC: (Ubuntu 11.4.0-1ubuntu1~22.04) 11.4.0"; SONAME libenet.so;
#   only NEEDED libc.so.6; IBT+SHSTK gnu.property; BND-prefixed IBT PLT;
#   RW-segment/RELRO layout of binutils <= 2.38; STT_FILE "crtstuff.c" from
#   Ubuntu's crt objects; CET (endbr64) _init/_fini from Ubuntu's
#   --enable-cet glibc crti.o/crtn.o.
#
# EXACT REPRODUCTION RECIPE (byte-identical, cmp = 0 bytes, build-id included —
# ld's default build-id is a content sha1, so it regenerates itself):
#   compile: gcc 11.4.0 (nixpkgs 23.11 gcc11 — same codegen as Ubuntu 11.4.0)
#            -O3 -DNDEBUG -D_FORTIFY_SOURCE=2 -fstack-protector-strong
#            -fcf-protection -fstack-clash-protection (Ubuntu's default hardening)
#            -fno-ident (the Ubuntu ident string is injected via comment.S instead,
#            because nix gcc would stamp "GCC: (GNU) 11.4.0")
#            -Wa,-mx86-used-note=no (Ubuntu gas default; nix gas emits the
#            x86-used ISA note otherwise)
#   link:    gcc/binutils from nixpkgs 22.05 (ld 2.38) with -Wl,-z,ibtplt —
#            binutils >= 2.39 dropped the BND prefix from the IBT PLT, so a
#            modern ld can never reproduce the shipped PLT bytes.
#            -nostartfiles + local crti.S/crtn.S (glibc's x86_64 crti/crtn with
#            endbr64, as built by Ubuntu's CET-enabled glibc — nix glibc's crti
#            lacks endbr64) + gcc crtbeginS.o/crtendS.o copied to files literally
#            named "crtstuff.c" (passed via -Wl, to bypass gcc source sniffing)
#            so ld synthesizes the same STT_FILE entries as Ubuntu's crt objects.
#            comment.S carries the Ubuntu .comment string and an IBT|SHSTK
#            gnu.property note (ld ANDs properties across ALL inputs — one
#            note-less input and the property vanishes).
#
# PURITY: two fixed builtins.fetchTarball nixpkgs pins + fetchFromGitHub source,
# all hash-locked; explicit deps only; no buildFHSEnv, no LD_LIBRARY_PATH.
# x86_64-linux only. The build asserts the shipped sha256 — toolchain drift
# fails loudly instead of silently degrading to "equivalent".

{ system ? "x86_64-linux" }:

let
  shippedSha256 = "8269fcb7ec49d2da4d31fa93baa6833a5ac16de604229a2197a80bfb49f6c537";

  # gcc 11.4.0 for codegen (matches Ubuntu 22.04's compiler payload)
  nixpkgsCompile = builtins.fetchTarball {
    url = "https://github.com/NixOS/nixpkgs/archive/refs/tags/23.11.tar.gz";
    sha256 = "1ndiv385w1qyb3b18vw13991fzb9wg4cl21wglk89grsfsnra41k";
  };
  # binutils 2.38 for the BND IBT PLT (removed in 2.39+... re-added never)
  nixpkgsLink = builtins.fetchTarball {
    url = "https://github.com/NixOS/nixpkgs/archive/refs/tags/22.05.tar.gz";
    sha256 = "0d643wp3l77hv2pmg2fi7vyxn4rwy0iyr8djcw1h5x72315ck9ik";
  };

  pkgsCompile = import nixpkgsCompile { inherit system; };
  pkgsLink    = import nixpkgsLink    { inherit system; };

  gccCompile = pkgsCompile.gcc11;  # 11.4.0
  gccLink    = pkgsLink.gcc11;     # 11.3.0 wrapper -> binutils 2.38

  src = pkgsLink.fetchFromGitHub {
    owner  = "SoftwareGuy";
    repo   = "ENet-CSharp";
    rev    = "14c4dc5590938af8fe04ecbb2890b1ba47e80ec8"; # v2.4.9pre6
    sha256 = "0xvl4vsnprgv7489py73091c4s3xfahzw7891qz90fy21m9x1h3z";
  };

in pkgsLink.stdenvNoCC.mkDerivation {
  pname = "libenet-csharp";
  version = "2.4.9pre6-14c4dc5";
  inherit src;

  crti = ./crti.S;
  crtn = ./crtn.S;
  comment = ./comment.S;

  nativeBuildInputs = [ pkgsLink.binutils ]; # objcopy, readelf for asserts

  dontConfigure = true;
  # fixupPhase's `strip -S` would perturb the byte-identical output
  dontStrip = true;

  buildPhase = ''
    runHook preBuild
    export NIX_DONT_SET_RPATH_x86_64_unknown_linux_gnu=1

    # --- compile (gcc 11.4.0) ---
    ${gccCompile}/bin/gcc -c -fPIC -O3 -DNDEBUG -fno-ident \
      -fstack-protector-strong -fcf-protection -fstack-clash-protection \
      -D_FORTIFY_SOURCE=2 \
      -Wa,-mx86-used-note=no \
      -DENET_NO_PRAGMA_LINK -DENET_DLL -Denet_EXPORTS \
      -ISource/Native -o enet.o Source/Native/enet.c

    # --- crt shims (assembled with the LINK toolchain's gas) ---
    ${gccLink}/bin/gcc -c -Wa,-mx86-used-note=no -o crti.o    $crti
    ${gccLink}/bin/gcc -c -Wa,-mx86-used-note=no -o crtn.o    $crtn
    ${gccLink}/bin/gcc -c -Wa,-mx86-used-note=no -o comment.o $comment

    # gcc's crtbeginS/crtendS, renamed so ld synthesizes STT_FILE "crtstuff.c"
    # (Ubuntu's carry that .file symbol; nix's carry none). Strip their
    # .comment so only comment.S's Ubuntu string survives.
    GCCLIB=$(dirname $(${gccLink}/bin/gcc -print-file-name=crtbeginS.o))
    mkdir cb ce
    cp $GCCLIB/crtbeginS.o cb/crtstuff.c
    cp $GCCLIB/crtendS.o   ce/crtstuff.c
    chmod u+w cb/crtstuff.c ce/crtstuff.c
    objcopy --remove-section=.comment cb/crtstuff.c
    objcopy --remove-section=.comment ce/crtstuff.c

    # --- link (ld 2.38, BND IBT PLT) ---
    ${gccLink}/bin/gcc -shared -nostartfiles \
      crti.o -Wl,cb/crtstuff.c enet.o -Wl,ce/crtstuff.c crtn.o comment.o \
      -Wl,-soname,libenet.so -Wl,--hash-style=gnu -Wl,--build-id -Wl,-z,ibtplt \
      -o libenet.so

    runHook postBuild
  '';

  installPhase = ''
    runHook preInstall
    install -Dm644 libenet.so $out/lib/libenet.so
    runHook postInstall
  '';

  doInstallCheck = true;
  installCheckPhase = ''
    got=$(sha256sum $out/lib/libenet.so | cut -d' ' -f1)
    if [ "$got" != "${shippedSha256}" ]; then
      echo "FATAL: built libenet.so sha256 $got != shipped ${shippedSha256}" >&2
      echo "(toolchain drift — the pins above no longer reproduce the blob)" >&2
      exit 1
    fi
    echo "byte-identical to shipped blob: $got"
  '';

  meta = {
    description = "ENet-CSharp native library (SoftwareGuy fork, 2.4.9), byte-identical rebuild of the shipped Decentraland Linux explorer blob";
    homepage = "https://github.com/SoftwareGuy/ENet-CSharp";
    license = pkgsLink.lib.licenses.mit;
    platforms = [ "x86_64-linux" ];
  };
}
