# Building `livekit_ffi.dll` + `livekit_ffi.pdb` on Windows (x86_64)

> **Build tooling, not a Unity asset.** These scripts live under `BuildScripts~/` (the trailing
> `~` keeps the folder out of Unity's asset pipeline). They **download** the rust source into a
> gitignored `.src/` subfolder and build the native FFI library there; the source is never committed.
> They are **not** part of the shipped package and are not run from `Runtime/Plugins`.

Build the **`livekit-ffi`** crate as a release cdylib **with PDB debug symbols** on Windows, target `x86_64-pc-windows-msvc`. Everything is driven by `build.config.toml`:

| File | What it is |
|------|------------|
| `build.config.toml` | Config: `Tag` (which release to download/build) and `InstallToPlugins` (where the output goes). Edit this, not the scripts. |
| `Patch.toml` | The `[profile.release]` overlaid onto the downloaded `Cargo.toml`. See its header comment for what each flag does and why; edit it to change build flags. |
| `env-setup.ps1` | One-time bootstrap (PowerShell, so it runs on a bare machine): installs Python 3, VS2022 Build Tools (MSVC v143) + Windows 11 SDK, Git, Rust (MSVC), libclang, protoc, dasel. |
| `build-win.py` | Clones the `Tag` source (with nested submodules) into `.src/`, overlays `Patch.toml` onto `[profile.release]` for PDB output, runs `cargo build`, and places the DLL + PDB per `InstallToPlugins`. |

## Quick start

```powershell
cd BuildScripts~/windows
.\env-setup.ps1             # one-time bootstrap (installs Python + the toolchain)
notepad build.config.toml   # set Tag + InstallToPlugins
python build-win.py
```

## Configuration (`build.config.toml`)

```toml
Tag                   = "livekit-ffi/v0.12.48"   # release to build; tags at the releases page below
SourceDir             = ".src"                   # where to clone the source
InstallToPlugins      = true                     # see below
CleanSourceAfterBuild = false                    # delete the clone after a successful build
```

- `Tag`: a `livekit-ffi/vX.Y.Z` tag from <https://github.com/livekit/rust-sdks/releases>.
- `SourceDir`: where the source is cloned. Relative paths resolve against this tool folder; absolute paths (e.g. `C:/src`) work too. The default `.src` is gitignored; if you point it at another in-repo folder, add that to `.gitignore` yourself. An absolute path outside the repo also sidesteps the long-paths note below.
- `InstallToPlugins`:
  - `true` -> the built `livekit_ffi.dll` + `livekit_ffi.pdb` **replace** the ones in `Runtime/Plugins/ffi-windows-x86_64/` (ready to ship).
  - `false` -> they are dropped in this tool folder (`BuildScripts~/windows/`, gitignored) for inspection; copy them over yourself.
- `CleanSourceAfterBuild`: `true` removes the `<SourceDir>/rust-sdks-<tag>` checkout once the DLL + PDB are placed; `false` (default) keeps it so re-runs skip the clone.

The source is cloned to `<SourceDir>\rust-sdks-<tag>\` and reused on re-runs. The two output files:

| File | Purpose |
|------|---------|
| `livekit_ffi.dll` | Runtime cdylib. CRT is statically linked (`+crt-static`), so no VC++ Redistributable is needed. |
| `livekit_ffi.pdb` | Debug symbols, paired to that exact DLL (matching RSDS GUID). Needed only to debug/symbolicate, not to run. |

## Notes on the build

- **Source is downloaded, not vendored.** `build-win.py` does `git clone --recurse-submodules` of `livekit/rust-sdks` at the tag into `.src/` (gitignored); this also pulls the nested `yuv-sys/libyuv` + `livekit-protocol/protocol` submodules. webrtc is downloaded separately by `livekit-ffi/build.rs`. Nothing is added to this repo. (The `client-sdk-rust~` submodule that lives here is for C# proto generation via `generate_proto.sh`, not for this build.)
- **`+crt-static` comes from upstream, not the script.** The downloaded source's `.cargo/config.toml` sets `target-feature=+crt-static` for `x86_64-pc-windows-msvc`; cargo picks it up because the build runs from the source root. The script does not set it.
- **The profile patch is deliberate, and done with dasel.** `build-win.py` overlays `Patch.toml`'s `[profile.release]` onto the downloaded `Cargo.toml` using dasel, feeding the source `Cargo.toml` to dasel on stdin; see `Patch.toml` for the flags and what each is for. dasel reformats the file (reorders tables, single-quotes strings, drops comments), which is harmless since the checkout under `.src/` is a throwaway. This matches upstream's own release profile, so for recent tags it just re-asserts existing values. It requires the source to already define `[profile.release]` (livekit-ffi tags do); if one ever doesn't, the patch step fails loudly rather than silently shipping a symbol-less build.
- **Long paths (read this if the C++ compile fails).** webrtc's bundled headers nest deeply: under the default `SourceDir = ".src"` (inside this repo) their full paths reach ~390 characters, well past the Windows 260-char `MAX_PATH` limit. The clone itself survives (git runs with `core.longpaths=true`), but `cl.exe` is not long-path aware unless the machine has `LongPathsEnabled=1`, so the webrtc-sys C++ compile fails with `fatal error C1083: Cannot open include file` **on a header that actually exists** - the path is simply too long to open. Two fixes, either is enough:
  - Set `SourceDir` to a short absolute path **outside** the repo, e.g. `C:/src` (recommended - no admin needed). This is what shortens the deep paths back under the limit.
  - Or enable Windows long paths once, as admin: set `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled = 1` (DWORD) and reboot.

## Why VS2022 + Windows 11 SDK

The prebuilt `webrtc.lib` references VS2022 STL symbols (`__std_find_trivial_*`) and is built against the Windows 11 SDK (`NTDDI_WIN11_*`). VS2019 or a Windows 10 SDK will not compile or link.

## Shipping notes

- To **run**: ship `livekit_ffi.dll` only. It loads on stock Windows 10/11 **x86_64** in any 64-bit host process.
- To **debug/symbolicate**: keep `livekit_ffi.pdb` paired with that exact DLL, or put it in a symbol store.
- Architecture: **x86_64** only. For arm64 build `aarch64-pc-windows-msvc` separately; a 64-bit DLL cannot load into a 32-bit process.

## Troubleshooting (symptom → cause)

| Symptom | Cause / fix |
|---|---|
| `Python installed but not yet on PATH` (env-setup) | env-setup just installed Python; open a new terminal so PATH refreshes, then re-run `.\env-setup.ps1`. |
| `python` is not recognized (running build-win.py) | Python not on PATH yet; open a new terminal after `env-setup.ps1`, or call it by full path. |
| `git not found` (build-win) | Git missing; re-run `env-setup.ps1` (it installs Git). |
| `dasel not found` (build-win) | dasel missing; re-run `env-setup.ps1` (it installs dasel). |
| `dasel failed to patch [profile.release]` | The chosen `Tag` has no `[profile.release]`; add one to `Patch.toml`'s target or pick a tag that defines it. |
| `yuv-sys` build.rs panics `NotFound` reading `include/libyuv` | libyuv submodule not fetched; delete the `.src\rust-sdks-*` checkout and re-run so the clone pulls submodules. |
| webrtc-sys C++: `fatal error C1083: Cannot open include file` (header that exists) | Paths exceed the 260-char `MAX_PATH` limit. Set `SourceDir` to a short path like `C:/src`, or enable `LongPathsEnabled=1` (admin). See the long-paths note above. |
| `bindgen`: `Unable to find libclang` | `LIBCLANG_PATH` unset/wrong → re-run `env-setup.ps1`. |
| build.rs: `Could not find protoc` | `PROTOC` unset → re-run `env-setup.ps1`. |
| `fileapi.h: error C2061: ... 'FILE_INFO_BY_HANDLE_CLASS'` | SDK too old; `NTDDI_WIN11_*` undefined → install a Windows 11 SDK. |
| `LNK2019/LNK2001: __std_find_trivial_*`, `__std_find_last_trivial_*` | Linking with VS2019 STL; build with the VS2022 v143 toolset. |

## Manual build

If you prefer not to use the scripts, see `build-win.py` for the exact steps: install the toolchain, `git clone --recurse-submodules` the tag, set the `[profile.release]` block (the contents of `Patch.toml`), then run `cargo build --release -p livekit-ffi` from the source root in a shell with `vcvarsall.bat x64 <SDKVER>` loaded and `LIBCLANG_PATH` / `PROTOC` set.
