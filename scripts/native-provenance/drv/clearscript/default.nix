# ClearScriptV8.linux-x64.so — FROM-SOURCE build (V8 + ClearScript 7.5.1)
#
# Shipped blob: Plugins/x86_64/ClearScriptV8.linux-x64.so
#   sha256 2fe119eaa68fa6cab84124807d1063df2c4ea4567b21e0444af106e9b65b5486 (58846968 B)
#
# PROVENANCE of the shipped blob (fully solved, see binary-repack.nix): it is
# Microsoft's official NuGet binary (Microsoft.ClearScript.V8.Native.linux-x64
# 7.5.1) post-processed by the in-tree clearscript_hide_dynsyms.py +
# clearscript-hidden-symbols.txt (7274 libstdc++/libgcc dynsyms -> STV_HIDDEN;
# dual-libstdc++ Unity crash fix). binary-repack.nix reproduces that
# byte-for-byte (lock entry "clearscript").
#
# THIS derivation builds the same thing FROM SOURCE on the current nixpkgs
# toolchain (user request). SPLIT STRUCTURE for iteration efficiency:
#   v8-monolith.nix — the expensive part (V8 14.7.173.23 + ClearScript's
#     V8Patch/BuildPatch/ICUPatch + nix-compat patches -> libv8_monolith.a
#     + the PATCHED include tree). Only changes to V8 inputs rebuild it.
#   this file       — the cheap part (~1 min): ClearScriptV8 wrapper compile
#     + link per Unix/ClearScriptV8/Makefile, DCL hide-dynsyms
#     post-processing, entry-point installCheck.
#
# The wrapper compiles against ${v8}/include (the PATCHED tree: V8Patch
# injects include/ClearScript/polyfill.h and adds ClearScript API surface;
# compiling against the pristine v8 fetch would silently drop both).
#
# Byte-identity with the shipped blob is NOT a goal (clang 22 vs Microsoft's
# chromium-clang 23); the contract is the exported entry-point surface
# (verified in installCheck against names taken from the shipped blob) —
# lock entry "clearscript-source", expected verdict divergent with matching
# entry points.
#
# Proper-nix: every input is a fixed-output fetch with an explicit hash;
# explicit deps; no buildFHSEnv, no LD_LIBRARY_PATH. x86_64-linux only.

{ pkgs ? import ../../nixpkgs.nix { } }:

let
  inherit (pkgs) stdenv fetchFromGitHub python3;
  llvm = pkgs.llvmPackages_latest;

  v8 = import ./v8-monolith.nix { inherit pkgs; };

  clearscript = fetchFromGitHub {
    owner = "ClearFoundry";
    repo = "ClearScript";
    rev = "7.5.1";
    sha256 = "0a29h5irbwpgrwxmcz5hrr71xqb4i135dfjsqpmx2fskyf5qdb8y";
  };

  json = fetchFromGitHub {
    owner = "nlohmann";
    repo = "json";
    rev = "v3.10.4";
    hash = "sha256-fVoplDYv9GBECdcZwxTXj3iMWDL+n16WRbqEYmjnfZ4=";
  };

  gccRoot = pkgs.gcc.cc;

in stdenv.mkDerivation {
  pname = "clearscript-v8-source";
  version = "7.5.1+v8-14.7.173.23";

  dontUnpack = false;
  srcs = [ ]; # assembled below

  nativeBuildInputs = [ python3 llvm.clang llvm.lld pkgs.binutils ];

  hideScript = ./clearscript_hide_dynsyms.py;
  hideList   = ./clearscript-hidden-symbols.txt;

  unpackPhase = ''
    runHook preUnpack
    # ClearScript wrapper sources with repo layout intact — the sources use
    # repo-relative includes (../ClearScript/Exports/VersionSymbols.h).
    mkdir -p cs
    cp -r --no-preserve=mode,ownership ${clearscript}/ClearScriptV8 cs/ClearScriptV8
    mkdir -p cs/ClearScript
    cp -r --no-preserve=mode,ownership ${clearscript}/ClearScript/Exports cs/ClearScript/Exports
    runHook postUnpack
  '';

  buildPhase = ''
    runHook preBuild
    # ---- ClearScriptV8 wrapper (Unix/ClearScriptV8/Makefile, Release) ----
    mkdir -p obj
    CXXFLAGS="-std=c++20 -fvisibility=default -fPIC -fno-rtti \
      -Wno-deprecated-declarations -Wno-ignored-attributes -Wno-vla-cxx-extension \
      -O3 -DNDEBUG -I${v8}/include -I${json}/single_include"
    for s in HighResolutionClock HighResolutionClock.Unix HostObjectHolderImpl \
             HostObjectUtil Mutex RefCount StdString V8Context V8ContextImpl \
             V8Isolate V8IsolateImpl V8ObjectHelpers V8ObjectHolderImpl \
             V8ScriptHolderImpl V8SplitProxyManaged V8SplitProxyNative; do
      echo "CXX $s"
      clang++ $CXXFLAGS -c cs/ClearScriptV8/$s.cpp -o obj/$s.o &
    done
    wait

    # Makefile link line + libatomic.a (clang22/libstdc++15 emits libcall
    # atomics for 16-byte CAS in v8 wasm code; Microsoft's chromium-clang
    # inlines them). Static, so no extra NEEDED. No store rpaths in a plugin
    # that only needs glibc.
    export NIX_DONT_SET_RPATH=1
    export NIX_DONT_SET_RPATH_x86_64_unknown_linux_gnu=1
    clang++ $CXXFLAGS -s -static-libstdc++ -static-libgcc -fuse-ld=lld --shared \
      -Wl,--no-undefined \
      -L${v8}/lib obj/*.o -o ClearScriptV8.linux-x64.so \
      -pthread -lv8_monolith -ldl -lrt ${gccRoot}/lib/libatomic.a

    # ---- DCL post-processing: hide libstdc++/libgcc dynsyms ----
    python3 $hideScript ClearScriptV8.linux-x64.so $hideList hidden.so
    runHook postBuild
  '';

  installPhase = ''
    runHook preInstall
    install -Dm644 hidden.so $out/lib/ClearScriptV8.linux-x64.so
    runHook postInstall
  '';

  dontStrip = true;     # -s already applied per Makefile; keep surface stable
  dontPatchELF = true;  # no store rpaths to shrink; keep layout as linked

  doInstallCheck = true;
  installCheckPhase = ''
    # Contract: ClearScript C-interop entry points the shipped blob exports
    # must be present (the managed side P/Invokes these). Names verified
    # against the shipped blob's nm -D output.
    nm -D --defined-only $out/lib/ClearScriptV8.linux-x64.so | awk '{print $3}' | sort -u > built.syms
    for s in V8SplitProxyManaged_SetMethodTable V8SplitProxyNative_GetVersion \
             V8Context_ExecuteCode V8Context_Compile V8Isolate_Create \
             V8Isolate_CreateContext V8Entity_Release StdString_New \
             Memory_Free V8Value_New; do
      grep -qx "$s" built.syms || { echo "FATAL: missing entry point $s" >&2; exit 1; }
    done
    echo "entry-point surface OK ($(wc -l < built.syms) exported symbols)"
  '';

  passthru = { inherit v8; };

  meta = {
    description = "ClearScriptV8 native library built from source (V8 14.7.173.23 + ClearScript 7.5.1) with DCL dynsym hiding";
    platforms = [ "x86_64-linux" ];
  };
}
