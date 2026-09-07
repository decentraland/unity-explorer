# Native audio analysis

`Libraries/audio-analysis.{so,dll,dylib}` is the audio spectrum analyser used by
scene audio reactivity. It is a Rust cdylib exposing a single entry point,
`audio_analysis_analyze_audio_buffer`.

- **Source:** first-party, in this repository at `native/audio-analysis` (crate
  `audio-analysis` 0.1.0). There is no separate upstream project and the crate
  declares no license of its own.
- **DSP:** the `fundsp` crate, **0.20.0** — the version pinned by the
  derivation's lockfile, matching the registry path embedded in the Linux
  library.

## Building

- macOS (`audio-analysis.dylib`): `compile_mac.sh` (universal, lipo).
- Windows (`audio-analysis.dll`): `compile_win.bat`.
- Linux (`audio-analysis.so`): no script; `cargo build --release` in the crate
  produces `target/release/libaudio_analysis.so`, shipped here renamed to
  `audio-analysis.so` to match `DllImport("audio-analysis")`.

## Rebuilding `audio-analysis.so` from pinned sources

From the repository root:

```sh
nix-build scripts/native-provenance/drv/audio-analysis/default.nix
```

The derivation builds a copy of the crate vendored beside it, hash-asserted and
byte-identical to `native/audio-analysis`. The crate ships no `Cargo.lock`, so
the derivation carries one it resolved itself; the transitive dependency
versions in it are that resolution, not a recovered original.

The rebuild is **not** byte-identical to the committed `audio-analysis.so`. It
exports the same dynamic symbol table — the single entry point, at the same
address — and requires the same shared libraries. The byte differences are
toolchain and vendoring noise: the embedded cargo vendor prefix, an extra ELF
hash section, runpath entries, and crate metadata disambiguators. Treat it as an
ABI-equivalent replacement, not a reproduction of these bytes.

The recorded verdict is the `audio-analysis` entry in
[`scripts/native-provenance/PROVENANCE.lock`](../../../../scripts/native-provenance/PROVENANCE.lock).
