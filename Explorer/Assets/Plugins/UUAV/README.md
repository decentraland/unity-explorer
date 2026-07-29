# UUAV (Ultimately United Audio/Video)

A video/audio player for Unity built on a native Rust core and FFmpeg, with hardware-accelerated video decoding: D3D11VA on Windows, VideoToolbox on macOS (Apple Silicon + Metal).

- `native/` - Rust crate compiled to `uuav.dll` / `libuuav.dylib` (`cdylib`, C ABI)
- `Packages/UUAV/` - Unity package (`com.nickkhalow.uuav`) with the C# bindings and the `UUAVPlayer` component

## How it works

The C# side is a thin binding layer. `UUAVRuntime` initializes the native runtime once at startup: it passes a probe texture (that's how the plugin captures Unity's graphics device - D3D11 or Metal), the audio output format and an error callback. Everything else goes through `UUAVPlayer`, a MonoBehaviour that talks to the native player by id.

On the native side each player owns a dedicated playback thread that demuxes and decodes the media. Video is decoded on the GPU (D3D11VA on Windows, VideoToolbox on macOS; hardware decode is mandatory on both, NV12 only). When a frame is due, Unity's render thread (via `GL.IssuePluginEvent`) triggers a copy of the decoded surface into presentation textures: on Windows one shared NV12 texture whose Y/UV planes C# wraps as two format-cast views, on macOS two per-plane `MTLTexture`s (Y `R8Unorm`, UV `RG8Unorm`) blitted from the decoded frame's IOSurface. C# wraps them with `Texture2D.CreateExternalTexture` and blits to a regular `RenderTexture` through the `Hidden/UUAV/NV12ToRGB` shader. That `RenderTexture` is what you get from `player.CurrentTexture`.

Audio is decoded and resampled to interleaved `f32` at the engine's output format, buffered in a lock-free ring, and pulled by Unity from `OnAudioFilterRead`. Playback is paced by a master media clock, which can also be slaved to an external clock via `AssignMasterClock` (useful for syncing multiple players).

Opening a URL is async and cancellable; closing never blocks the engine - playback threads are cancelled and abandoned, never joined on the hot path.

### Basic usage

```csharp
var player = UUAVPlayer.New();      // or add the component in the editor
player.OpenMedia("https://example.com/video.mp4");
player.Play();

// render player.CurrentTexture (RenderTexture) anywhere - UI RawImage, material, etc.
```

See `Packages/UUAV/Example/UIExamplePlayer.cs` for a working example.

## Why Rust

Mostly safety: this is code that shares a D3D11 device and threads with the engine, and memory/thread-safety bugs there are miserable to debug. Rust catches most of them at compile time; the unsafe surface is confined to the FFI boundary, FFmpeg calls and D3D interop.

The other reason is reusability. The output is a plain C-ABI DLL with nothing engine-specific baked in - the same `uuav.dll` can be embedded in other engines or apps (Unreal, Godot, whatever), only the thin C# binding layer is Unity-specific.

## Building

### Windows

Prerequisites:

