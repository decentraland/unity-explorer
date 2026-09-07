# KTX native transcoder (Linux)

`libktx_unity.so` is the native transcoder of the KTX for Unity package
(`com.unity.cloud.ktx`, pinned to 3.6.3 in `Explorer/Packages/manifest.json`).
The file committed here is byte-identical to
`Runtime/Plugins/x86_64/libktx_unity.so` inside that published package.

- **Upstream core:** KhronosGroup KTX-Software (Apache-2.0),
  <https://github.com/KhronosGroup/KTX-Software>, version **4.4.0** — the
  version named by the `com.unity.cloud.ktx` changelog for the `ktx-plugin`
  release that produced this Linux binary.
- **Unity wrapper:** `Unity-Technologies/ktx-plugin`, which is **not public**.
  It continues <https://github.com/atteneder/KTX-Software-Unity>, and the
  wrapper's exported surface (21 `ktx_*` functions plus the `ktx_basisu_*` C
  binding) matches `src/ktx_c_binding.cpp` of that predecessor exactly.
- **Not established:** the wrapper revision actually used. Because `ktx-plugin`
  is private, the derivation substitutes the last public release of its
  predecessor, `atteneder/KTX-Software-Unity` v1.1.0, over KTX-Software v4.4.0.
  That combination is a reconstruction, not a verified match.

## Rebuilding

From the repository root:

```sh
nix-build scripts/native-provenance/drv/ktx/default.nix
```

The build flags are derived from this file's export set: read-only libktx (no
encoder, no astcenc), no GL or Vulkan upload, no ETC unpack, core linked
statically into the Unity module.

The rebuild is **not** byte-identical, and its exported dynamic symbol table is
not identical either. It exports nothing this file does not; this file
additionally exports a small set of weak inline instantiations from the Basis
Universal transcoder and libstdc++, plus the `_init` / `_fini` / `__bss_start` /
`_edata` / `_end` markers that a current toolchain no longer places in
`.dynsym`. No `ktx_*` or `ktx_basisu_*` export differs. Treat the rebuild as an
ABI-equivalent replacement, not a reproduction of these bytes.

The recorded verdict is the `ktx` entry in
[`scripts/native-provenance/PROVENANCE.lock`](../../../../scripts/native-provenance/PROVENANCE.lock).
