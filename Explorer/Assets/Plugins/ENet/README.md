# ENet native transport

`runtimes/<rid>/native/` holds the native ENet library that the managed ENet
bindings P/Invoke.

- **Upstream:** <https://github.com/SoftwareGuy/ENet-CSharp> (MIT), the
  maintained fork of `nxrighthere/ENet-CSharp`. This is not vanilla
  `lsalzman/enet`: `enet_linked_version()` reports `0x20409` (2.4.9) and the
  library exports `enet_crc64` and `enet_array_is_zeroed`.
- **Version:** 2.4.9, pinned at rev `14c4dc5590938af8fe04ecbb2890b1ba47e80ec8`
  (tag `v2.4.9pre6`). `Source/Native/` is identical across the four `v2.4.9pre`
  tags and all four publish the same Linux shared object.

## Rebuilding `runtimes/linux-x64/native/libenet.so`

From the repository root:

```sh
nix-build scripts/native-provenance/drv/enet/default.nix
```

The result is **byte-identical** to the file committed here, build-id included,
and the derivation asserts that sha256 so toolchain drift fails loudly instead
of quietly degrading to "equivalent".

Byte-identity needs two separately pinned toolchains, both fixed inside the
derivation: gcc 11.4.0 for codegen, and binutils 2.38 for the link. binutils
2.39 dropped the BND prefix from the IBT PLT, so no later linker can emit this
file's PLT bytes.

`runtimes/macos-universal/native/libenet.dylib` and
`runtimes/win-x64/native/enet.dll` come from the same upstream release, but no
derivation rebuilds them and their bytes are not verified here.

The recorded verdict is the `enet` entry in
[`scripts/native-provenance/PROVENANCE.lock`](../../../../scripts/native-provenance/PROVENANCE.lock).
