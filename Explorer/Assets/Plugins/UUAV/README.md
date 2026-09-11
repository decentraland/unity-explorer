# UUAV (Ultimately United Audio/Video)

A video/audio player for Unity built on a native Rust core and FFmpeg, with hardware-accelerated video decoding: D3D11VA on Windows, VideoToolbox on macOS (Apple Silicon + Metal). Decoding runs **out of process** — FFmpeg never shares Unity's address space or GPU device, so a decoder crash cannot take the engine down. The decode process is also **sandboxed** on both platforms, so a decoder exploit lands in a process that cannot launch anything or write to disk (see [Sandboxing](#sandboxing)). The only exception is the debug-only `UUAV_NO_IPC_LAYER` mode, which cannot reach a release player build (see [Debug-only in-process mode](#debug-only-in-process-mode-uuav_no_ipc_layer)).

- `native/` - Rust workspace:
  - `src/` (**uuav-core**) - the decode/playback core, linked into the helper
  - `uuav-client/` - compiled to `uuav.dll` / `libuuav.dylib` (`cdylib`, C ABI) - the middleware Unity loads
  - `uuav-server/` - compiled to `uuav-helper(.exe)` - the process that hosts the core
  - `uuav-ipc/` - the wire protocol + native channel/mach plumbing shared by both
- `Packages/UUAV/` - Unity package (`com.nickkhalow.uuav`) with the C# bindings and the `UUAVPlayer` component

## How it works

The C# side is a thin binding layer and is identical to the old in-process design. `UUAVRuntime` initializes the runtime once at startup: it passes a probe texture (that's how the client captures Unity's graphics device - D3D11 or Metal), the audio output format and error/log callbacks. Everything else goes through `UUAVPlayer`, a MonoBehaviour that talks to a player by id.

Under the hood the `uuav` library Unity loads is a client. At init it creates a duplex OS channel — a message-mode named pipe on Windows (overlapped I/O), an `AF_UNIX` socketpair on macOS — spawns `uuav-helper` from its own folder, and hands it its end of the channel by inheritance: on Windows the pipe handle is the only handle passed, via an explicit `PROC_THREAD_ATTRIBUTE_HANDLE_LIST`; on macOS the fd number rides argv. A session token in the first message guards against a stale helper generation. The helper creates its **own** GPU device on the same adapter (matched by LUID on Windows / registry id on macOS), initializes the unchanged uuav core against it, and from then on adapts IPC messages onto the core's C API:

