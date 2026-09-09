# rust-segment

To access the native part go to .native/ directory.

Native Segment analytics client exposed via a C ABI for integration with Unity and other foreign runtimes.

This crate builds a native library (`cdylib`) to submit analytics operations.

---

## Overview

* Language: Rust
* Output: native dynamic library (`.dylib` / `.dll`)
* Interface: C ABI
* Primary use case: Unity client integration

---

## Project Structure

```
src/
  lib.rs          Public entry point and globals
  cabi.rs         C ABI surface (extern "C")
  server.rs       Native server lifecycle and runtime ownership
  operations.rs   Construction of analytics operations
```

Build script: `build.sh` (both platforms). Toolchain pin: `rust-toolchain.toml`.

---

## Build

The shipped binaries are built by CI, not by hand — see [CI verification of the shipped binaries](#ci-verification-of-the-shipped-binaries). A local build is for development; its output must not be committed, because the lock records the bytes CI produced.

Prerequisites (both platforms): Rust via `rustup`. `rust-toolchain.toml` pins the compiler and lists the targets, and rustup installs both on the first `cargo` invocation from `.native/`, so no manual `rustup target add` is needed.

### macOS (universal: Apple Silicon + Intel)

The build host must be Apple Silicon; the x86_64 slice is cross-compiled.

```sh
cd .native
./build.sh
```

Builds both slices, merges them with `lipo` into `SegmentServerWrap/Libraries/Mac/segment-server.dylib`.

### Windows

Prerequisites: Visual Studio Build Tools with the C++ x64 toolset (rustc locates `link.exe` on its own; `rusqlite`'s bundled SQLite is compiled with `cl.exe` from the same toolset).

```sh
cd .native
bash build.sh
```

Builds `x86_64-pc-windows-msvc` and copies the DLL to `SegmentServerWrap/Libraries/Windows/segment-server.dll`. The target is MSVC, not GNU: the DLL has always imported the UCRT that ships with the player in `Plugins/.VCRedist`.

`build.sh` passes `--locked`, so a manifest change that would rewrite `Cargo.lock` fails the build instead of silently resolving different crates than the lock records. `.cargo/config.toml` carries the flags that make the MSVC link deterministic (`/Brepro`, `/PDBALTPATH`).

`Libraries/Linux/segment-server.so` is not produced by `build.sh` and is outside the lock: Linux is not a release target and the file predates the crate's current source.

---

## Public C API

### Initialization

```c
bool segment_server_initialize(
    const char* queue_file_path,
    uint32_t queue_count_limit,
    const char* segment_write_key,
    FfiCallbackFn callback_fn,
    FfiErrorCallbackFn error_fn
);
```

Notes:

* Must be called exactly once before any operations.
* All pointer arguments must remain valid for the duration of the call.
* Callbacks must be thread-safe and non-blocking.

---

### Operations

Operations are asynchronous. Completion and errors are reported via the provided callbacks.

### Response codes

Each operation completes through `FfiCallbackFn` with one of:

| Code | Name | Meaning |
|---|---|---|
| 0 | `Success` | Operation completed. |
| 1 | `Error` | Generic failure; details arrive via the error callback. |
| 2 | `ErrorDiskFull` | The persistent queue cannot write because the disk is full (SQLITE_FULL). |

`Libraries/Linux/segment-server.so` predates `ErrorDiskFull` and has not been rebuilt (Linux is not a release target), so on Linux a full disk still completes with `Error`.

---

## Shutdown / Disposal Contract

Correct shutdown ordering is required.

### Required shutdown sequence

During application shutdown or disposal:

1. Stop issuing new operations from the foreign runtime.
2. Ensure no callbacks are expected after disposal begins.
3. Release the native library.

## Testing

`cargo test` runs the unit tests (disk-full response mapping, operation construction). The integration test is `#[ignore]`d because it needs a Segment write key and the network; run it explicitly with:

```sh
SEGMENT_WRITE_KEY=... SEGMENT_QUEUE_PATH=... cargo test -- --ignored
```

`rust-segment-verify.yml` runs `cargo check`, `cargo test` and `cargo clippy -- -D warnings` on every PR touching this plugin.

## CI verification of the shipped binaries

The committed binaries are pinned by a hash lock, `scripts/rust-segment/rust-segment-binaries.lock.json`, and two workflows enforce it. The design is the one UUAV uses (see [`Explorer/Assets/Plugins/UUAV/README.md`](../UUAV/README.md) and [`docs/uuav.md`](../../../../docs/uuav.md) for the rationale); the scripts under `scripts/rust-segment/` are its own copies, minus the FFmpeg provenance checks this crate has no use for.

- **`rust-segment-verify.yml`** runs on every PR touching this plugin or `scripts/rust-segment/`. It re-hashes the shipped `.dylib`/`.dll` and every build input that feeds them (`.native/src`, `Cargo.toml`, `Cargo.lock`, `.cargo/config.toml`, `rust-toolchain.toml`, `build.sh`) against the lock, fails on any binary under a `Libraries/<Mac|Windows>` folder the lock does not name, and requires new native binaries to be stored in Git LFS. It also checks, tests and lints the crate on a macOS runner and runs `cargo audit` over the committed `Cargo.lock` (also weekly on a schedule, since advisories land without any PR touching the crate).
- **`rust-segment-native.yml`** runs on demand (`workflow_dispatch`, or the `build-rust-segment-native` PR label). It rebuilds both targets on hosted runners under the pinned toolchain, then runs two gates: Gate A (`scripts/rust-segment/repro-gate.sh`) builds twice from two source trees through one canonical path (`scripts/rust-segment/build-canonical.sh`) and requires byte-identical output; Gate B (`scripts/rust-segment/reproduces-lock.py`) compares the fresh build against the committed hashes, hard-failing only when the runner's toolchain matches the one pinned in the lock. Its step summary opens with a per-target Gate B line (`reproduced` or `skipped - <reason>`), so a green run that verified nothing is visible at a glance. Runners are dated images (`macos-15`, `windows-2025`) because the lock pins their toolsets. It has `contents: read` and never commits.

### Relocking after a deliberate rebuild

Relocking is the human step that commits both the CI-built binaries and the lock that describes them:

1. Label the PR `build-rust-segment-native` and let both jobs finish. Download the `rust-segment-macos-universal` and `rust-segment-windows-x86_64` artifacts.
2. Commit each artifact's binary into `SegmentServerWrap/Libraries/<Mac|Windows>/` (LFS picks them up through the `*.dylib` / `*.dll` rules).
3. Relock each target, passing the toolchain the build recorded:

```sh
python3 scripts/rust-segment/verify-binaries.py --update --only macos-universal --toolchain toolchain-macos.txt
python3 scripts/rust-segment/verify-binaries.py --update --only windows-x86_64 --toolchain toolchain-windows.txt
```

`--toolchain` is what makes the relock honest about who built the bytes: without it `--update` refuses on any host whose pinned components differ from `targets.<t>.rust.toolchain`, and for a target with no pin yet the flag gives it its first one. On Windows the pinned components are `rustc`, `cargo` and `msvc` (the VC++ toolset `link.exe` came from); on macOS `rustc`, `cargo`, `clang`, `xcode` and `sdk` (`ld` is recorded and deliberately not pinned).

Two things about the round trip are easy to get wrong:

- **Gate B needs a second run.** The round that produces the binaries compares them against the lock as it stood *before* the relock, so it skips. It becomes a real byte comparison on the next labelled run, after the relocked lock and the new binaries are pushed.
- **`.native/` must not move between the build and the relock.** The binaries come from the commit CI built; `source_digest` is taken from the working tree at relock time. One more source edit in between and the two disagree again.

The lock digests the build inputs as raw bytes, so `.gitattributes` forces `eol=lf` across `.native/`.