- Rust toolchain with the GNU target: `rustup target add x86_64-pc-windows-gnu`
- MSYS2 with the mingw64 toolchain at `C:\msys64` (the linker is pinned to `C:\msys64\mingw64\bin\gcc.exe` in `native/.cargo/config.toml`)
- FFmpeg **8.1** development files (headers + import libs) in `native/.third_party/ffmpeg/`. Use the [BtbN](https://github.com/BtbN/FFmpeg-Builds/releases) **LGPL shared** win64 build. `FFMPEG_DIR` already points there via `native/.cargo/config.toml`.

Then:

```bash
cd native
./build.sh
```

This runs `cargo build --release --target x86_64-pc-windows-gnu` and copies `uuav.dll` into `Packages/UUAV/Runtime/Plugins/x86_64/`.

### macOS (universal: Apple Silicon + Intel)

The binaries are universal (arm64 + x86_64), but the **build host** must be Apple Silicon - the x86_64 slice is cross-compiled from arm64.

Prerequisites:

- Xcode (or the Command Line Tools) for clang, Metal frameworks and `codesign`
- Rust toolchain with both targets: `rustup target add aarch64-apple-darwin x86_64-apple-darwin`
- `brew install nasm` (assembles the x86_64 SIMD kernels; the FFmpeg build fails fast without it)

FFmpeg is built from source once (the macOS analog of dropping the BtbN build into `.third_party/ffmpeg`):

```bash
cd native
./scripts/build-ffmpeg-macos.sh   # clones FFmpeg n8.1, LGPL shared universal build into .third_party/ffmpeg
./build.sh
```

Both scripts produce fat binaries: FFmpeg is built out-of-tree once per arch and the dylibs merged with `lipo`; `libuuav.dylib` comes from two cargo targets merged with `lipo -create`. `build.sh` copies `libuuav.dylib` plus the seven FFmpeg dylibs into `Packages/UUAV/Runtime/Plugins/macOS/`, ad-hoc code-signs everything (mandatory on arm64) and ends by running `doctor-libs.sh`, which fails the build if any deployed dylib is not universal.

Dylib loading needs no `install_name_tool` post-processing: FFmpeg is configured with `--install-name-dir='@rpath'` and `libuuav.dylib` carries an `LC_RPATH @loader_path` entry (set in `native/.cargo/config.toml`), so the whole set resolves from the plugin folder - both in the editor and in a built player (`Contents/PlugIns/`). Verify with `otool -L libuuav.dylib`.

## Runtime deployment

`uuav.dll` dynamically links FFmpeg, so the FFmpeg runtime DLLs must sit next to `uuav.dll` (`Packages/UUAV/Runtime/Plugins/x86_64/` in-project, or the plugins folder of a built player). The exact set, taken from the FFmpeg **8.1** BtbN LGPL shared build:

```
avcodec-63.dll
avdevice-63.dll
avfilter-12.dll
avformat-63.dll
avutil-61.dll
libbz2-1.dll
libiconv-2.dll
liblzma-5.dll
libwinpthread-1.dll
swresample-7.dll
swscale-10.dll
zlib1.dll
```

The library major versions (avcodec **63**, avformat **63**, avutil **61**, swresample **7**, swscale **10**, avfilter **12**, avdevice **63**) are tied to the FFmpeg 8.1 release. A different FFmpeg version ships differently-named DLLs (`avcodec-62.dll`, …) that `uuav.dll` won't load - keep the DLL set and `ffmpeg-sys-next = "8.1.0"` in `native/Cargo.toml` in lockstep.

A few notes:

- `libwinpthread-1.dll` is mandatory. The FFmpeg chain links against it; if it isn't next to the plugin, the loader falls back to `PATH` (which may hold an incompatible copy, e.g. Git's older mingw64) and `LoadLibrary` fails with `ERROR_PROC_NOT_FOUND (127)`.
- FFmpeg is used under LGPL as a dynamically-linked shared library; see the license shipped with the BtbN build.

On macOS the equivalent set lives in `Packages/UUAV/Runtime/Plugins/macOS/` (deployed by `build.sh`; the majors are from the FFmpeg n8.1 source build and differ from the BtbN win64 numbering):

```
libuuav.dylib
libavcodec.62.dylib
libavdevice.62.dylib
libavfilter.11.dylib
libavformat.62.dylib
libavutil.60.dylib
libswresample.6.dylib
libswscale.9.dylib
```

No extra support libraries are needed: https uses SecureTransport (an OS framework), and zlib/bzip2/iconv come from the OS.

macOS notes:

- **Universal binaries** (arm64 + x86_64), **Metal only**: `UUAVRuntime` refuses to initialize on any other graphics API, because native has no way to validate that the probe texture pointer is an `id<MTLTexture>`. Verify the slices with `lipo -archs <dylib>` or by running `doctor-libs.sh` in the plugins folder.

## GPU device synchronization

The native decoder shares Unity's D3D11 device (captured from a probe texture at init, see `native/src/hw_device.rs`). The `ID3D11Device` itself is free-threaded, but the immediate context is not - D3D11VA decode calls on the playback thread would otherwise race Unity's render thread and hang the runtime.

`SetMultithreadProtected(true)` used for the immediate context, performance affection is negligible in practice. All it does is wrap each context call in an internal critical section: uncontended that's nanoseconds, and contention only happens during the short window where a decoded frame is copied into the presentation texture. Next to the actual GPU work and Unity's own command submission this is noise based on the investigation.

The alternative - a separate D3D11 device for FFmpeg with frames passed across via `D3D11_RESOURCE_MISC_SHARED` textures - was considered and rejected. It removes immediate-context contention entirely, but adds cross-device texture lifetime management, per-frame open/acquire/release complexity and additional failure surface, all to avoid a cost that was already negligible.

None of this applies on macOS: VideoToolbox owns its decode pipeline end to end, the plugin blits on its own `MTLCommandQueue`, and `MTLDevice` is free-threaded. The per-frame blit is committed with `waitUntilCompleted`, which both orders it against Unity's queue and guarantees the borrowed decoded frame outlives the GPU reads; replacing that wait with `MTLSharedEvent`/`IUnityGraphicsMetal` synchronization is deliberately deferred optimization headroom.
