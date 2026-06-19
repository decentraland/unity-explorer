# Pulse

This page covers the **Pulse transport** — an ENet-based UDP peer transport that runs alongside [LiveKit](livekit-networking.md). Pulse is a second, experimental real-time channel for multiplayer traffic (movement, emotes, profile propagation, teleports). It is gated by a feature flag and ships off by default; when enabled, it runs **concurrently** with LiveKit rather than replacing it.

The transport-neutral pieces (encoding, send system, receive pipeline, interpolation, SDK propagation, remote-player tables) live in **[multiplayer.md](multiplayer.md)** and are shared with LiveKit. This page is specifically about the Pulse transport's internals.

---

## What Pulse is and why

Pulse is a direct peer transport built on ENet (reliable-UDP), distinct from LiveKit's WebRTC-based room model:

- **No rooms** — a single peer connection per client, with peer-ids assigned server-side and mapped to Web3 wallets.
- **Protobuf framing** — every message is a `ClientMessage` or `ServerMessage` envelope wrapping a `oneof` payload (movement, emote, profile, teleport, disconnect, …).
- **Flat addressing** — senders identify themselves by their peer-id; receivers use `PeerIdCache` to translate peer-ids back to wallets.
- **Pool-backed packet buffers** — `PacketPool` + `MessagePacket` keep ENet packet allocation out of the hot path.

Pulse was introduced as a parallel pipe rather than a LiveKit replacement so:

