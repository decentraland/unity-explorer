# UUAV — architecture and roads not taken

UUAV moves FFmpeg out of the process that holds wallet and session state and
delivers decoded frames back with no pixel copy over the IPC channel. This is the
one design doc: what the shape is, and for each real fork, what we rejected and
why. It supersedes the round-1 plans, gap lists, and per-platform transport notes.

## The shape

A sandboxed child (`uuav-adapter`) decodes untrusted media via FFmpeg and writes
NV12 frames into shared memory. The host half (`uuav-ipc`, in Unity's process,
exposed to C# through `uuav-client`'s `uuav_*` C ABI) presents them. One child
**per active video stream**, each with its own segment, control channel, and GPU
device. `present_core` is the platform-agnostic presenter shared by macOS and
Windows; the frozen media core (`native/src`) wraps FFmpeg and is one runtime with
N players.

## Decisions

### Out of process, not in process
The boundary that earns its keep is **decoder ↔ credential-bearing parent**:
FFmpeg parsing hostile bytes must not be able to read wallet/session state. That
is the whole reason the child is sandboxed. Stream-to-stream isolation is
second-order. The control ABI is a tiny attack surface next to the media bytes
themselves, which is what the sandbox exists to contain.

### One process per stream, not a single shared child
Rejected consolidating N streams into one multi-session child. Measured on v16:
per adapter ~120 MB (fixed ~51 MB: process + D3D11 device + FFmpeg runtime;
per-stream ~70 MB: decode contexts + audio ring, paid N times regardless). Only
~30–35 MB/adapter actually amortizes, so a shared child saves ~18–22% (~65–130 MB)
at the 3–5 concurrent decoders the explorer *already* caps to
(`VideoPrioritizationSettings.maximumSimultaneousVideos = 5`, plus it pauses
videos below 6.6 % screen or beyond 80 m — so no process exists for an off-screen
`VideoPlayer`). Against that modest, bounded win: a shared child needs frozen-core
changes — a device-wide decode-serialization lock (one D3D11 device across
concurrent D3D11VA decode/present brackets can corrupt frames or trigger
DEVICE_REMOVED; `SetMultithreadProtected` guards single calls only) and
per-session fetch-plane routing (`fetch.rs`/`uuav_protocol.c` are process-global,
no session key → cross-session media corruption). Not worth the correctness risk.
If memory ever matters, the lever is lowering `maximumSimultaneousVideos`, not a
refactor.

### A/V sync: a free-running transport-minus-latency clock
Video is scheduled against `now = transport − L`, where `L` is a held EMA of
`transport − audio_played` (the audio playback latency, ~200 ms of jitter buffer).
- Rejected the **bare transport clock**: robust 30 fps but video presents at
  production time while audio plays ~200 ms later → video leads audio.
- Rejected slaving `now` to the **raw audio-playback clock**: it froze video
  (presented 0/s) — the consume-derived clock stalls on any audio underrun and
  video slaved to it freezes (the deadlock trap; never slave decode or
  presentation to a consume clock).
`transport − L` rides the free-running transport clock (never freezes) but is
offset back by the measured latency so each frame lands when its audio plays.
v16-validated: 30 fps, 0 late drops, ~15 ms A/V error.

### The presenter is twin-merged
`present_core::poll_core` is one wait-free implementation for both platforms. The
only real deltas are supplied as closures/flags: `geometry(slot)`, `planes(slot)`,
and `tolerate_zero_state` (Windows tolerates a transport-corrupt `state==0` before
the first snapshot; macOS faults — likely a macOS drift bug to revisit).

### cdylib core → rlib, `uuav-client` is the shipped cdylib
The C-ABI seam that lets the backend swap between the out-of-process (IPC) path
and an in-process one, transparently to C#, lives at **`uuav-client`'s `uuav_*`
exports** — still a cdylib. So making the core (`uuav`) an rlib costs nothing
there; the seam just moved down a crate.

### The export surface is shared memory, not RPC
"Every export is a round trip and attack surface" does not hold: the transport is
shared memory. Queries (`state`, `current_time`, `rate`, `size`) are reads off the
child's published `TransportSnapshot` — served in-parent, no crossing; the master
owns the presentation clock, so `current_time` is already host-side. Commands
(`play`/`seek`/`open`…) are fire-and-forget cell writes. Only the open handshake
genuinely round-trips.

### Slot pool must cover ring + retained + copy-in-flight
`SLOTS_PER_GENERATION ≥ VIDEO_RING_CAPACITY + RETAINED_FRAMES + 1` (now a
`const _` static assert). Below it the adapter starves waiting for the host to
recycle a slot and presentation collapses to the recycle rate — the ~7 fps
theatre stall (6 slots − 4 retained left the ring stuck at ~2 of 8).

## Transport / sandbox (Windows; the macOS increment is the mach sibling)

| Fork | Chosen | Rejected — why |
|---|---|---|
| Control transport | Message-mode named pipe, overlapped, bounded waits | ALPC — undocumented, unstable message layout; a plugin can't ship against it |
| Pipe name | Host creates both ends; child inherits one | Child opening `\\.\pipe\<name>` forces widening the DACL to the very principal meant to have no access |
| Handle duplication | Host pulls with `DuplicateHandle(child,…)` — privilege flows one way | Granting the child `PROCESS_DUP_HANDLE` on its parent is a full escape |
| Incoming handle | Shape-checked + duplicated with a narrow per-kind access mask | `DUPLICATE_SAME_ACCESS` from a process assumed compromised |
| Shared memory | Anonymous `CreateFileMapping(INVALID_HANDLE_VALUE,…)`, handle duplicated in | Named section reintroduces the squat/unlink race |
| GPU delivery | `CreateSharedHandle` NT handle + `OpenSharedResource1` | Legacy shared handle (no lifetime/security); CPU round trip. Cross-device is mandatory: the child can't use the engine's device |
| Cross-process sync | Keyed mutex, two keys, zero timeout on the render thread | Shared fence needs D3D11.4; blocking the render thread. Zero timeout → a contended surface is a skipped frame |
| Adapter | Child enumerates the host's exact LUID or fails | Default adapter — wrong GPU on a hybrid laptop yields an unopenable handle |
| Sandbox | Restricted low-integrity token (GPU-process-grade), nameless kill-on-close job | AppContainer — designed, deliberately not implemented (needs a persistent profile + ACL writes; a half-built path won't start) |
| ACG + win32k lockdown | Software-decode mode only | On in GPU mode: the D3D UMD JITs shaders and reaches win32k for adapter enumeration — it can't live under them |
| Code Integrity Guard | Off, permanently | Admits only MS-signed images; our Rust helper + FFmpeg DLLs + the vendor D3D UMD are not — the process couldn't load its own code |

**Network egress is deliberately unresolved.** The restricted-token helper *can*
open sockets, and nothing denies it. The containment claim is about the host
address space (hostile bytes can't reach wallet/session state), not egress. The
shipped macOS seatbelt actually denies sockets entirely (so network media doesn't
work out-of-process there today) — "match macOS" is ambiguous, and only the
AppContainer path denies egress cleanly. Flagged, not quietly resolved.

## Status
Validated on v16: 30 fps theatre playback, the A/V-sync clock, per-stream memory
cost + the concurrent-decode cap. The Windows sandbox specifics above are REASONED
from documentation and how shipping multi-process browsers solve the same problem;
the crate type-checks and unit-tests for `x86_64-pc-windows-msvc`, and the media
path runs, but the token/mitigation compatibility with the D3D driver is the
highest-value thing still to prove exhaustively on hardware.
