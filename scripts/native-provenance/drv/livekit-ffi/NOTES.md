# livekit-ffi verdict notes

Verdict: **divergent — accepted** (per project decision 2026-09-06: byte-identity
is not a goal for this drv; target = clean build of the pinned source rev on
current nixpkgs).

- `dynsym_match: true` — the exported dynamic-symbol table (383 symbols:
  `livekit_ffi_initialize/request/dispose/drop_handle` + all `cxxbridge1$…`)
  is **identical** to the shipped blob, name and type.
- `normalized_match: false` — expected toolchain noise:
  - shipped: rustc 1.93.1 (`/rustc/01f6ddf…`), GitHub-runner glibc toolchain;
    built: current nixpkgs rustc 1.97.1 / gcc 15.3.
  - shipped links glib statically-resolved at runtime without NEEDED entries;
    nix build's pkg-config probe adds NEEDED libglib/gobject/gio-2.0 + nix
    RUNPATH.
  - shipped was built on a host with CUDA headers (NvCodec strings present);
    nix build is without CUDA (NVIDIA codec support off; libcuda/libnvcuvid
    are dlopened lazy-load implibs either way, ABI surface unaffected).
- Source pinning verified: tag `rust-sdks/livekit-ffi@0.12.48`
  (e7faaea5ce6c70f0b4ea67e2e7db695f4e51237f) matches all dependency versions
  embedded in the shipped blob's cargo-registry path strings (cxx 1.0.194,
  bytes 1.11.0, reqwest 0.12.28, log 0.4.29, regex 1.12.2, jiff 0.2.18, …).
  No decentraland fork of rust-sdks exists.
- webrtc archive pinned as fixed-output fetch:
  `webrtc-0001d84-2/webrtc-linux-x64-release.zip`
  (sha256-akGuXN8n6o/ft+KuPRq9prdNiRe3e+rFxj7isEjij/0=), injected via the
  documented `LK_CUSTOM_WEBRTC` env var.
- Functional check: `dlopen_check` harness (from
  an earlier out-of-tree build) — DLOPEN-OK, all 3 FFI symbols
  resolve, ALL-PASS.
