# UUAV — native media for the Explorer

UUAV is the Explorer's media stack. It decodes the video and audio that scenes
ask for, replacing AVPro on the desktop platforms the client ships to.

Scenes are not trusted, and neither are the URLs they name. Decoding means
running container and codec parsers over bytes an attacker chose, so UUAV has
been built on one assumption: that parser will eventually be wrong. Everything
below follows from deciding where it is standing when that happens.

It is not standing in the client. Decode runs in a separate, sandboxed helper
process. A parser bug there costs a black video quad, and the client respawns
the helper and restores every player without a restart.

## Shape

Unity binds a small C ABI exported by `uuav.dll` / `libuuav.dylib` — the
client library (`native/uuav-client`). Behind it, the client marshals calls
over an IPC channel to `uuav-helper` (`native/uuav-server`), a child process
that links the media core (`native/src`, the `uuav-core` crate) and drives
FFmpeg. Decoded video never travels as pixels: the helper publishes GPU
surfaces the client adopts directly, so the frame path stays on the GPU.

The helper fetches its own media: FFmpeg's ordinary `http`/`https` protocols
run inside it, gated by the protocol whitelist the client passes at init. TLS
terminates in the helper against the platform stack (SecureTransport on macOS,
Schannel in the Windows FFmpeg build). The sandbox therefore allows outbound
network and is deny-default about most everything else.

## Platforms

macOS ships a universal build and delivers frames as IOSurfaces over mach
ports, with a Seatbelt profile (`native/uuav-server/helper.sb`, embedded at
compile time) confining the helper. Windows ships x86_64 and delivers frames
as shared D3D11 textures with keyed mutexes over a named pipe, with the helper
confined by a restricted token, low integrity, a job object and
process-creation mitigations.

FFmpeg 8.1 comes from two provenances: macOS builds it from the pinned source
tag (`native/scripts/build-ffmpeg-macos.sh`), Windows consumes a hash-pinned
BtbN LGPL shared release asset (`scripts/uuav/fetch-ffmpeg-windows.sh`). The
compiled binaries are committed to Git LFS and pinned to their inputs by
`scripts/uuav/uuav-binaries.lock.json`; `uuav-verify.yml` re-asserts the pins
on every PR, and `uuav-native.yml` is the on-demand rebuild (label
`build-uuav-native`): when run, it rebuilds both targets from the pinned
inputs and fails unless two builds produce identical bytes. See the CI
section of
[`Explorer/Assets/Plugins/UUAV/README.md`](../Explorer/Assets/Plugins/UUAV/README.md)
for the relock workflow.

## Design decisions, and paths not taken

**The helper drives the existing media core; it does not reimplement decode.**
Rewriting decode for a sandboxed process would have meant maintaining two
decoders and reconciling their behaviour forever. The helper links the same
core that used to run in-process and drives it through the same API, so there
is one implementation of playback and the sandbox is a deployment property
rather than a fork.

**The transport is mach ports and named pipes, not sockets or a message
library.** Handle-passing OS primitives keep the channel private to the
parent/child pair, carry the things a byte stream cannot (IOSurface port
rights on macOS, duplicated texture handles on Windows), and ship no
third-party transport library at all.

**Binaries are committed rather than built on demand.** Contributors and CI do
not need a Rust and FFmpeg build environment to build the Explorer, and
everyone runs the same bytes. Those bytes are pinned to their inputs so they
cannot quietly diverge from the sources in the tree. The pin is only worth as
much as the build is repeatable, so the on-demand build workflow is gated on
repeatability: when it runs, two builds from two source trees through one
canonical path must produce identical bytes before its comparison against the
lock means anything.

**The exports live in the client library, and nothing else may link it.**
`uuav-client` ships the whole `uuav_*` C ABI; the helper links the core as a
plain rlib. Anything else linking the client's rlib would carry a second copy
of every export, and that collision does not reliably announce itself — it can
link cleanly and bind the wrong implementation. CI asserts no package depends
on `uuav-client` (its rlib exists only so `examples/smoke.rs` can drive the
same code headlessly).

**The helper spawns at init and is resurrected on crash.** A recovery worker
in the client watches the connection, respawns the helper and restores every
player's state — URL, position, rate, looping — so a decoder crash degrades to
a stutter rather than a session loss.

**The AVPro-shaped surface belongs once, on the Explorer side — today it
exists twice.** Scene code talks to an AVPro-shaped player, and a runtime
switch picks the backend behind it (`Explorer/Assets/DCL/AvProSwitch/`:
`MediaPlayer`, `AvProBackend`, `UuavBackend`). The UUAV package still carries
a second copy of that shape (`Packages/UUAV/AVProCompat/`), and `UuavBackend`
currently drives it, so the UUAV path runs two facades deep and the enums are
duplicated. A facade per backend reads as symmetry but buys nothing: it
duplicates every enum, adds a conversion at each boundary, and gives a bug two
places to hide — which is why the package-side copy is slated to be collapsed
into `UuavBackend`, leaving the shape defined once next to the switch.

## Where to look

The Unity-facing package, the media core, the sandboxed helper and the
transport all live under `Explorer/Assets/Plugins/UUAV/` — the package README
there covers building, runtime deployment and the CI verification of the
shipped binaries. The lock and its tooling live under `scripts/uuav/`. The
headless smoke harness is `native/uuav-client/examples/smoke.rs`.
