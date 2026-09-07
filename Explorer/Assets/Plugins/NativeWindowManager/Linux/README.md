# WindowResizeConstraint (Linux)

`libWindowResizeConstraint.so` is the X11 / XWayland implementation of the
WindowResizeConstraint plugin, built from `WindowResizeConstraint.c` in this
folder. It exports the same two entry points as the Windows and macOS builds,
`WindowConstraint_Init` and `WindowConstraint_Set`.

Ad-hoc rebuild (any gcc with libX11 headers):

```sh
gcc -shared -fPIC -O2 -o libWindowResizeConstraint.so WindowResizeConstraint.c -lX11
```

## Rebuilding from pinned sources

From the repository root:

```sh
nix-build scripts/native-provenance/drv/c-shims/default.nix
```

The result is **byte-identical** to the file committed here. The derivation
compiles the copy of `WindowResizeConstraint.c` vendored beside it, which is
hash-asserted byte-identical to the copy in this folder, with the flags this
file was linked with (`-O2 -fstack-protector-strong
-fzero-call-used-regs=used-gpr -Wl,-z,now`, plus `-lX11`).

Two things to know before touching it:

- Byte-identity holds against a package set that resolves the same compiler,
  glibc and libX11 store paths this file's runpath records. On a different
  package set the build still succeeds and grades ABI-equivalent instead.
- The committed library's runpath leads with an entry that resolves nowhere. It
  is inert, and the derivation reproduces it deliberately — removing it changes
  the bytes.

The same derivation also produces `libDCLProcesses.so`; see
`Explorer/Assets/Plugins/DclNativeProcesses/README.md`. Verdicts are recorded in
[`scripts/native-provenance/PROVENANCE.lock`](../../../../../scripts/native-provenance/PROVENANCE.lock)
under `c-shims-windowresize` and `c-shims-dclprocesses`.
