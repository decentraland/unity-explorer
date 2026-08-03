# UUAV — native media for the Explorer

UUAV is the Explorer's media stack. It decodes the video and audio that scenes
ask for, and it has replaced AVPro on every platform the client ships to.

Scenes are not trusted, and neither are the URLs they name. Decoding means
running container and codec parsers over bytes an attacker chose, so UUAV has
been built on one assumption: that parser will eventually be wrong. Everything
below follows from deciding where it is standing when that happens.

It is not standing in the client. Decode has moved into a separate, sandboxed
process that holds no network capability, no filesystem to speak of, and no
session key. A parser bug there costs a black video quad.

## Shape

Unity binds a small C ABI. Behind it, a client library marshals calls across an
IPC channel to a child process, which drives FFmpeg and hands frames back.
Decoded video never travels as pixels: the child publishes GPU surfaces the
client adopts directly, so the frame path stays on the GPU.

The child fetches nothing. Every byte of media is retrieved by Unity, in managed
code, and served to the child over the same channel. TLS is terminated in the
trusted process against the OS trust store; the child's FFmpeg has been built
with no network protocol at all.

The ABI between the client and the media core is frozen and mechanically held
that way, so the core can be treated as a fixed component rather than something
that drifts with each change to the transport around it.

## Platforms

macOS ships a universal build and delivers frames as IOSurfaces over mach ports,
with a seatbelt profile confining the child. Windows ships x86_64 and delivers
frames as shared D3D11 textures over a named pipe, with the child confined by a
restricted token, low integrity, a job object and process-creation mitigations.

Both platforms build FFmpeg from source against an explicit list of the formats
and codecs the Explorer actually plays. The compiled binaries are committed, and
building the same sources again reproduces them byte for byte.

## Design decisions, and paths not taken

**The child drives the existing media core; it does not reimplement decode.**
Rewriting decode for a sandboxed process would have meant maintaining two
decoders and reconciling their behaviour forever. The child links the same core
and drives it through the same C API, so there is one implementation of playback
and the sandbox is a deployment property rather than a fork.

**The transport is mach ports and named pipes, not sockets.** Sockets are the
obvious channel and were rejected deliberately: a socket is a network
capability, and a sandbox that must grant one cannot be deny-default about the
network. Handle-passing primitives let the child be denied networking outright
while still talking to its parent.

**Unity fetches; the child does not.** The alternative was to let the child do
its own HTTP and TLS, which is what a media library normally does. That would
put HTTP framing and TLS record handling — both parsing untrusted input — inside
the sandbox, and would add a native TLS stack to the attack surface. Fetching in
managed code in the trusted process removes both, and gets peer verification
against the OS trust store for free. The cost is an IPC round trip per read,
which streaming absorbs.

**Fetching is intercepted below the core, not inside it.** The core opens media
by URL and has not been taught about a custom I/O path, because teaching it would
mean changing the component the freeze exists to protect. Interception happens
one level down, in the child's own FFmpeg build, under the ordinary URL scheme
names. The core opens the same URLs it always did.

**FFmpeg is built from source rather than taken from a distribution.** A
prebuilt FFmpeg carries every demuxer and decoder its packager chose, and an
unbuilt parser is the only kind that is certainly unreachable. Building it here
also makes the sandboxing and protocol decisions above possible at all.

**Binaries are committed rather than built on demand.** Contributors and CI do
not need a working FFmpeg cross-compilation environment to build the Explorer,
and everyone runs the same bytes. Those bytes are pinned to their inputs so they
cannot quietly diverge from the sources in the tree. The pin is only worth as
much as the build is repeatable, so the build is kept free of anything that
varies between runs: rebuilding unchanged sources has to produce the same bytes,
or a lock failure means nothing and gets cleared by habit.

**The exports live in their own library, separate from the transport.** Having
the transport carry them collides with the core's own copies inside the child,
and that collision does not reliably announce itself — it can link cleanly and
bind the wrong implementation. Keeping them apart makes the arrangement fail
loudly or not at all.

**Spawning is deferred to the first player, not done at load.** Paying it at
load would move the cost rather than remove it, and would charge it to clients
that never play anything.

**The AVPro-shaped surface exists once, on the Explorer side.** Scene code still
talks to an AVPro-shaped player, and a runtime switch picks the backend behind
it. That shape is mirrored in the Explorer, next to the switch — not a second
time inside the UUAV package, which presents only its own player. A facade per
backend reads as symmetry but buys nothing: it duplicates every enum, adds a
conversion at each boundary, and gives a bug two places to hide.

## Where to look

The Unity-facing package, the media core, the sandboxed child and the transport
all live under `Explorer/Assets/Plugins/UUAV/`. The media core is the part under
a freeze; the crates around it are where ordinary work happens.

A media sandbox under `test-scenes/` serves representative content locally, over
TLS, for exercising formats end to end without depending on the public internet.
A playback smoke test in the Unity package opens real media and is the fastest
way to find out whether the whole path is intact.
