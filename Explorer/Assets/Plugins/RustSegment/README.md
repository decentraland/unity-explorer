# rust-segment

To access the native part go to .native/ directory.

Native Segment analytics client exposed via a C ABI for integration with Unity and other foreign runtimes.

This crate builds a native library (`cdylib`) to submit analytics operations.

---

## Overview

* Language: Rust
* Output: native dynamic library (`.dylib` / `.dll` / `.so`)
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

Build scripts:

* `compile_mac.sh`
* `compile_win.bat`

---

## Build

### macOS

```sh
./compile_mac.sh
```

### Windows

```bat
compile_win.bat
```

The resulting dynamic library is produced via `crate-type = ["cdylib"]`.

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

`Libraries/Linux/segment-server.so` predates `ErrorDiskFull` and has not been rebuilt, so on Linux a full disk still completes with `Error`. Linux is a shipping target, so this binary is due a rebuild.

---

## Provenance

Unlike the other shipped natives, this library has no derivation under
[`scripts/native-provenance/`](../../../../scripts/native-provenance/README.md)
and therefore cannot be rebuilt from pinned sources or checked against an
upstream release. What the committed Linux binary establishes about itself:
it links the `segment` analytics crate 0.2.4, `tokio` 1.40.0 and `serde_json`
1.0.128, and it was produced on a developer workstation rather than by a
reproducible build. The crate that wraps them — the one owning `src/server.rs`
and the `segment_server_*` C entry points — is not identified by anything in
this repository.

Closing this gap means locating that source, adding a derivation beside the
others, and rebuilding all three platform binaries.

---

## Shutdown / Disposal Contract

Correct shutdown ordering is required.

### Required shutdown sequence

During application shutdown or disposal:

1. Stop issuing new operations from the foreign runtime.
2. Ensure no callbacks are expected after disposal begins.
3. Release the native library.

## Testing

Integration tests can be run locally with:

```sh
SEGMENT_WRITE_KEY=... SEGMENT_QUEUE_PATH=... cargo test
```
