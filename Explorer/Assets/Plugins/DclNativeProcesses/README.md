# DclNativeProcesses

Small C plugin (`dcl_processes.c` / `dcl_processes.h`, sources in this folder)
exposing process helpers that IL2CPP's `Process` support does not cover:
`get_process_name`, `start_process`, `dcl_start_process_blocking`, `free_name`.
First-party source; there is no upstream project.

Rebuild per platform:

- Linux (`libDCLProcesses.so`, plain libc — `posix_spawn` + `/proc`):

  ```sh
  gcc -shared -fPIC -O2 -o libDCLProcesses.so dcl_processes.c
  ```

- macOS (`libDCLProcesses.dylib`): `build.sh` (universal clang build).
- Windows (`DCLProcesses.dll`): `build.bat` (clang).

## Rebuilding `libDCLProcesses.so` from pinned sources

From the repository root:

```sh
nix-build scripts/native-provenance/drv/c-shims/default.nix
```

The result is **byte-identical** to the `libDCLProcesses.so` committed here.
The committed library is a `clang -shared -fPIC -O2` build, not a gcc one.

Two things to know before touching it:

- The derivation compiles a hash-asserted copy of `dcl_processes.c` vendored
  beside it, **not** the copy in this folder. The two differ in
  `get_process_name`: they trim the trailing newline off `/proc/<pid>/comm`
  differently and are functionally equivalent, but only the vendored variant
  compiles to the committed bytes. Rebuilding from the source in this folder
  produces a working library that is not this one.
- Byte-identity holds against a package set that resolves the same compiler and
  glibc store paths this file's runpath records. On a different package set the
  build still succeeds and grades ABI-equivalent instead.

The same derivation also produces `libWindowResizeConstraint.so`; see
`Explorer/Assets/Plugins/NativeWindowManager/Linux/README.md`. Verdicts are
recorded in
[`scripts/native-provenance/PROVENANCE.lock`](../../../../scripts/native-provenance/PROVENANCE.lock)
under `c-shims-dclprocesses` and `c-shims-windowresize`.