1. Roll-out can be gated behind `FeatureId.PULSE` and flipped on per-user or per-environment without rebuilding.
2. Messages sent over both transports are transparently de-duplicated by downstream consumers (see [multiplayer.md → Core Interfaces](multiplayer.md#core-interfaces) — the proxy implementations union inputs from both transports).
3. LiveKit remains the source of truth for chat, voice, and scene-room semantics; Pulse handles player-state traffic that benefits from a tighter, more direct path.

See [multiplayer.md → Transport Selection & Wiring](multiplayer.md#transport-selection--wiring) for how both containers are composed under `MultiplayerContainer` and how the four proxy implementations fan out calls to each.

---

## Feature flag and program arg

Pulse is toggled by a single logical flag, evaluated by `FeaturesRegistry` as either a launch arg **or** a remote feature flag:

| Source | Symbol | Where |
|---|---|---|
| Program arg | `--pulse` | `AppArgsFlags.PULSE_MULTIPLAYER = "pulse"` |
| Remote flag | `"pulse"` | `FeatureFlagsStrings.PULSE = "pulse"` |
| Registry key | `FeatureId.PULSE` | Final boolean exposed to code |

From `DCL/FeatureFlags/FeaturesRegistry.cs`:

```csharp
[FeatureId.PULSE] = appArgs.HasFlag(AppArgsFlags.PULSE_MULTIPLAYER)
                 || featureFlags.IsEnabled(FeatureFlagsStrings.PULSE),
```

`OR` semantics: passing `--pulse` at launch enables it regardless of the remote flag, useful for local development and integration testing.

### Where the flag is consumed

- **`PulseContainer`** — reads `FeaturesRegistry.Instance.IsEnabled(FeatureId.PULSE)` in its constructor and stores it as `FeatureEnabled`. During `InitializeInternalAsync`:
  - `pulseMultiplayerService` is either a real `PulseMultiplayerService(transport, messagePipe, identityCache, urlsSource)` or `new IPulseMultiplayerService.Dummy()`.
  - `pulseProfilePropagationBus` is either a real `PulseProfilePropagationBus(...)` or `new IProfilePropagation.Dummy()`.
  - The `PulseMultiplayerBus`, `PulseIncomingProfileAnnouncements`, `PulseRemoveIntentions`, and `ENetTransport` are **always** constructed; the flag only swaps the Service and ProfilePropagation paths. The bus and incoming collectors simply never receive traffic when the service is a dummy.
- **`LiveKitMultiplayerContainer`** — reads the same flag and passes `backwardCompatibilityMode = true` to `LiveKitMessagesBroadcaster`. This adjusts how LiveKit serializes certain control messages so Pulse-enabled peers interoperate with LiveKit-only peers without double-delivery or format drift.

### Disabling behavior

When the flag is off:

- All four proxies in `MultiplayerContainer` still fan out to Pulse, but the Pulse side is a no-op — `PulseMultiplayerBus` methods early-return against `pulseMultiplayerService.Dummy`.
- Incoming proxies' `Fill(...)` / `Bunch()` calls include empty Pulse lists.
- `IProfilePropagation.Dummy` makes the main self-profile-propagation call a no-op (the profile still propagates over LiveKit via `IProfileBroadcast`).
- `LiveKitMessagesBroadcaster.backwardCompatibilityMode = false` — LiveKit runs in its original serialization mode.

The effective runtime cost of Pulse when disabled is a few dummy virtual calls per frame.

---

## Transport layer

### `ITransport` — the abstraction

`Connections/Pulse/ITransport.cs` is the interface the rest of Pulse uses. It is intentionally narrow so the ENet implementation can be swapped for tests or alternate backends:

```csharp
public interface ITransport : IDisposable
{
    TransportState State { get; }

    long BytesSent, BytesReceived;
    long PacketsSent, PacketsReceived;

    UniTask ConnectAsync(string address, int port, CancellationToken ct);
    void   Disconnect(DisconnectReason reason);
    void   Send(IMessage message, PacketMode mode);

    public enum TransportState
    {
        NONE, CONNECTING, CONNECTED, DISCONNECTING, DISCONNECTED,
    }
}
```

`PacketMode` and `DisconnectReason` come from the external `Pulse.Transport` UPM package (`com.decentraland.pulse.transport`) — the same enums the server uses.

### `ENetTransport` — the implementation

`Connections/Pulse/ENet/ENetTransport.cs` wraps the managed ENet binding and drives the socket on a dedicated worker.

**Single-peer topology** — `client.Create(peerLimit: 1, channelLimit: ENetChannel.COUNT)` and `serverPeer = client.Connect(...)`. Pulse is strictly client↔server; there is no peer mesh. The `serverPeer` nullable field tracks the one remote.

**Lifecycle** — `ConnectAsync(ip, port, ct)`:
1. Initialize the ENet native library lazily (guarded by `static volatile bool isLibInitialized`). Throws `InvalidOperationException` if `Library.Initialize()` fails.
2. Allocate a new `Host`, resolve the address, `client.Connect(...)`.
3. Spawn the receive/send loop via `UniTask.RunOnThreadPool(...)`.
4. `UniTask.WaitUntil(State == CONNECTED).Timeout(ConnectTimeoutMs)` — surfaces a `TimeoutException` if the handshake stalls, and cancels the loop on timeout.

`TransportState` is computed by mapping ENet's `PeerState` to the five `TransportState` values: `Connecting` / `ConnectionPending` / `AcknowledgingConnect` → `CONNECTING`; `Connected` / `ConnectionSucceeded` → `CONNECTED`; the two disconnect states → `DISCONNECTING` / `DISCONNECTED`.

### The service loop

ENet is not thread-safe — all socket I/O must happen on a single thread. `ListenForIncomingDataAsync` enforces this:

```csharp
return UniTask.RunOnThreadPool(async () =>
{
    while (!ct.IsCancellationRequested)
    {
        if (client == null) continue;

        // Service does socket I/O + returns one event. Short timeout so we never block outgoing flushes.
        if (client.Service(options.ServiceTimeoutMs, out Event netEvent) > 0)
            ReceiveIncomingMessage(in netEvent);

        // Service only returns one event per call. If multiple packets arrived in that I/O pass,
        // the rest are queued internally. CheckEvents drains them without redundant socket I/O.
        while (client.CheckEvents(out netEvent) > 0)
            ReceiveIncomingMessage(in netEvent);

        SendOutgoingMessages();
        await Task.Yield();
    }

    client?.Flush();
    client?.Dispose();
    client = null;
}, configureAwait: false, cancellationToken: ct);
```

Two nuances:
- **`Service` vs. `CheckEvents`** — `Service(timeoutMs)` performs socket I/O and returns **at most one** event. Remaining queued events from that I/O pass are drained in the tight `CheckEvents` inner loop. This avoids redundant socket syscalls when a burst of packets arrives together.
- **Outgoing on the same thread** — `SendOutgoingMessages` drains the `MessagePipe` output queue on the service thread, because `Send` must run on the thread that owns the socket.

`ServiceTimeoutMs` defaults to **1 ms** in `ENetTransportOptions` so the loop is effectively a tight spin — optimized for low latency over CPU efficiency. `BufferSize` defaults to **4096** bytes; both send and receive buffers are pre-allocated and reused.

### Channels and packet modes

Pulse opens `ENetChannel.COUNT = 3` channels on connect — see `Pulse.Transport.ENetChannel`:

| Channel | `PacketMode` | ENet flags | Used for |
|---|---|---|---|
| 0 `RELIABLE` | `RELIABLE` | `PacketFlags.Reliable` | Handshakes, teleports, profile propagation, disconnects — anything that must arrive. |
| 1 `UNRELIABLE_SEQUENCED` | `UNRELIABLE_SEQUENCED` | `PacketFlags.Unthrottled` | High-frequency `STATE_DELTA` (movement) fan-out. |
| 2 `UNRELIABLE_UNSEQUENCED` | `UNRELIABLE_UNSEQUENCED` | `PacketFlags.Unsequenced` | One-shot events where ordering doesn't matter. |

The `UNRELIABLE_SEQUENCED` channel intentionally uses `PacketFlags.Unthrottled` (not just `PacketFlags.Unreliable`). The package's own doc comment explains:

> The `Unthrottled` flag disables ENet's send-side throttle. Without it, ENet silently destroys unreliable packets before they reach the wire when `packetThrottle` drops below 32 — which happens when measured RTT exceeds `lastRTT + 40 ms + 2 × variance`. Dropping a STATE_DELTA is strictly worse than sending it: the client detects a sequence gap and issues a RESYNC_REQUEST, which costs a full reliable round-trip.

In plain terms: for movement deltas, losing the packet on the wire (and resyncing on a gap) is cheaper than having ENet silently drop it and potentially hold back subsequent packets on the same channel. Application-level rate control (the adaptive `sendRate` in [`PlayerMovementNetSendSystem`](multiplayer.md#send-system--adaptive-rate)) already governs send frequency, so the transport-level throttle is redundant and harmful.

> **Warning:** Reliable packets on a channel block sequenced-unreliable packets on the **same** channel — head-of-line blocking. That's why reliable traffic (channel 0) and `STATE_DELTA` (channel 1) are kept on separate channels.

### Incoming event handling

`ReceiveIncomingMessage(in Event)` switches on `netEvent.Type`:

- **`Connect`** — latch `serverPeer = netEvent.Peer`. This is the moment `State` transitions to `CONNECTED`.
- **`Disconnect`** — clear `serverPeer`, call `messagePipe.OnDisconnected((DisconnectReason)netEvent.Data)`, cancel the lifecycle CTS. `netEvent.Data` is an `uint` set by the remote when it called `Disconnect(reason)`; it's cast into the shared `DisconnectReason` enum (`GRACEFUL`, `AUTH_TIMEOUT`, `AUTH_FAILED`, `DUPLICATE_SESSION`, `KICKED`, `SERVER_FULL`).
- **`Timeout`** — same as `Disconnect` but reports `DisconnectReason.NONE` — the remote never got a chance to tell us why, we just stopped hearing from it.
- **`Receive`** — copy the packet payload into the pre-allocated `receiveBuffer`, bump counters, dispatch through `messagePipe.OnDataReceived(new MessagePacket(span, peerId))`. The ENet `Packet` is disposed via the `using` pattern to release unmanaged memory back to ENet.

### Outgoing send path

`Send(IMessage message, PacketMode mode)` is called from any thread via `MessagePipe`. The message is **enqueued** into the pipe's outbox; actual socket writes happen on the service thread inside `SendOutgoingMessages`:

```csharp
private void SendOutgoingMessages()
{
    while (messagePipe.TryReadOutgoingMessage(out OutgoingMessage msg))
    {
        ENetChannel channel = ToENetChannel(msg.PacketMode);
        using OutgoingMessage _ = msg;

        if (serverPeer != null)
            SendToPeer(serverPeer.Value, channel, msg.Message);
    }
}

private void SendToPeer(Peer peer, ENetChannel channel, IMessage message)
{
    int size = message.CalculateSize();
    var span = new Span<byte>(sendBuffer, 0, size);
    message.WriteTo(span);
    BytesSent += size;
    PacketsSent++;
    var packet = default(Packet);
    packet.Create(span, channel.PacketMode);
    peer.Send(channel.ChannelId, ref packet);
}
```

Two details worth calling out:

- **Zero-copy serialize** — the Protobuf message is serialized directly into the pre-allocated `sendBuffer`, then ENet copies from that span into its own packet. No per-send heap allocation for the payload.
- **`using OutgoingMessage _`** — the message envelope is pooled; the `using` returns it to `PacketPool` after send, regardless of success.

### Disconnect and dispose

`Disconnect(DisconnectReason reason)` calls `serverPeer?.Disconnect((uint)reason)` — the reason round-trips to the server through ENet's disconnect-data field.

`Dispose()`:
1. Clears `serverPeer` and cancels the lifecycle CTS.
2. Spins (`Thread.Sleep(10)`) until the service loop observes the cancellation and nulls out `client` itself (from inside the same thread — satisfying ENet's threading requirement).
3. Calls `Library.Deinitialize()` and flips `isLibInitialized = false`.

The spin is ugly but necessary: `Host.Dispose()` must be called on the thread that created it, and there's no other safe place to wait for that.

### `ENetTransportOptions`

A plain `ScriptableObject` / serializable config with three fields:

| Field | Default | Purpose |
|---|---|---|
| `ConnectTimeoutMs` | 10 000 | Upper bound on `ConnectAsync` before it throws `TimeoutException`. |
| `ServiceTimeoutMs` | 1 | Per-iteration `Host.Service` timeout. Low values trade CPU for latency. |
| `BufferSize` | 4 096 | Size of the fixed send/receive byte buffers. Must exceed the largest expected Protobuf payload. |

Serialized into `PulseContainer.Settings` so the options can be tuned per build without code changes.

---

## ENet native libraries — build provenance

The native ENet libraries that back the managed `ENet.cs` P/Invoke wrapper (shipped by the `com.decentraland.pulse.transport` package) live in the repo at `Explorer/Assets/Plugins/ENet/runtimes/`. The bindings expect the native symbol library to resolve to the name `enet` (Windows) / `libenet` (macOS, Linux) — see the `[DllImport]` declarations in that package's `Runtime/ENet.cs`.

Keep this section in sync whenever any binary under `runtimes/` is rebuilt or swapped, so the exact source and flags behind each slice stay auditable.

**Upstream source:** https://github.com/SoftwareGuy/ENet-CSharp

### Building the macOS libraries

All commands run from the **upstream repository root**.

> **Note (CMake 4.x):** Homebrew's CMake 4.x removed support for the `cmake_minimum_required(VERSION 3.1)` this project declares. Pass `-DCMAKE_POLICY_VERSION_MINIMUM=3.5` to configure anyway (shown below). It does not affect the output.

**Universal binary (arm64 + x86_64 in one file) — via CMake:**

```bash
cmake -S . -B build-mac \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_OSX_ARCHITECTURES="arm64;x86_64" \
  -DCMAKE_POLICY_VERSION_MINIMUM=3.5
cmake --build build-mac --config Release
```

**Universal binary — directly with clang (no CMake):**

```bash
clang -arch arm64 -arch x86_64 -O2 -dynamiclib \
  -DENET_NO_PRAGMA_LINK -DENET_DLL \
  -ISource/Native Source/Native/enet.c \
  -o libenet.dylib
```

**Separate single-architecture binaries — directly with clang** (this is how the two macOS slices currently in `runtimes/` were produced):

```bash
# x86_64 only
clang -arch x86_64 -O2 -dynamiclib \
  -DENET_NO_PRAGMA_LINK -DENET_DLL \
  -ISource/Native Source/Native/enet.c \
  -o libenet-x86_64.dylib

# arm64 only
clang -arch arm64 -O2 -dynamiclib \
  -DENET_NO_PRAGMA_LINK -DENET_DLL \
  -ISource/Native Source/Native/enet.c \
  -o libenet-arm64.dylib
```

Check any resulting binary with `file libenet.dylib` or `lipo -archs libenet.dylib`.

### Windows & Linux libraries

The Windows (`runtimes/win-x64/native/enet.dll`) and Linux (`runtimes/linux-x64/native/libenet.so`) binaries were not built locally — they were taken directly from the upstream prebuilt release: https://github.com/SoftwareGuy/ENet-CSharp/releases/tag/autobuild-14c4dc5

---

## Service and message model

One abstraction up from `ITransport`: `PulseMultiplayerService` owns the connection lifecycle (including the authentication handshake), routes incoming messages to registered handlers, and exposes a Send API. Everything above (`PulseMultiplayerBus`, `PulseProfilePropagationBus`) consumes this service through the `IPulseMultiplayerService` interface.

### `IPulseMultiplayerService`

```csharp
public interface IPulseMultiplayerService : IDisposable
{
    public bool IsAuthenticated { get; }

    public void Dispose();
    public void RegisterSyncHandler(ServerMessage.MessageOneofCase type, Action<IncomingMessage> handler);
    public void RegisterDisconnectHandler(Func<DisconnectReason, bool> handler);
    public void UnregisterAllHandlers();

    public UniTask ConnectAsync(CancellationToken ct);
    public void    Disconnect();
    public void    Send(OutgoingMessage outgoingMessage);

    public class Dummy : IPulseMultiplayerService { /* no-op everything; disposes OutgoingMessage in Send */ }
}
```

The `Dummy` is the feature-flag-off path — it returns immediately from every method and, crucially, disposes any `OutgoingMessage` passed to `Send` so the pool doesn't leak.

### Protobuf envelopes — `ClientMessage` and `ServerMessage`

All Pulse traffic is framed as one of two Protobuf messages with a `oneof` payload, generated into `Decentraland.Pulse`:

**Client → Server (`ClientMessage.MessageOneofCase`):**

| Case | Payload | Typical channel |
|---|---|---|
| `Handshake` | `HandshakeRequest` (auth chain) | `RELIABLE` |
| `Input` | `PlayerStateInput` | `UNRELIABLE_SEQUENCED` |
| `Resync` | `PlayerStateInput` | `RELIABLE` |
| `ProfileAnnouncement` | `PlayerStateInput` (reused proto) | `RELIABLE` |
| `EmoteStart` | `EmoteStart` | `RELIABLE` |
| `EmoteStop` | `EmoteStop` | `RELIABLE` |
| `Teleport` | `TeleportRequest` | `RELIABLE` |

**Server → Client (`ServerMessage.MessageOneofCase`):**

| Case | Payload |
|---|---|
| `Handshake` | `HandshakeResponse` (success, optional error) |
| `PlayerJoined` | `PlayerJoined` |
| `PlayerStateDelta` | `PlayerStateDeltaTier0` |
| `PlayerStateFull` | `PlayerStateFull` |
| `PlayerLeft` | `PlayerLeft` |
| `PlayerProfileVersionAnnounced` | `PlayerProfileVersionsAnnounced` |
| `EmoteStarted` | `EmoteStarted` |
| `EmoteStopped` | `EmoteStopped` |
| `Teleported` | `TeleportPerformed` |

> **Note:** Some client-side cases (`Resync`, `ProfileAnnouncement`) share the `PlayerStateInput` payload type — these are different *actions* framed in the same proto shape. Server-side responses are all distinct message types.

### `OutgoingMessage` and `IncomingMessage` — pooled envelopes

Both are `readonly struct` wrappers that implement `IDisposable`; disposal returns the underlying Protobuf message to the correct per-case pool.

**`OutgoingMessage`:**

```csharp
public readonly struct OutgoingMessage : IDisposable
{
    private static readonly ClientMessagePool POOL = new();
    public readonly ClientMessage Message;
    public readonly PacketMode PacketMode;

    public static OutgoingMessage Create(PacketMode packetMode, ClientMessage.MessageOneofCase kind) =>
        new(POOL.Get(kind), packetMode);

    public void Dispose() => POOL.Release(Message);
}
```

Usage is always `var msg = OutgoingMessage.Create(PacketMode.RELIABLE, ClientMessage.MessageOneofCase.Handshake); msg.Message.Handshake.AuthChain = ...; pulseMultiplayerService.Send(msg);` — the service transfers ownership to the transport, which `using`-disposes after send.

**`IncomingMessage`:**

```csharp
public readonly struct IncomingMessage : IDisposable
{
    private static readonly ServerMessagePool POOL = new();
    public PeerId From { get; }
    public ServerMessage Message { get; }

    public static bool TryCreate(PeerId from, ReadOnlySpan<byte> data, out IncomingMessage message) { ... }
    public void Dispose() => POOL.Release(Message);
}
```

`TryCreate` has an interesting optimization: it extracts the `MessageOneofCase` directly from the **first tag byte** (`data[0] >> 3`) without fully parsing the wire bytes, uses that to get a correctly-typed `ServerMessage` from the pool, then calls `MergeFrom(data)` to populate it. This lets the pool pre-allocate the right sub-message shape for each case instead of allocating on every receive.

> **Warning:** `IncomingMessage.Dispose()` returns the underlying proto to the pool. Handlers must not store references to `Message`, `Message.PlayerStateDelta`, or any sub-message — those references become invalid as soon as the handler returns. Copy anything that needs to outlive the callback.

### `MessagePacket` — the transport-layer wrapper

```csharp
public readonly ref struct MessagePacket
{
    public readonly ReadOnlySpan<byte> Data;
    public readonly PeerId FromPeer;
}
```

A `ref struct` because it carries a `ReadOnlySpan<byte>` pointing at `ENetTransport.receiveBuffer` — it can't escape the stack frame. The service thread constructs it in `ReceiveIncomingMessage` and hands it to `MessagePipe.OnDataReceived`; the pipe parses into an `IncomingMessage` right there (on the service thread) before the span goes out of scope.

### `ClientMessagePool` / `ServerMessagePool` — per-case pooling

`Connections/Pulse/PacketPool.cs` defines two pools, one per envelope direction. Each internally holds a `Dictionary<OneofCase, IObjectPool<TMessage>>`, and `Get(kind) / Release(message)` routes to the right sub-pool by case:

```csharp
private static readonly IReadOnlyDictionary<ClientMessage.MessageOneofCase, IObjectPool<ClientMessage>> POOLS = new Dictionary<...>
{
    [ClientMessage.MessageOneofCase.Handshake]   = CreatePool<HandshakeRequest>((packet, mes) => packet.Handshake = mes),
    [ClientMessage.MessageOneofCase.Input]       = CreatePool<PlayerStateInput>((packet, mes) => packet.Input = mes),
    [ClientMessage.MessageOneofCase.EmoteStart]  = CreatePool<EmoteStart>((packet, mes) => packet.EmoteStart = mes),
    // ...
};
```

The inner `ThreadSafeObjectPool` pre-allocates a fresh sub-message when a new pool entry is created (so `packet.Handshake`, `packet.Input`, etc. are non-null out of the pool) and clears Protobuf fields on release via `packet.GetUnderlyingData()?.ClearProtobufComponent()`.

Release routes by `message.MessageCase` — so if a handler mutates the case, the message can end up in a different sub-pool than it came from. This is normally fine because the case is set at `Create` time and not changed.

### `MessagePipe` — thread boundary and event stream

`Connections/Pulse/MessagePipe.cs` is the producer-consumer queue between the ENet service thread and the Pulse service:

```csharp
public sealed class MessagePipe
{
    private readonly Channel<MessagePipeEvent> eventChannel = Channel.CreateUnbounded<MessagePipeEvent>();
    private readonly Channel<OutgoingMessage>  outgoingChannel = Channel.CreateUnbounded<OutgoingMessage>();

    public IAsyncEnumerable<MessagePipeEvent> ReadEventsAsync(CancellationToken ct) =>
        eventChannel.Reader.ReadAllAsync(ct);

    public bool TryReadOutgoingMessage(out OutgoingMessage message) =>
        outgoingChannel.Reader.TryRead(out message);

    public void Send(OutgoingMessage message) => outgoingChannel.Writer.TryWrite(message);

    public void OnDisconnected(DisconnectReason reason) =>
        eventChannel.Writer.TryWrite(MessagePipeEvent.FromDisconnectEvent(reason));

    public void OnDataReceived(MessagePacket packet)
    {
        if (IncomingMessage.TryCreate(packet.FromPeer, packet.Data, out IncomingMessage incomingMessage))
            eventChannel.Writer.TryWrite(MessagePipeEvent.FromMessage(incomingMessage));
    }
}
```

Two channels (`System.Threading.Channels`), both unbounded:
- **`eventChannel`** — incoming events (`IncomingMessage` or `DisconnectEvent`). Written by the ENet thread; read by the Pulse service's routing loop.
- **`outgoingChannel`** — outgoing messages. Written by Pulse callers (any thread); read by the ENet thread in `SendOutgoingMessages`.

### `MessagePipeEvent` — tagged union

`Connections/Pulse/MessagePipeEvent.cs` uses the `[REnum]` source generator (from the `REnum` package) to emit a tagged-union `readonly partial struct`:

```csharp
[REnum]
[REnumField(typeof(IncomingMessage), "Message")]
[REnumField(typeof(DisconnectEvent))]
public readonly partial struct MessagePipeEvent : IDisposable
{
    public readonly struct DisconnectEvent
    {
        public readonly DisconnectReason Reason;
        // implicit conversions to/from DisconnectReason
    }

    public void Dispose() => Match(static m => m.Dispose(), static _ => { });
}
```

This guarantees disconnects and messages flow through the **same channel** in **wire order**. A handler can rely on "every delta before the disconnect event was observed before the socket closed". Routing the disconnect through a side channel would lose that ordering.

`Dispose()` uses the source-generated `Match` to dispose the `IncomingMessage` variant (returning the proto to the pool) and no-op on the disconnect variant.

### `PulseMultiplayerService` — connection lifecycle

`Connections/Pulse/PulseMultiplayerService.cs` orchestrates:
- Connection with retries (`MAX_CONNECT_ATTEMPTS = 3`).
- Authentication handshake (round-trip of `HandshakeRequest`/`HandshakeResponse`).
- Routing loop (dispatch incoming messages to registered sync handlers).
- Automatic reconnection after disconnect, if the disconnect handler returns `true`.

**Connect** — `ConnectWithRetriesAsync`:

```csharp
for (var attempt = 1; attempt <= MAX_CONNECT_ATTEMPTS; attempt++)
{
    try { await ConnectInternalAsync(ct); return; }
    catch (TimeoutException) when (attempt < MAX_CONNECT_ATTEMPTS)
    {
        ReportHub.LogWarning(ReportCategory.MULTIPLAYER,
            $"Pulse connection attempt {attempt}/{MAX_CONNECT_ATTEMPTS} timed out, retrying...");
    }
}
```

Only `TimeoutException` is caught and retried. Other failures propagate immediately.

**Authentication handshake** — `ConnectInternalAsync`:
1. `transport.ConnectAsync(urlsSource.Url(DecentralandUrl.Pulse), PORT = 7777, ct)` — ENet handshake.
2. Register a one-shot sync handler for `ServerMessage.MessageOneofCase.Handshake` that completes a `UniTaskCompletionSource<(bool, string?)>`.
3. Start the routing loop (`StartRouting`).
4. Build and send a `HandshakeRequest`:
   ```csharp
   var handshakePacket = OutgoingMessage.Create(PacketMode.RELIABLE, ClientMessage.MessageOneofCase.Handshake);
   handshakePacket.Message.Handshake.AuthChain = ByteString.CopyFromUtf8(BuildAuthChain());
   Send(handshakePacket);
   ```
5. Await the handshake response. On failure, `Disconnect()` and throw `PulseException(error)`.

`BuildAuthChain()` signs `connect:/:{timestamp}:{}` with the cached Web3 identity, builds the canonical `x-identity-auth-chain-0/1/...` + `x-identity-timestamp` + `x-identity-metadata` dictionary, and JSON-serializes it. Same auth-chain shape used across the platform, just delivered inside a Protobuf `ByteString` instead of HTTP headers.

**Routing loop** — `StartRouting(connectionCt, parentCt)`:

```csharp
UniTask.RunOnThreadPool(async () =>
{
    await foreach (MessagePipeEvent evt in pipe.ReadEventsAsync(connectionCt))
    {
        if (evt.IsDisconnectEvent(out DisconnectEvent disconnectEvent))
        {
            bool shouldReconnect = disconnectHandler?.Invoke(disconnectEvent) ?? false;
            if (shouldReconnect && !parentCt.IsCancellationRequested)
            {
                ResetConnectionLifecycle();
                await Task.Delay(RECONNECTION_DELAY_MS, parentCt);
                try { await ConnectAsync(parentCt); } catch { /* logged */ }
            }
            break;
        }

        if (!evt.IsMessage(out IncomingMessage message)) continue;
        try { if (syncHandlers.TryGetValue(message.Message.MessageCase, out var handler)) handler(message); }
        finally { evt.Dispose(); }
    }
}, configureAwait: false, cancellationToken: connectionCt).Forget();
```

Important details:
- `RunOnThreadPool` + `configureAwait: false` keep continuations on the thread pool — matches the ENet transport pattern (avoids spurious main-thread hops).
- The comment inside `StartRouting` warns against `UniTask.Delay` here — it schedules on the Unity player loop and would resume on the main thread. `Task.Delay` respects the null `SynchronizationContext` of thread-pool threads.
- Two cancellation tokens: `connectionCt` (per-connection, cancelled on disconnect to stop routing cleanly) and `parentCt` (outer lifecycle; cancelling it stops reconnect attempts).
- `evt.Dispose()` in `finally` returns the proto to the pool even if the handler throws.

**Disconnect handler contract** — `RegisterDisconnectHandler(Func<DisconnectReason, bool>)` — the boolean return decides whether to reconnect. This lets `DUPLICATE_SESSION` stop the loop (another client took over, don't fight it) while transient disconnects ask for a `RECONNECTION_DELAY_MS = 10s` cooldown followed by a fresh `ConnectAsync`. `PulseMultiplayerBus` wires the handler.

### `PulseException`

Trivial — `public class PulseException : Exception { public PulseException(string m) : base(m) {} }`. Thrown by `ConnectInternalAsync` when the handshake fails, with the server-supplied error message (or `"Handshake failed"` if the server didn't include one). No other code path throws it today.

---

## Identity mapping

Pulse frames every message with a `PeerId` (a small server-assigned `uint`). The rest of the stack (movement inbox, emote bus, profile announcements, entity-participant table) works in `Web3Address` / wallet-ID strings. `PeerIdCache` is the translation layer between the two.

### `PeerId`

`Connections/Pulse/PeerId.cs` — a `readonly struct` wrapping a `uint`:

```csharp
public readonly struct PeerId : IEquatable<PeerId>
{
    public readonly uint Value;
    public PeerId(uint value) => Value = value;
    public static implicit operator uint(PeerId id) => id.Value;
    public bool Equals(PeerId other) => Value == other.Value;
    public override int GetHashCode() => (int)Value;
}
```

The struct exists purely to keep the app layer from assuming "peer identifier == uint" — its own doc comment says *"Keeps the app domain separated from the transport assumption."* If the transport ever moves off ENet, this abstraction stays intact.

### `PeerIdCache`

`Connections/Pulse/PeerIdCache.cs` — a thread-safe bidirectional map `uint ↔ Web3Address`:

```csharp
public class PeerIdCache
{
    private readonly object sync = new();
    private readonly Dictionary<uint, Web3Address> peersByWallet = new();
    private readonly Dictionary<Web3Address, uint> walletsByPeerId = new();

    public void Set(Web3Address wallet, uint peerId) { lock (sync) { peersByWallet[peerId] = wallet; walletsByPeerId[wallet] = peerId; } }
    public void Remove(uint peerId) { lock (sync) { if (peersByWallet.Remove(peerId, out var wallet)) walletsByPeerId.Remove(wallet); } }
    public bool TryGetWallet(uint peerId, out Web3Address wallet) { lock (sync) return peersByWallet.TryGetValue(peerId, out wallet); }
    public bool TryGetPeerId(Web3Address wallet, out uint peerId) { lock (sync) return walletsByPeerId.TryGetValue(wallet, out peerId); }

    public void RemoveAll(Action<string> onWalletRemoved)
    {
        lock (sync)
        {
            foreach (string wallet in peersByWallet.Values) onWalletRemoved(wallet);
            peersByWallet.Clear();
            walletsByPeerId.Clear();
        }
    }
}
```

All mutation and reads are serialized on a single `lock`. The cache is populated from `PlayerJoined` messages (in `PulseMultiplayerBus.PlayerState.cs` → `HandlePlayerJoined`) and evicted from `PlayerLeft` or full-disconnect events.

> **Note:** The field names `peersByWallet` / `walletsByPeerId` are **inverted** relative to the actual key types (`peersByWallet` is keyed by `uint` peer-id, not by wallet). Be careful reading call sites — trust the types, not the names.

### `RemoveAll` and disconnect fan-out

`RemoveAll(onWalletRemoved)` is the critical path when the transport disconnects. Called from `PulseMultiplayerBus.RemoveAllPeers`:

```csharp
private void RemoveAllPeers()
{
    peerIdCache.RemoveAll(wallet => removeIntentions.Enqueue(wallet));

    lastMovementMessages.Clear();
    pendingResyncs.Clear();
    emotingSubjects.Clear();
}
```

Under a single `lock`, every known peer is enqueued as a `RemoveIntention(wallet, RoomSource.PULSE)` and the internal dictionaries are cleared. The callback runs inside the lock — which means `removeIntentions.Enqueue` must not take a lock that could deadlock against `sync`. In practice it writes into a thread-safe collection with a separate lock, so this is safe.

### `SELF_MIRROR_WALLET_ID` — self-mirror sentinel

`PulseMultiplayerBus.cs` defines:

```csharp
internal const string SELF_MIRROR_WALLET_ID = "self_mirror";

private Web3Address ResolveSelfMirrorWallet(string userId)
{
    if (userId != SELF_MIRROR_WALLET_ID)
        return new Web3Address(userId);

    return identityCache.EnsuredIdentity().Address;
}
```

The Pulse server can advertise a player with the literal string `"self_mirror"` as the user id. When the client sees it, `ResolveSelfMirrorWallet` rewrites the wallet to the **local** identity — effectively making the host's own avatar visible as a remote player for this connection.

This is a debug/testing affordance: it lets a developer run Pulse end-to-end on a single client by having the server mirror their own state back at them, and exercises the whole receive pipeline (`HandlePlayerStateDelta`, interpolation, animation blending) against the host's real wallet.

The only caller is `HandlePlayerJoined` in `PulseMultiplayerBus.PlayerState.cs` — once the wallet is resolved at join time, the rest of the bus treats the mirrored peer as any other remote player.

> **Warning:** If the real local wallet is *also* present on the server under its own user id, the client will see two entries for the same wallet from two different peer-ids — `EntityParticipantTable` expects wallet uniqueness, so this would conflict. The sentinel is only meant for isolated dev environments.

---

## `PulseMultiplayerBus` — the app-layer bus

`PulseMultiplayerBus` implements both `IMovementMessageBus` and `IEmotesMessageBus` over the `IPulseMultiplayerService`. It is split across six partial class files under `Movement/Systems/`:

| File | Responsibility |
|---|---|
| `PulseMultiplayerBus.cs` | Fields, constructor, `Dispose`, `SELF_MIRROR_WALLET_ID`, `SubscribeToIncomingMessages` (handler registration), `RemoveAllPeers` shared teardown, and the `Inbox` helper. |
| `.PlayerState.cs` | Movement send, handlers for `PlayerJoined` / `PlayerLeft` / `PlayerStateFull` / `PlayerStateDelta`, the resync state machine, proto ↔ `NetworkMovementMessage` marshalling. |
| `.Teleports.cs` | `BroadcastTeleport` and `HandleTeleport`. |
| `.Profiles.cs` | `HandleProfileAnnouncement`. |
| `.Emotes.cs` | `IEmotesMessageBus` surface (`Send`, `SendStop`, intention bunches, retry), `HandleEmoteStarted` / `HandleEmoteStopped`, `emotingSubjects` set. |
| `.Disconnects.cs` | `HandleDisconnect` — reconnect-or-bail decision. |

Shared state lives on the main partial: `lastMovementMessages` (per-peer sequence + last known `NetworkMovementMessage`), `pendingResyncs`, `emotingSubjects`, `emoteIntentions` / `emoteStopIntentions` with a `MutexSync`. All of this runs on a single thread at a time (the routing loop), so the non-emote state uses plain `Dictionary`/`HashSet` without locks.

### `.cs` — subscription wiring

`SubscribeToIncomingMessages` is called by `PulseContainer` right after the bus is constructed. It registers one handler per `ServerMessage.MessageOneofCase` and the disconnect callback:

```csharp
pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.PlayerJoined,                    HandlePlayerJoined);
pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.PlayerLeft,                      HandlePlayerLeft);
pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.PlayerStateFull,                 HandlePlayerStateFull);
pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.PlayerStateDelta,                HandlePlayerStateDelta);
pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.PlayerProfileVersionAnnounced,  HandleProfileAnnouncement);
pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.Teleported,                      HandleTeleport);
pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.EmoteStarted,                    HandleEmoteStarted);
pulseService.RegisterSyncHandler(ServerMessage.MessageOneofCase.EmoteStopped,                    HandleEmoteStopped);
pulseService.RegisterDisconnectHandler(HandleDisconnect);
```

`RemoveAllPeers` — used by `HandleDisconnect` and, if needed, by any caller that wants to purge known peers:

```csharp
peerIdCache.RemoveAll(wallet => removeIntentions.Enqueue(wallet));
lastMovementMessages.Clear();
pendingResyncs.Clear();
emotingSubjects.Clear();
```

### `.PlayerState.cs` — movement send and receive

**Send** — `Send(NetworkMovementMessage)` on the `IMovementMessageBus` surface:

```csharp
public void Send(NetworkMovementMessage message)
{
    if (isDisposed || !pulseService.IsAuthenticated) return;

    var clientMessage = OutgoingMessage.Create(PacketMode.UNRELIABLE_SEQUENCED, ClientMessage.MessageOneofCase.Input);
    WritePlayerStateInput(message, clientMessage.Message.Input);
    pulseService.Send(clientMessage);
}
```

`WritePlayerStateInput` serializes the `NetworkMovementMessage` into a `PlayerStateInput` proto: parcel index via `parcelEncoder.Encode`, parcel-relative position, velocity, quantized rotation, `MovementBlend` (clamped 0..3), `SlideBlend`, head yaw/pitch (only when enabled), `GlideState` enum cross-mapping, and a packed `StateFlags` uint (`Grounded | LongJump | Falling | LongFall | Stunned | HeadYaw | HeadPitch`).

> **Note:** There's a `// TODO Override the last movement message in the pipe as it doesn't make sense to send more than 1` — the intent is to collapse queued duplicate movement messages at the pipe level so the worker thread only ships the newest one per flush. Not implemented today.

**Receive: `HandlePlayerJoined`** — caches the wallet↔subject-id pairing, enqueues an initial profile announcement, builds a `NetworkMovementMessage` from the joined `PlayerState`, stores it in `lastMovementMessages[subjectId] = (sequence, message)`, and ships it to the inbox.

**Receive: `HandlePlayerLeft`** — reverses: removes peer from all local maps, enqueues a `RemoveIntention`, drops any `emotingSubjects` entry.

**Receive: `HandlePlayerStateFull`** — updates `lastMovementMessages` (and clears any pending resync for the subject) and forwards the message to the inbox. `PlayerStateFull` carries a complete snapshot and resets the sequence tracking.

**Receive: `HandlePlayerStateDelta`** — the heart of the sequence-based resync logic:

```
             peer unknown ──► drop  (log warning)
             lastMessage missing ──► request resync on RELIABLE channel
             delta.NewSeq ≤ lastMovement.sequence ──► drop (stale)
             delta.BaselineSeq > lastMovement.sequence ──► request resync, drop delta
             delta.BaselineSeq == lastMovement.sequence ──► accept, clear pending resync
             delta.BaselineSeq <  lastMovement.sequence ──► drop (old resync delta)
```

Sequence numbers are per-peer; each delta references the baseline it was computed against. If the server's baseline is ahead of the client's known sequence, the client has missed packets and issues a `ResyncRequest(subjectId, knownSequence)` on the reliable channel. `pendingResyncs` acts as the dedup set — `TryAdd` returns `false` if a resync is already in flight.

Interleaving is deliberate: `STATE_DELTA` travels on `UNRELIABLE_SEQUENCED` (channel 1, unthrottled) while `ResyncRequest`/`PlayerStateFull` ride `RELIABLE` (channel 0). That's why a "normal" delta can sometimes arrive after a resync has been requested — if its `BaselineSeq == lastSequence`, it's the consecutive delta and the pending resync is cleared.

`MergeIntoNetworkMovementMessage(last, delta)` applies the `Has…` flags from the delta: every quantized field is optional in the proto, so the bus copies just the fields that changed, keeping the rest from `last`. Movement blend and velocity get their derived bookkeeping (`velocitySqrMagnitude`, `movementKind` inferred from blend value) recomputed.

`TryUpdateLastMovementAndCompleteResync(serverTick, subjectId, sequence, movement, allowOverrides)` centralizes the "update `lastMovementMessages` if the sequence is newer, and clear any pending resync" pattern — called from `PlayerStateFull`, `EmoteStarted`, `EmoteStopped`, and the teleport handler.

### `.Teleports.cs` — teleport broadcast and reception

**Send** — `BroadcastTeleport(realmName, worldPosition)` builds a reliable `TeleportRequest` with parcel index + relative position + realm name:

```csharp
var outgoing = OutgoingMessage.Create(PacketMode.RELIABLE, ClientMessage.MessageOneofCase.Teleport);
Vector2Int parcelIndex = worldPosition.ToParcel();
TeleportRequest teleport = outgoing.Message.Teleport;
teleport.ParcelIndex = parcelEncoder.Encode(parcelIndex);
teleport.Position = relativePosition.ToProtoVector();
teleport.Realm = realmName;
pulseService.Send(outgoing);
```

**Receive** — `HandleTeleport` builds a `NetworkMovementMessage` with `isInstant: true` (so the receiving [`RemotePlayersMovementSystem`](multiplayer.md#receive-pipeline) snaps rather than interpolates), updates the per-peer baseline, and ships to the inbox.

### `.Profiles.cs` — profile version announcements

A one-method partial. When the server says "peer *X* now has profile version *V*", the bus looks up the wallet and pushes a `(wallet, version)` pair into `PulseIncomingProfileAnnouncements` — that collector implements `IRemoteAnnouncements`, so `MultiplayerProfilesSystem` picks it up next frame through the `RemoteAnnouncementsProxy` (see [multiplayer.md → Remote Players](multiplayer.md#remote-players-entities-profiles-and-lifecycle)).

### `.Emotes.cs` — emote send/receive and `IEmotesMessageBus` surface

**State**:
- `emotingSubjects: HashSet<uint>` — peers currently mid-emote (main thread access only).
- `emoteIntentions: HashSet<RemoteEmoteIntention>` + `emoteStopIntentions: HashSet<RemoteEmoteStopIntention>` — per-frame outbound batches consumed by the emote proxy; both guarded by a shared `MutexSync` (see [multiplayer.md → Deduplication & Concurrency Primitives](multiplayer.md#deduplication--concurrency-primitives) for `OwnedBunch` / `MutexSync`).

**`IEmotesMessageBus.Send(urn, loopCyclePassed, mask, durationMs, playerState)`** — skips when `loopCyclePassed` is true (the local emote already finished a loop cycle, the server doesn't need to know again), builds a reliable `EmoteStart` with the emote urn, optional duration, and optional embedded `PlayerStateInput` so the server can fold a movement state update into the emote.

**`SendStop`** — emits a reliable `EmoteStop`. No subject id needed; the server knows who's sending.

**`OnPlayerRemoved(walletId)`** — no-op on Pulse (LiveKit clears a scheduler here; Pulse doesn't have one). The method exists to satisfy the interface.

**`SaveForRetry`** / `EnqueueEmoteIntention` — add an intention into the corresponding set under the `MutexSync` scope. These are the re-drop points used when a consumer couldn't apply an intention and wants to try again next frame.

**`HandleEmoteStarted`** — marks `emotingSubjects.Add(subjectId)`, builds a `NetworkMovementMessage` with `isInstant: true` from the emote's embedded `PlayerState`, updates the sequence baseline with `allowOverrides = true` (because an emote-started may arrive with the same sequence as a teleport or delta — it's authoritative in that case), fixes up any stored "isEmoting=false" state from a racing delta, and enqueues a `RemoteEmoteIntention` into the bunch:

```csharp
double timestamp = emoteStarted.ServerTick * SERVER_TICKS_TO_MOVEMENT_TIMESTAMP;
EnqueueEmoteIntention(new RemoteEmoteIntention(new URN(emoteStarted.EmoteId), walletId, timestamp, AvatarEmoteMask.AemFullBody));
```

The `EmoteStateMismatchCount` counter increments whenever the fix-up path had to run — useful for observing how often the unreliable `STATE_DELTA` races the reliable `EmoteStarted`.

**`HandleEmoteStopped`** — removes from `emotingSubjects`, enqueues a `RemoteEmoteStopIntention` with the server-tick timestamp, and — if the message carries an updated `PlayerState` — runs the same `TryUpdateLastMovementAndCompleteResync` routine. If no state is embedded, it patches `lastMovementMessages[subjectId].isEmoting = false` in place so the next delta arrives with the correct baseline.

**`IsPeerEmoting(Web3Address wallet)`** — internal helper used by the debug system; cheap `peerIdCache.TryGetPeerId` + `emotingSubjects.Contains`.

### `.Disconnects.cs` — reconnect-or-bail

```csharp
private bool HandleDisconnect(DisconnectReason reason)
{
    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse transport disconnected: {reason}");
    RemoveAllPeers();
    return reason is DisconnectReason.NONE or DisconnectReason.GRACEFUL;
}
```

Every disconnect triggers `RemoveAllPeers` — every remote we knew about is evicted from the caches and surfaced as a `RemoveIntention`, so the entity/profile layer tears down remote avatars cleanly.

The return value is the reconnect flag consumed by `PulseMultiplayerService.StartRouting`. Only `NONE` (timeout) and `GRACEFUL` (clean server shutdown) ask the service to retry. `AUTH_FAILED`, `DUPLICATE_SESSION`, `KICKED`, `SERVER_FULL`, and `AUTH_TIMEOUT` return `false` — retrying those would just fail again or (for `DUPLICATE_SESSION`) fight another session.

---

## Profile propagation over Pulse

Pulse implements `DCL.Profiles.Self.IProfilePropagation` with `PulseProfilePropagationBus`. The interface is two lines:

```csharp
public interface IProfilePropagation
{
    void Propagate(Profile profile);

    public class Dummy : IProfilePropagation
    {
        public void Propagate(Profile profile) { }
    }
}
```

The `Dummy` is the feature-flag-off fallback — when Pulse is disabled, `PulseContainer` substitutes it so the rest of the code calls `Propagate` unconditionally and it becomes a no-op.

### `PulseProfilePropagationBus`

`Connections/Pulse/PulseProfilePropagationBus.cs`:

```csharp
public void Propagate(Profile profile)
{
    var message = OutgoingMessage.Create(PacketMode.RELIABLE, ClientMessage.MessageOneofCase.ProfileAnnouncement);

    message.Message.ProfileAnnouncement = new ProfileVersionAnnouncement
    {
        Version = profile.Version,
    };

    service.Send(message);
}
```

Key facts:
- The wire message is `ProfileVersionAnnouncement { Version }` — **only the version number**. The full profile is fetched via HTTP by receivers through the normal `IProfileRepository` path, same as on LiveKit. Remote clients who already hold that version skip the fetch.
- Sent on `PacketMode.RELIABLE` (channel 0) — profile announcements must arrive.

### Where it's called from

`DCL/UserInAppInitializationFlow/StartupOperations/StartPulseMultiplayerStartupOperation.cs` is the main (and currently only regular) caller:

```csharp
protected override async UniTask InternalExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
{
    await service.ConnectAsync(ct);
    Profile? profile = await selfProfile.ProfileAsync(ct);
    profilePropagation.Propagate(profile!);
}
```

On startup, connect to Pulse, fetch the host's profile, and announce its version. This is a **one-shot** at connect — there's no debounce mechanism like `DebounceLiveKitProfileBroadcast` watching `ISelfProfile.ProfilePropagated` continuously. (LiveKit's broadcast runs on every profile change; Pulse only fires at connect time today.)

`MultiplayerContainer` does also subscribe to `ISelfProfile.ProfilePropagated` and invoke `ProfilePropagation.Propagate(profile)` for later updates — so subsequent profile changes do reach Pulse. But there's no timer-based throttle wrapping that call, so the cadence is whatever `ISelfProfile` chooses to fire at.

---

## Incoming collectors

Both of these are the Pulse half of transport-neutral interfaces (`IRemoteAnnouncements`, `IRemoveIntentions`). The LiveKit halves are in [livekit-networking.md](livekit-networking.md); the proxies in [multiplayer.md](multiplayer.md#transport-selection--wiring) union the two.

### `PulseIncomingProfileAnnouncements`

`Profiles/Announcements/PulseIncomingProfileAnnouncements.cs`:

```csharp
public class PulseIncomingProfileAnnouncements : IRemoteAnnouncements
{
    private readonly ConcurrentQueue<RemoteAnnouncement> queue = new();

    public void Enqueue(string userId, int version) =>
        queue.Enqueue(new RemoteAnnouncement(version, userId, RoomSource.PULSE));

    public void Fill(List<RemoteAnnouncement> announcements)
    {
        while (queue.TryDequeue(out RemoteAnnouncement item))
            announcements.Add(item);
    }

    public void Remove(IReadOnlyCollection<RemoveIntention> removeIntentions) { }
}
```

Called by `PulseMultiplayerBus`:
- `HandlePlayerJoined` — enqueues `(userId, profileVersion)` on first sight of a peer.
- `HandleProfileAnnouncement` — enqueues on subsequent profile-version announcements from the server.

Both tag the announcement with `RoomSource.PULSE` so `RemoteProfiles`' pending-request tracking knows which transport sourced it (important for mergeable pending requests — see [multiplayer.md → `RemoteProfiles`](multiplayer.md#remoteprofiles--announcement--profile-download)).

Uses a `ConcurrentQueue` because `Enqueue` may run on the routing thread while `Fill` runs on the main thread. `Remove` is a no-op — Pulse doesn't need to drop pending announcements when a player leaves; the removal flows through `PulseRemoveIntentions` instead.

### `PulseRemoveIntentions`

`Profiles/RemoveIntentions/PulseRemoveIntentions.cs`:

```csharp
public class PulseRemoveIntentions : IRemoveIntentions
{
    private readonly MutexSync mutexSync = new();
    private readonly HashSet<RemoveIntention> set = new();

    public void Enqueue(string walletId)
    {
        using (mutexSync.GetScope())
            set.Add(new RemoveIntention(walletId, RoomSource.PULSE));
    }

    public OwnedBunch<RemoveIntention> Bunch() => new(mutexSync, set);
}
```

Called by `PulseMultiplayerBus`:
- `HandlePlayerLeft` — enqueues on explicit peer departure.
- `RemoveAllPeers` (called from `HandleDisconnect`) — enqueues every known peer when the transport drops.

Guarded by `MutexSync` because enqueues arrive from the routing thread but `Bunch()` is consumed from the main thread — the `OwnedBunch<RemoveIntention>` pattern ensures the consumer holds the mutex while reading and clears the set on dispose. See [multiplayer.md → Deduplication & Concurrency Primitives](multiplayer.md#deduplication--concurrency-primitives) for the primitive.

---

## `PulseContainer` wiring

`Movement/Systems/PulseContainer.cs` is an `internal` `DCLWorldContainer<Settings>` — it manages the Pulse stack's lifecycle and settings.

### Construction

```csharp
public static async UniTask<PulseContainer> CreateAsync(IPluginSettingsContainer pluginSettingsContainer,
    IWeb3IdentityCache web3IdentityCache, MovementInbox movementInbox, LandscapeData landscapeData,
    IDecentralandUrlsSource urlsSource, CancellationToken ct)
{
    var container = new PulseContainer(web3IdentityCache, movementInbox,
        new ParcelEncoder(landscapeData.terrainData), urlsSource);
    await container.InitializeContainerAsync<PulseContainer, Settings>(pluginSettingsContainer, ct);
    return container;
}
```

Two pieces allocated eagerly:
- `peerIdCache` — a shared `PeerIdCache` used by the bus and internal consumers.
- `parcelEncoder` — built from `LandscapeData.terrainData` (same Genesis-City-aware encoder used by [multiplayer.md → Encoding](multiplayer.md#networkmovementmessage--encoding)). Re-exposed on `MultiplayerContainer.ParcelEncoder` so other systems can share it.

### `InitializeInternalAsync`

Ordered construction of the internal stack:

```csharp
transport = new ENetTransport(settings.ENetTransportOptions, messagePipe);
pulseMultiplayerService = FeatureEnabled
    ? new PulseMultiplayerService(transport, messagePipe, identityCache, urlsSource)
    : new IPulseMultiplayerService.Dummy();

pulseMultiplayerBus = new PulseMultiplayerBus(pulseMultiplayerService, peerIdCache,
    movementInbox, parcelEncoder, IncomingProfiles, RemoveIntentions, identityCache);
pulseMultiplayerBus.SubscribeToIncomingMessages();

pulseProfilePropagationBus = FeatureEnabled
    ? new PulseProfilePropagationBus(pulseMultiplayerService)
    : new IProfilePropagation.Dummy();
```

Observations:
1. **`ENetTransport` is always constructed**, even when Pulse is off — it's just never connected. The service (not the transport) is what gets swapped for the `Dummy`.
2. **`PulseMultiplayerBus` is always constructed** and wired to the service. When the service is a `Dummy`, `SubscribeToIncomingMessages` registers handlers that will never be called — harmless.
3. **Subscribe before connect** — `SubscribeToIncomingMessages` runs in `InitializeInternalAsync`, but the first `ConnectAsync` happens later in `StartPulseMultiplayerStartupOperation`. Handlers must be ready before the routing loop starts producing events.

### Settings

`PulseContainer.Settings : IDCLPluginSettings` has two serialized fields:

```csharp
[field: SerializeField] public ENetTransportOptions ENetTransportOptions { get; private set; }
[field: SerializeField] public LandscapeDataRef LandscapeData { get; private set; }
```

Loaded via the standard `IPluginSettingsContainer` — the same mechanism other plugins use (see [feature-flags.md](feature-flags.md) for the pattern).

### Dispose

```csharp
lifeCycleCts?.SafeCancelAndDispose();
pulseMultiplayerBus?.Dispose();
pulseMultiplayerService?.Dispose();
```

Note: `pulseMultiplayerService.Dispose()` disposes the underlying `ENetTransport` too (`transport.Dispose()` is called from `PulseMultiplayerService.Dispose`), so the container doesn't need to dispose the transport separately.

---

## Differences from LiveKit at a glance

| Concern | LiveKit | Pulse |
|---|---|---|
| Topology | Room-based (Island + Scene + Chat + VoiceChat, each a LiveKit room) | Single client↔server peer connection |
| Underlying protocol | WebRTC via the LiveKit client SDK | Reliable-UDP via ENet, through the `Pulse.Transport` UPM package |
| Transport channels | LiveKit data tracks with internal reliability choices | Three explicit ENet channels (RELIABLE / UNRELIABLE_SEQUENCED-unthrottled / UNRELIABLE_UNSEQUENCED) |
| Authentication | Per-room sign flow (Archipelago-served token, GateKeeper-served token, connection strings for voice) | One-time handshake with a signed auth chain over channel 0 |
| Framing | Protobuf messages (`MovementCompressed` or `Decentraland.Kernel.Comms.Rfc4.Movement`) over LiveKit data pipes | Protobuf `ClientMessage`/`ServerMessage` envelopes (`oneof`) over ENet packets |
| Movement encoding | `NetworkMessageEncoder` bit-packing (160 bits per frame) when `UseCompression` is on | `PlayerStateInput` proto with quantized fields + delta encoding (`PlayerStateDeltaTier0`) |
| Identity | Per-room participants keyed by wallet (`LKParticipant.Identity`) | Server-assigned `PeerId uint` mapped to wallets via `PeerIdCache` |
| Movement loss handling | Periodic full-state via send system's adaptive rate | Sequence-baselined deltas with on-demand `ResyncRequest` when a gap is detected |
| Profile announcement trigger | `DebounceLiveKitProfileBroadcast` watching `ISelfProfile.ProfilePropagated` continuously | One-shot at connect from `StartPulseMultiplayerStartupOperation` |
| Profile wire payload | Version only (`AnnounceProfileVersion`) | Version only (`ProfileVersionAnnouncement`) |
| Disconnect semantics | `IConnectiveRoom` state machine with per-room reconnection; `DuplicateIdentity` stops the loop | Single-connection; reconnect only on `NONE` (timeout) or `GRACEFUL` disconnect reasons |
| Voice chat | `VoiceChatActivatableConnectiveRoom` (on-demand room) | ❌ Not supported — no voice track plumbing on Pulse |
| Text chat | `ChatConnectiveRoom` | ❌ Not supported — chat flows over LiveKit's chat room |
| Scene CRDT sync | GateKeeper Scene room (host-only scope) | ❌ Not supported — scene CRDT stays on LiveKit |
| Remove-intention delivery | `LiveKitRemoveIntentions` subscribes to per-room participant `Disconnected` events from the LiveKit thread | `PulseRemoveIntentions` populated by `HandlePlayerLeft` and `RemoveAllPeers` from the routing thread |
| Thread handoff primitive | `MutexSync` + `OwnedBunch<T>` (identical) | `MutexSync` + `OwnedBunch<T>` (identical) |

In short: **Pulse focuses on low-latency player-state traffic** (movement, emotes, teleports, profile version announcements) and leaves chat, voice, and scene CRDT sync entirely to LiveKit. That's why both transports must be active for full functionality — Pulse is additive, not a replacement.

---

## See Also

- **[Multiplayer](multiplayer.md)** — transport-neutral hub: shared interfaces, movement pipeline, entity/profile tables, SDK propagation.
- **[Network Synchronization](livekit-networking.md)** — LiveKit transport, rooms, voice chat, message pipes.
- **[Feature Flags](feature-flags.md)** — how `FeaturesRegistry` and `FeatureId.PULSE` are resolved.
- **[App Arguments](app-arguments.md)** — `--pulse` and other launch flags.
