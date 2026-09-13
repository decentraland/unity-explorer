# ClearScriptV8.linux-x64.so — ClearScript's native V8 library (Linux x64).
#
# Shipped blob: decentraland-explorer_Data/Plugins/x86_64/ClearScriptV8.linux-x64.so
#   sha256 2fe119eaa68fa6cab84124807d1063df2c4ea4567b21e0444af106e9b65b5486 (58846968 bytes)
#
# Provenance chain (verified byte-identical, 2026-09-06):
#   1. Upstream artifact: the OFFICIAL Microsoft NuGet package
#      Microsoft.ClearScript.V8.Native.linux-x64 7.5.1
#      (nupkg sha256 0431a26aaeb791c7171aed72eef0d0b02510b034017e0d387cf20af8108099b8;
#       inner runtimes/linux-x64/native/ClearScriptV8.linux-x64.so
#       sha256 35454aef78e70230950d94e3014bb28c3dbf30376239b934b18339d7475fb41d,
#       same 58846968 bytes). Embedded V8 14.7.173.23-ClearScript; .comment stamps
#      chromium clang/LLD 23.0.0git @8a0be0bc3772 + GCC 10.5.0 (Ubuntu 20.04) —
#      i.e. Microsoft's own V8Update.sh/depot_tools build, not a Decentraland build.
#   2. Deterministic post-processing (in-tree, linux-integrate commit d9d40de11d
#      "feat(linux): Linux native plugin binaries"): clearscript_hide_dynsyms.py
#      marks 7274 .dynsym entries STV_HIDDEN (st_other 0x00 -> 0x02) per
#      clearscript-hidden-symbols.txt — every defined dynamic export owned by the
#      statically-linked libstdc++/libgcc runtime (std::, __gnu_cxx, __cxxabiv1,
#      __cxa_*, operator new/delete, _Unwind_*, ...). Rationale: without this,
#      another Unity plugin pulling in the dynamic libstdc++.so.6 makes
#      ClearScript's std:: references bind across into that copy while its
#      internal runtime state stays in the static copy — two libstdc++ heaps ->
#      `free(): invalid size` inside V8::Initialize. The ClearScript C interop
#      surface (V8Context_*/V8Isolate_*/StdString_*/...) keeps DEFAULT visibility.
#      The transformation only flips st_other visibility bits; no other bytes
#      change (7274 single-byte diffs vs the NuGet blob, verified with cmp -l).
#
# Why no from-source V8 rebuild: the shipped bytes ARE Microsoft's official
# release binary (post-processed). Rebuilding V8 14.7.173.23 + the ClearScriptV8
# wrapper from source (github.com/microsoft/ClearScript v7.5.1, V8Update.sh:
# depot_tools + gn + monolithic v8) with any nix toolchain cannot reproduce
# Microsoft's chromium-clang-23.0.0git@8a0be0bc snapshot output byte-for-byte;
# the strongest provenance statement available is this exact chain to the signed
# upstream NuGet artifact, which reproduces the shipped file bit-for-bit.
#
# Sources for the transformation inputs (vendored beside this file, canonical
# home: rig/unity/linux-runtime/ in the dcl one-foundry repo):
#   clearscript_hide_dynsyms.py       sha256 (see src hash below, pure stdlib python3)
#   clearscript-hidden-symbols.txt    7274 mangled names,
#     sha256 210b5da69fa60d6c012dd80ee757dbfc3526df8c34eeca5223e674230fde9c66
{ pkgs ? import ../../nixpkgs.nix { } }:

let
  shippedSha256 = "2fe119eaa68fa6cab84124807d1063df2c4ea4567b21e0444af106e9b65b5486";

  nupkg = pkgs.fetchurl {
    # Stable v3 flat-container URL for Microsoft.ClearScript.V8.Native.linux-x64 7.5.1
    url = "https://api.nuget.org/v3-flatcontainer/microsoft.clearscript.v8.native.linux-x64/7.5.1/microsoft.clearscript.v8.native.linux-x64.7.5.1.nupkg";
    hash = "sha256-BDGiaq63kccXGu1y7vDQsCUQsDQBfg04fPIK+BCAmbg=";
  };
in
pkgs.stdenv.mkDerivation {
  pname = "clearscript-v8-native-linux-x64";
  version = "7.5.1";

  srcs = [ ];
  dontUnpack = true;

  nativeBuildInputs = [ pkgs.unzip pkgs.python3 ];

  hideScript = ./clearscript_hide_dynsyms.py;
  symbolList = ./clearscript-hidden-symbols.txt;

  buildPhase = ''
    runHook preBuild

    unzip -q ${nupkg} runtimes/linux-x64/native/ClearScriptV8.linux-x64.so
    python3 "$hideScript" \
      runtimes/linux-x64/native/ClearScriptV8.linux-x64.so \
      "$symbolList" \
      ClearScriptV8.linux-x64.so

    runHook postBuild
  '';

  installPhase = ''
    runHook preInstall
    install -Dm644 ClearScriptV8.linux-x64.so $out/lib/ClearScriptV8.linux-x64.so
    runHook postInstall
  '';

  # Byte-identity target: stdenv fixup must not touch the file.
  dontStrip = true;
  dontPatchELF = true;

  doInstallCheck = true;
  installCheckPhase = ''
    echo "${shippedSha256}  $out/lib/ClearScriptV8.linux-x64.so" | sha256sum -c -
  '';

  meta = with pkgs.lib; {
    description = "ClearScript native V8 library (official NuGet 7.5.1 binary with C++-runtime dynsym exports localized)";
    homepage = "https://github.com/microsoft/ClearScript";
    license = licenses.mit; # ClearScript is MIT; V8 is BSD-3-Clause
    platforms = [ "x86_64-linux" ];
  };
}
