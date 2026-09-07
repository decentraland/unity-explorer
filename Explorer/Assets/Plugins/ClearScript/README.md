# ClearScript native V8 (Linux)

`ClearScriptV8.linux-x64.so` is the native V8 library for Microsoft ClearScript,
which hosts the JavaScript scene runtime.

- **Upstream:** <https://github.com/microsoft/ClearScript>. ClearScript is MIT;
  the embedded V8 is BSD-3-Clause.
- **Version:** ClearScript 7.5.1, embedded V8 `14.7.173.23-ClearScript`.
- **What this file is:** the official NuGet payload
  `Microsoft.ClearScript.V8.Native.linux-x64` 7.5.1, post-processed so the 7274
  dynamic symbols owned by the statically linked libstdc++/libgcc runtime are
  marked `STV_HIDDEN`. Only visibility bits differ from the NuGet payload; no
  other byte changes. That pass is required: with those symbols exported, a
  second Unity plugin that pulls in the dynamic `libstdc++.so.6` makes
  ClearScript's `std::` references bind into that copy while its runtime state
  stays in the static one — two libstdc++ heaps, and `V8::Initialize` dies on
  `free(): invalid size`. The ClearScript C interop surface (`V8Context_*`,
  `V8Isolate_*`, `StdString_*`, `Memory_*`, …) keeps default visibility.

## Rebuilding

Two derivations make two different claims. Run either from the repository root:

```sh
# Reproduce this exact file: fetch the pinned NuGet package, re-apply the
# visibility pass, assert the sha256.
nix-build scripts/native-provenance/drv/clearscript/binary-repack.nix

# Build the same library from source instead: V8 14.7.173.23 assembled from its
# DEPS-pinned fetches plus ClearScript 7.5.1's V8Patch/BuildPatch/ICUPatch, then
# the ClearScriptV8 wrapper and the same visibility pass.
nix-build scripts/native-provenance/drv/clearscript/default.nix
```

- `binary-repack.nix` produces a file **byte-identical** to the one committed
  here, and fails if it does not.
- `default.nix` does **not**, and its exported dynamic symbol table is not
  identical either — the two builds use different compiler versions, so they
  emit different C++ template instantiations and V8-internal symbols. What is
  guaranteed is the interop contract: all 151 C entry points this file exports
  are present in the from-source build, and the derivation fails if any is
  missing. Treat it as a replacement library, not as a reproduction of these
  bytes.

Verdicts live in
[`scripts/native-provenance/PROVENANCE.lock`](../../../../scripts/native-provenance/PROVENANCE.lock)
under `clearscript` and `clearscript-source`; the reasoning is in
[`drv/clearscript/NOTES.md`](../../../../scripts/native-provenance/drv/clearscript/NOTES.md).