- **Commands** (open/play/pause/seek/...) are forwarded as fire-and-forget messages; only init-time calls block on replies.
- **State** is pushed back at ~50 Hz per player; every per-frame C# getter is served from that local cache (`current_time` extrapolates between snapshots), so getters never touch the wire.
- **Video** stays on the GPU end to end. The helper drives the core's render events itself (~60 Hz), copies each presented frame into one of 3 cross-process shared slots — keyed-mutex NV12 textures on Windows (NT handles duplicated straight into Unity's process), per-plane IOSurfaces on macOS (ports transferred over a mach channel) — and publishes the slot. On Unity's render event (`GL.IssuePluginEvent`) the client copies the latest published slot into client-owned presentation textures on Unity's device: one NV12 texture on Windows (C# wraps Y/UV as two format-cast views), two per-plane `MTLTexture`s on macOS. C# wraps them with `Texture2D.CreateExternalTexture` and blits to a `RenderTexture` through `Hidden/UUAV/NV12ToRGB`; that is `player.CurrentTexture`. The pointers are stable across frames and survive resolution changes with a one-poll retire grace — the C# poll-and-rewrap flow is the same as it always was.
- **Audio** crosses the boundary as messages: the helper pulls interleaved `f32` from the core every 10 ms (standing in for Unity's audio thread, so the core's drift correction keeps working) and the client feeds a ~50 ms jitter ring that `OnAudioFilterRead` drains. Master-clock slaving (`AssignMasterClock`) is forwarded, throttled to the state cadence.

### Crash recovery

The helper is watched by a recovery worker inside the client. If it dies, playback self-heals with **no C#/ECS involvement**: every command keeps a per-player *desired state* (url, play/pause, looping, rate, resume position) current, so the worker respawns the helper (backoff 0 s/1 s/3 s), re-runs the init handshake, and rebuilds every player — reopen, resume playback, seek back to where the clock was. During the outage players read `UUAV_OPENING`, commands are absorbed into desired state, the video freezes on the last presented frame (the presentation textures C# wraps are client-owned and never die), and audio pads silence. Public player ids are allocated by the client and stay stable across helper generations. After three failed respawns the runtime parks (`UUAV_ERROR`) until the next open/play re-arms it. Every death still reports once through the error callback, so crashes stay visible in logs/Sentry.

The helper never outlives Unity: it watches the parent PID and exits when Unity goes away for any reason; `uuav_deinit` shuts it down gracefully (≤1 s, then kill).

### Sandboxing

FFmpeg demuxes attacker-controlled media (URLs come from untrusted scenes), so the helper runs with the least authority that still allows GPU decoding and outbound network access. On both platforms a compromised decoder cannot launch another process or persist files on disk; the enforcement mechanism is the native one for each OS, so the two implementations are deliberately different shapes:

- **Windows: restricted at spawn, by the client** (`uuav-client/src/sandbox_windows.rs`). The helper is created suspended under a restricted **low-integrity token** (elevation dropped, every privilege stripped but `SeChangeNotifyPrivilege`), assigned to a **single-process kill-on-close job object** (no children; the client holds the only job handle, so kernel cleanup kills the helper on any Unity exit), given a **process mitigation policy** (DEP, mandatory/bottom-up/high-entropy ASLR, control-flow guard, strict handle checks, heap terminate-on-corruption), and only then resumed - the limits bind before its first instruction. Winsock is unaffected by integrity level, so playback keeps its network access. One consequence: the low-IL helper cannot duplicate texture handles into Unity, so the client pulls them out instead.
- **macOS: self-lockdown under Seatbelt** (`uuav-server/src/sandbox_macos.rs` + `uuav-server/helper.sb`). As the first act of `main`, before the IPC channel is adopted and before any untrusted byte is parsed, the helper applies a **deny-by-default profile** via `sandbox_init_with_parameters` (the Chromium/Firefox mechanism; the profile is embedded at compile time, per-session values enter as sandbox parameters). Allowed: system library reads, Metal/IOSurface/VideoToolbox user clients, outbound TCP/UDP plus the DNS and trustd brokers (FFmpeg fetches media itself; TLS trust includes user-installed root CAs, so corporate TLS interception keeps working), and `mach-lookup` of exactly the one per-session `uuav.<token>` service the client registered for IOSurface transfer. `process-exec`, `process-fork` and every file write are denied. **Fail-closed**: if the profile fails to apply the helper exits and the client's recovery surfaces `UUAV_ERROR`; there is no bypass switch. The Editor's `file:` playback works because the client passes `--allow-file-read` when the protocol whitelist contains `file`, which opens broad *reads* only - player builds never pass it, and writes/exec stay denied regardless.

The macOS IOSurface channel authenticates its sender (`uuav-ipc/src/mach_channel.rs`): the per-session bootstrap name is discoverable by other local processes (shared namespace, helper argv), so the client's receiver requests the kernel audit trailer on every message and destroys, unread, anything not sent by the exact helper pid it spawned. A third local process that looks the service up and sends surface ports gets its rights released, never a rendered plane.

Debugging a macOS denial: run `log stream --style compact --predicate 'sender == "Sandbox"'` next to the Editor and look for `uuav-helper(pid) deny(1) <operation> <name>` lines, then extend `helper.sb` with a comment tying the new allowance to its need.

### Debug-only in-process mode (`UUAV_NO_IPC_LAYER`)

`uuav-core` also builds as a cdylib (`uuav_core.dll` / `libuuav_core.dylib`) exposing the same C ABI as the client. Defining `UUAV_NO_IPC_LAYER` in the scripting defines repoints every P/Invoke at it, running the decode core **inside Unity's process** with no helper, no IPC and no recovery worker. This exists purely as a debugging aid — to bisect whether a bug lives in the core or in the IPC/helper layer, and to step through decode code without attaching to a second process.

In this mode every guarantee in this section is off: no Seatbelt profile, no restricted low-integrity token, no kill-on-close job object, no process-mitigation policy, no separate GPU device, and a decoder crash takes the engine down with it. Because that must never ship to end users, `NativeMethods.cs` fails compilation with `#error` when `UUAV_NO_IPC_LAYER` is defined without `DEBUG` — Unity defines `DEBUG` only in the Editor and in Development builds, so a release player build with the define set cannot be produced. The protocol whitelist is unaffected by the switch: `uuav_core`'s `uuav_init` takes the same `protocol_whitelist` argument and the C# layer passes the same constant either way.

### Basic usage

```csharp
var player = UUAVPlayer.New();      // or add the component in the editor
player.OpenMedia("https://example.com/video.mp4");
player.Play();

// render player.CurrentTexture (RenderTexture) anywhere - UI RawImage, material, etc.
```

See `Packages/UUAV/Example/UIExamplePlayer.cs` for a working example.

### Headless smoke test

`native/uuav-client/examples/smoke.rs` exercises the whole pipeline without Unity: it creates a real GPU probe texture, walks the C ABI like `UUAVRuntime`/`UUAVPlayer` do, expects a stream to reach PLAYING with video pointers and audible audio, then **kills the helper and asserts playback self-recovers**. Build the workspace, copy `uuav-helper(.exe)` (and on Windows the FFmpeg DLLs from the deployed plugin folder) next to the example binary, and `cargo run -p uuav-client --example smoke [media-url]`.

## Why Rust

Mostly safety: this is code that juggles GPU resources and threads next to the engine, and memory/thread-safety bugs there are miserable to debug. Rust catches most of them at compile time; the unsafe surface is confined to the FFI boundary, FFmpeg calls and GPU interop.

The other reason is reusability. The output is a plain C-ABI DLL with nothing engine-specific baked in - the same `uuav` client + helper pair can be embedded in other engines or apps (Unreal, Godot, whatever), only the thin C# binding layer is Unity-specific.

## Building

### Windows

Prerequisites:

- Rust toolchain with the GNU target: `rustup target add x86_64-pc-windows-gnu`
- MSYS2 with the mingw64 toolchain at `C:\msys64` (the linker is pinned to `C:\msys64\mingw64\bin\gcc.exe` in `native/.cargo/config.toml`)
- FFmpeg **8.1** development files (headers + import libs + runtime DLLs) in `native/.third_party/ffmpeg/`. Use the [BtbN](https://github.com/BtbN/FFmpeg-Builds/releases) **LGPL shared** win64 build for release 8.1 — the DLL majors must be avcodec **62** / avutil **60** (see Runtime deployment). `FFMPEG_DIR` already points there via `native/.cargo/config.toml`.

Then:

```bash
cd native
./build.sh
```

`build.sh` builds the workspace for `x86_64-pc-windows-gnu` and deploys `uuav.dll`, `uuav-helper.exe` and the four FFmpeg runtime DLLs the helper links into `Packages/UUAV/Runtime/Plugins/x86_64/` — the runtime DLLs come from the same `.third_party/ffmpeg` the build linked against, so the majors can never drift apart.

### macOS (universal: Apple Silicon + Intel)

The binaries are universal (arm64 + x86_64), but the **build host** must be Apple Silicon - the x86_64 slice is cross-compiled from arm64.

Prerequisites:

- Xcode (or the Command Line Tools) for clang, Metal frameworks and `codesign`
- Rust toolchain with both targets: `rustup target add aarch64-apple-darwin x86_64-apple-darwin`
- `brew install nasm` (nasm assembles the x86_64 SIMD kernels)

FFmpeg is built from source once:

```bash
cd native
./scripts/build-ffmpeg-macos.sh   # clones FFmpeg n8.1, LGPL shared universal build into .third_party/ffmpeg
./build.sh
```

All scripts produce fat binaries merged with `lipo`. `build.sh` copies `libuuav.dylib`, `uuav-helper` and the seven FFmpeg dylibs into `Packages/UUAV/Runtime/Plugins/macOS/`, ad-hoc code-signs everything (mandatory on arm64) and ends by running `doctor-libs.sh`, which fails the build if any deployed dylib is not universal.

Dylib loading needs no `install_name_tool` post-processing: FFmpeg is configured with `--install-name-dir='@rpath'`, and both `libuuav.dylib` and `uuav-helper` carry an `LC_RPATH @loader_path` entry (set in `native/.cargo/config.toml`), so the whole set resolves from the plugin folder - in the editor and in a built player (`Contents/PlugIns/`). Verify with `otool -L`.

## Runtime deployment

Everything ships in one folder — `Packages/UUAV/Runtime/Plugins/x86_64/` in-project, mirrored into `<Game>_Data/Plugins/x86_64/` in a built player. `uuav-helper.exe` is not a Unity plugin, so `HelperBuildPostprocessor` (in `Packages/UUAV/Editor/`) copies it into builds; CI signs it alongside the game executable. The Windows set:

```
uuav.dll            # the client Unity loads (C ABI)
uuav-helper.exe     # the decode process, spawned by uuav.dll from its own folder
avcodec-62.dll      # FFmpeg 8.1 runtime, linked by the helper
avformat-62.dll
avutil-60.dll
swresample-6.dll
```

Notes:

- The FFmpeg majors (avcodec **62**, avformat **62**, avutil **60**, swresample **6**) are tied to the FFmpeg 8.1 release and must match what the helper linked — `build.sh` deploys them from `.third_party/ffmpeg/bin` for exactly that reason. A different FFmpeg line ships differently-named DLLs that the helper won't load; keep `.third_party/ffmpeg` and `ffmpeg-sys-next = "8.1.0"` in `native/Cargo.toml` in lockstep.
- avfilter/avdevice/swscale and the old support-DLL set (bz2/iconv/lzma/zlib/winpthread) are gone on purpose: the helper's import closure is exactly the four DLLs above (NV12-only pipeline), the BtbN shared build links its support libs statically, and both Rust binaries are self-contained (IPC uses OS primitives — no transport library ships at all). If a DLL is ever missing on a user machine the helper dies at load — watch for the "uuav helper terminated" error with a loader exit code.
- FFmpeg is used under LGPL as a dynamically-linked shared library; see the license shipped with the BtbN build.

On macOS the equivalent set lives in `Packages/UUAV/Runtime/Plugins/macOS/` (deployed by `build.sh`; majors from the FFmpeg n8.1 source build):

```
libuuav.dylib
uuav-helper
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
- The Seatbelt profile (`native/uuav-server/helper.sb`) is embedded into `uuav-helper` at compile time - nothing extra ships, and sandboxing needs no entitlements, so ad-hoc signing stays sufficient.
- CI does not notarize; the helper inherits the ad-hoc signing from `build.sh`. If notarization lands later, add `uuav-helper` to the signed inventory.

## CI verification of the shipped binaries

The committed binaries are pinned by a hash lock, `scripts/uuav/uuav-binaries.lock.json`, and two workflows enforce it:

- **`uuav-verify.yml`** runs on every PR touching this plugin or `scripts/uuav/`. It re-hashes every shipped binary and every build input that feeds one (Rust source trees, manifests, `Cargo.lock` closure of the shipping roots, `.cargo/config.toml`, the FFmpeg builder script) against the lock, reads the FFmpeg configure line embedded in each shipped library and compares it with the recorded provenance, and requires new native binaries to be stored in Git LFS. It also `cargo check`/`test`/`clippy`s the FFmpeg-free crates (`uuav-ipc`, `uuav-client`) on a macOS runner and asserts nothing links `uuav-client`'s rlib.
- **`uuav-native.yml`** runs on demand (`workflow_dispatch`, or the `build-uuav-native` PR label). It rebuilds both targets from pinned inputs — macOS FFmpeg from the source commit in the lock, Windows FFmpeg fetched as the hash-pinned BtbN release asset via `scripts/uuav/fetch-ffmpeg-windows.sh` — then runs two gates: Gate A (`scripts/uuav/repro-gate.sh`) builds twice from two source trees through one canonical path (`scripts/uuav/build-canonical.sh`) and requires byte-identical cargo artifacts; Gate B (`scripts/uuav/reproduces-lock.py`) compares the fresh build against the committed hashes, hard-failing only when the runner's toolchain matches the one pinned in the lock.

### Relocking after a deliberate rebuild

`uuav-native.yml` does not relock — it has `contents: read` and only ever reads the lock. It hands back, per target, the rebuilt binaries and the `toolchain-<os>.txt` its "Record the toolchain actually used" step wrote. Relocking is the human step that commits both:

1. Commit the rebuilt binaries from that run's artifact into `Packages/UUAV/Runtime/Plugins/<macOS|x86_64>/`. On macOS the FFmpeg dylibs are rebuilt too; committing only the two cargo-produced files is fine, and `--update` re-records the FFmpeg hashes unchanged.
2. Relock that target, passing the toolchain the build recorded:

```
python3 scripts/uuav/verify-binaries.py --update --only macos-universal --toolchain toolchain-macos.txt
```

`--toolchain` is what makes the relock honest about who built the bytes. Without it, `--update` refuses on any host whose pinned components differ from `targets.<t>.rust.toolchain` — including the host that just bumped `rust-toolchain.toml`, which by definition no longer matches the pin the previous binaries carry. For a target with no pin yet, the same flag gives it its first one, taken from the recorded components the script can probe again later (`ld` is recorded and deliberately not pinned; nothing can re-probe it).

`--update` rewrites only machine-derived fields. It re-derives `crate_version` and `repo_commit` as a description of the relock — nothing compares them — and it never touches the upstream pins (FFmpeg tag/commit, the BtbN release asset and its sha256) or a build input's `pending` key: both are deliberate human moves. A relock that first ships a binary built from a `pending` input is the commit that deletes that key, which is what the script's closing "still PENDING" line is asking for.

Two things about the round trip are easy to get wrong:

- **Gate B needs a second run.** The round that produces the binaries compares them against the lock as it stands *before* the relock, so it still skips. It becomes a real byte comparison on the next run, after the relocked lock and the new binaries are pushed — the label stays live across pushes, so that run happens on its own.
- **`native/` must not move between the build and the relock.** The binaries come from the commit CI built; `source_digest` is taken from the working tree at relock time. One more source edit in between and the two disagree again.

The lock digests these files as raw bytes, so `.gitattributes` forces `eol=lf` across the native tree. Without that a checkout with `core.autocrlf=true` would relock to digests no other host reproduces.

## GPU device separation

Unity's device and the decode device are different devices in different processes. The helper creates its own D3D11 device / `MTLDevice` on the same adapter as Unity (that's why the client sends the adapter LUID / registry id at init — shared textures cannot cross adapters) and hands the core a probe texture from it, exactly the way Unity used to in-process; the core is unchanged and still enables `SetMultithreadProtected` on its immediate context so FFmpeg's decoder threads coexist with the helper's video pump.

Cross-process frame hand-off is synchronized by the sharing primitives themselves, not by CPU waits: on Windows every slot is a keyed-mutex NV12 texture (`AcquireSync`/`ReleaseSync` order the helper's writes against the client's reads across the two devices); on macOS the helper's blit into the shared IOSurfaces completes (`waitUntilCompleted`) before the slot is published, and the client's own presentation blit orders against Unity's queue the same way the in-process plugin did. A 3-slot rotation per player keeps the two sides from ever reading and writing the same slot; a slot briefly held by the other side just skips that frame instead of blocking.

The costs of the extra hop are one GPU-GPU copy per frame (~3 MB at 1080p NV12, negligible next to GPU bandwidth), ≤20 ms of staleness on cached state getters, and ~10 ms + jitter-buffer latency on audio — all inside the pipeline's existing tolerances. In exchange, FFmpeg's device usage, threads and crashes are fully isolated from the engine.
