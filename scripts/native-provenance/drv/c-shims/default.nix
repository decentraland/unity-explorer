# c-shims — the two tiny C plugin shims shipped in
#   decentraland-explorer_Data/Plugins/x86_64/
#
#   libDCLProcesses.so            sha256 c6552d906d69342235da8fe6d33166d391092b23907100bdf595bb7b59963cfe
#   libWindowResizeConstraint.so  sha256 f120b374c237f21335c41ebb28933b508b9ce68ce9e11efd30d73c8972c8cb10
#
# DESIGN CHOICE: one derivation producing both .so files. They were shipped as
# a pair from the same dev machine/nixpkgs snapshot, share the verification
# story, and are each a single-TU compile — splitting them would only duplicate
# boilerplate.
#
# SOURCE PROVENANCE (vendored into ./src — see below for why):
#   repo: ../../../..  branch integrate/dev-linux-main
#   - WindowResizeConstraint.c : identical at HEAD 8c09a8708c4c080e0f3ee4d279154b323172172c
#     (= the blob-introducing commit d9d40de11d).
#     sha256 a5acf274cad20eb784be116411f769e9b4f164c790687d4f49f4421b15c66fe7
#   - dcl_processes.c/.h : from commit bead92bab6ae239099f769af763bc43cb4f5a19d
#     (branch linux/deps; NOT an ancestor of HEAD). The shipped blob predates the
#     in-tree strcspn() newline-trim rewrite — HEAD's source compiles to a
#     different (functionally equivalent) get_process_name body. The bead92bab6
#     variant (strlen + trailing-'\n' check) reproduces the shipped code exactly.
#     .c sha256 95dd382ee1814b528dfd9f903a7d0e3a9915e5ded2cc29109651c67230b4e56f
#     .h sha256 33a47aa9b7b4911542029515ec0dff6c03d0507b52e921b5c517a619d6b8eab3
#     (header identical at bead92bab6 and HEAD)
#   Vendored (local path) rather than fetchGit because builtins.fetchGit would
#   copy the multi-GB Unity repo into the store twice for three tiny files, and
#   the needed dcl_processes.c revision is not reachable from the main branch.
#   The sha256 asserts below keep the vendored copies honest.
#
# BUILD FINGERPRINT (probed from the shipped blobs; verified byte-identical):
#   libDCLProcesses.so:
#     .comment "GCC: (GNU) 15.3.0" + "clang version 21.1.8" -> clang, bfd ld
#     no soname, not stripped, no stack canary / gnu.property
#     RUNPATH glibc-2.42-67 : gcc-15.3.0-lib : gcc-15.3.0-libgcc (clang-wrapper default)
#     => clang -shared -fPIC -O2
#   libWindowResizeConstraint.so:
#     .comment "GCC: (GNU) 15.3.0" -> gcc; __stack_chk_fail in dynsym; xor-reg
#     epilogues (-fzero-call-used-regs=used-gpr); DT_FLAGS BIND_NOW; no soname;
#     not stripped.
#     RUNPATH leads with the DEAD path /nix/store/yawvs2adqdm8wymf50q4jwpnkhp2wscn-shell/lib
#     — the mkShell $out of the original dev nix-shell (GC'd, never existed on
#     this drv's inputs; harmless, replicated verbatim for byte-identity).
#     => gcc -shared -fPIC -O2 -fstack-protector-strong -fzero-call-used-regs=used-gpr
#        -Wl,-z,now -Wl,-rpath,<dead shell path> ... -lX11
#
# REPRODUCIBILITY REQUIREMENT: byte-identity holds only against a nixpkgs
# snapshot providing gcc 15.3.0, llvmPackages_21 clang 21.1.8, glibc 2.42-67
# and libx11 1.8.13 at the exact store paths baked into the shipped RUNPATHs

# Proper-nix: explicit deps only; no buildFHSEnv, no LD_LIBRARY_PATH.
# x86_64-linux only.

{ pkgs ? import ../../nixpkgs.nix { } }:

let
  inherit (pkgs) lib stdenv llvmPackages_21 libx11;

  # Dead mkShell store path baked into the shipped libWindowResizeConstraint.so
  # RUNPATH (provenance artifact of the original nix-shell build).
  deadShellRpath = "/nix/store/yawvs2adqdm8wymf50q4jwpnkhp2wscn-shell/lib";

  srcAssert = name: path: hash:
    assert builtins.hashFile "sha256" path == hash; path;

  dclProcessesC = srcAssert "dcl_processes.c" ./src/dcl_processes.c
    "95dd382ee1814b528dfd9f903a7d0e3a9915e5ded2cc29109651c67230b4e56f";
  dclProcessesH = srcAssert "dcl_processes.h" ./src/dcl_processes.h
    "33a47aa9b7b4911542029515ec0dff6c03d0507b52e921b5c517a619d6b8eab3";
  windowResizeC = srcAssert "WindowResizeConstraint.c" ./src/WindowResizeConstraint.c
    "a5acf274cad20eb784be116411f769e9b4f164c790687d4f49f4421b15c66fe7";

in
stdenv.mkDerivation {
  pname = "dcl-c-shims";
  version = "8c09a8708c";

  dontUnpack = true;

  # gcc comes from stdenv; clang 21.1.8 for libDCLProcesses.
  nativeBuildInputs = [ llvmPackages_21.clang ];
  buildInputs = [ libx11 ]; # propagates xorgproto headers

  # All flags are stated explicitly below so the output does not depend on the
  # stdenv hardening default set; the shipped pair was built in a plain
  # nix-shell where the wrapper defaults applied no canary to the clang build.
  hardeningDisable = [ "all" ];

  # Shipped blobs are unstripped and carry the dead-rpath entry; the fixup
  # phase would destroy byte-identity.
  dontStrip = true;
  dontPatchELF = true;

  # stdenv setup.sh prepends "-rpath $out/lib" to NIX_LDFLAGS; the shipped
  # blobs were linked in a nix-shell where no such self-rpath exists. Disable
  # it so the RUNPATH matches byte-for-byte.
  NIX_NO_SELF_RPATH = 1;

  buildPhase = ''
    runHook preBuild

    # FILE symbols in .symtab record the path exactly as passed to the
    # compiler; the shipped blobs show bare basenames, so compile from cwd.
    cp ${dclProcessesC} dcl_processes.c
    cp ${dclProcessesH} dcl_processes.h
    cp ${windowResizeC} WindowResizeConstraint.c

    clang -shared -fPIC -O2 -o libDCLProcesses.so dcl_processes.c

    gcc -shared -fPIC -O2 \
      -fstack-protector-strong -fzero-call-used-regs=used-gpr \
      -Wl,-z,now \
      -Wl,-rpath,${deadShellRpath} \
      -o libWindowResizeConstraint.so WindowResizeConstraint.c -lX11

    runHook postBuild
  '';

  installPhase = ''
    runHook preInstall
    mkdir -p $out/lib
    cp libDCLProcesses.so libWindowResizeConstraint.so $out/lib/
    runHook postInstall
  '';

  meta = with lib; {
    description = "Decentraland Explorer Linux native C shims (process spawn helper + X11 window resize constraint), rebuilt from source to match the shipped blobs";
    platforms = [ "x86_64-linux" ];
    license = licenses.asl20;
  };
}
