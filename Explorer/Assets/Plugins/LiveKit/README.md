# LiveKit native libraries (Linux)

Native libraries consumed by the LiveKit SDK package at
`Explorer/PackagesLocal/com.decentraland.livekit-sdk` (Apache-2.0; see the
LICENSE and NOTICE files in that package).

## `liblivekit_ffi.so`

The native FFI server of the LiveKit Rust SDK.

- **Upstream:** <https://github.com/livekit/rust-sdks> (Apache-2.0). No
  Decentraland fork of `rust-sdks` exists.
- **Version:** `livekit-ffi` **0.12.48** — the version the library itself
  announces at startup — pinned as tag `rust-sdks/livekit-ffi@0.12.48`, rev
  `e7faaea5ce6c70f0b4ea67e2e7db695f4e51237f`. The cargo registry paths embedded
  in the library (`cxx` 1.0.194, `bytes` 1.11.0, `reqwest` 0.12.28, `log`
  0.4.29, `regex` 1.12.2, `jiff` 0.2.18, …) match that tag's `Cargo.lock`
  exactly. The package's `version.ini` records FFI `v0.7.2`; that entry does not
  describe this Linux library.
- **libwebrtc:** the derivation pre-fetches the exact prebuilt archive
  `webrtc-0001d84-2` that `webrtc-sys` pins at this revision and injects it
  through the documented `LK_CUSTOM_WEBRTC` variable, so the build never
  downloads anything.

Rebuild, from the repository root:

```sh
nix-build scripts/native-provenance/drv/livekit-ffi/default.nix
```

The rebuild is **not** byte-identical. Its exported dynamic symbol table is
identical to this file's — all 383 symbols, `livekit_ffi_initialize` /
`request` / `dispose` / `drop_handle` plus the whole `cxxbridge1$…` set, same
names and types. The byte differences are toolchain and packaging details: a
newer rustc, `NEEDED` entries and a runpath added by the pkg-config probe, and
NVIDIA NvCodec support that this file was built with and the derivation
deliberately builds without (`libcuda`/`libnvcuvid` are lazily dlopened either
way, so the exported ABI is unaffected). Details in
[`drv/livekit-ffi/NOTES.md`](../../../../scripts/native-provenance/drv/livekit-ffi/NOTES.md).

## `librust_audio.so`

cpal/ALSA microphone capture, crate `rust-audio` 0.1.0 (cdylib `rust_audio`).

- **Source:** in this repository, at
  `Explorer/PackagesLocal/com.decentraland.livekit-sdk/RustAudio/rust-audio`.
  The derivation builds a copy vendored beside it, hash-asserted and
  byte-identical to that crate's `Cargo.toml`, `Cargo.lock` and `src/lib.rs`.
  The crate originates in the LiveKit Unity SDK lineage
  (<https://github.com/livekit/client-sdk-unity>, Apache-2.0); no public URL
  pins this exact revision, which is why the sources are vendored and asserted
  rather than fetched.

Rebuild, from the repository root:

```sh
nix-build scripts/native-provenance/drv/rust-audio/default.nix
```

The rebuild is **not** byte-identical but exports the same dynamic symbol
table; the differences are toolchain and vendored-path noise.

Both verdicts are recorded in
[`scripts/native-provenance/PROVENANCE.lock`](../../../../scripts/native-provenance/PROVENANCE.lock)
under `livekit-ffi` and `rust-audio`.
