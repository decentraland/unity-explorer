# Sentry native library (Linux)

`libsentry.so` is the Sentry native crash-reporting library, as packaged for
Unity. Its embedded SDK identifier is `sentry.native.unity/0.12.2`.

- **Upstream:** <https://github.com/getsentry/sentry-native> (MIT), tag
  **0.12.2** = commit `61a2667bfffee48d7962affac9c5230cda93dce9`.
- **Where these bytes come from:** the Sentry Unity SDK
  (<https://github.com/getsentry/sentry-unity>) builds it in its own CI and
  commits the result; the file here is byte-identical to the `libsentry.so`
  committed in the Unity SDK release whose `modules/sentry-native` submodule
  points at that revision. It is not built in this repository.
- **Build shape** (reproduced by the derivation): breakpad backend, SDK name
  `sentry.native.unity`, `RelWithDebInfo`, no runpath, then a partial strip
  keeping only `sentry_*` in `.symtab` and a gnu-debuglink to the unstripped
  companion `libsentry.dbg.so`.

## Rebuilding

From the repository root:

```sh
nix-build scripts/native-provenance/drv/sentry/default.nix
```

The derivation fetches the pinned tag with submodules (breakpad lives in
`external/breakpad`) and fails if any of the `sentry_*` exports the committed
file relies on is missing.

The rebuild is **not** byte-identical — the compiler and curl/zlib versions
float with the package set. Its exported dynamic symbol table is identical to
the committed file's; the byte differences are codegen noise. Treat it as an
ABI-equivalent drop-in, not a reproduction of these bytes.

The recorded verdict is the `sentry` entry in
[`scripts/native-provenance/PROVENANCE.lock`](../../../../scripts/native-provenance/PROVENANCE.lock).
