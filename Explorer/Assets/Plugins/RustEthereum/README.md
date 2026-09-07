# rust_eth native library (Linux)

`librust_eth.so` is the Ethereum signing/verification FFI library from the
rust-ethereum Unity package, built from the workspace crate `crates/rust_eth`.

- **Upstream:** <https://github.com/decentraland/rust-ethereum> (Apache-2.0).
- **Version:** **v0.2.3**, rev `dc9994994637d30b0a61e2354eb2bbb8e963fa33`. The
  file committed here is byte-identical to `native/linux/librust_eth.so` inside
  the upstream release asset `rust-eth-natives-v0.2.3.zip` — it is the upstream
  release build, not a build made in this repository.
- The file is `lib`-prefixed because `DllImport("rust_eth")` on Linux resolves
  only `lib`-prefixed sonames.

## Rebuilding

From the repository root:

```sh
nix-build scripts/native-provenance/drv/rust-eth/default.nix
```

The derivation fetches the pinned tag, vendors exactly what its `Cargo.lock`
pins, and runs the crate's own signing round-trip tests as a build-time check.

The rebuild is **not** byte-identical: the committed file was built with rustc
1.95.0, and the derivation builds with the rustc of the current package set.
Its exported dynamic symbol table is identical; the byte differences are
toolchain and build-path noise. Treat it as an ABI-equivalent replacement, not
a reproduction of these bytes.

The recorded verdict is the `rust-eth` entry in
[`scripts/native-provenance/PROVENANCE.lock`](../../../../scripts/native-provenance/PROVENANCE.lock).
