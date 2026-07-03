# Multiplayer

This page is the transport-agnostic reference for the multiplayer stack — the domain model, shared interfaces, movement pipeline, entity/profile tables, SDK propagation, and the wiring that lets two transports run side by side.

For transport-specific details:
- LiveKit rooms, messaging pipes, Archipelago/GateKeeper, voice and chat rooms — see **[Network Synchronization](livekit-networking.md)**.
- Pulse (ENet-based UDP transport) — see **[Pulse](pulse.md)**.

---

## Overview & Architecture

Two transports are active at the same time: **LiveKit** (Archipelago Island room + GateKeeper Scene room) and **Pulse** (ENet-based peer transport). `MultiplayerContainer` owns both and exposes proxy implementations of the shared interfaces that fan calls out to each transport and merge incoming data on the way back.

```
                    ┌───────────────────────────┐
                    │    MultiplayerContainer   │
                    │                           │
 systems ──► IMovementMessageBus (proxy) ──┬──► LiveKitMovementMessageBus
            IEmotesMessageBus   (proxy) ──┤
            IRemoteAnnouncements(proxy) ──┤    ┌──► PulseMultiplayerBus
            IRemoveIntentions   (proxy) ──┴────┘      (+ PulseIncomingProfileAnnouncements,
                                                        PulseRemoveIntentions)
            IProfileBroadcast   ──► LiveKit only
            IProfilePropagation ──► Pulse only
```

Both transports send the same `NetworkMovementMessage` / emote / profile-announcement payloads. On the receive side, incoming items from both transports are de-duplicated by wallet ID so the rest of the ECS pipeline is transport-oblivious.

Pulse is gated by the `FeatureId.PULSE` feature flag, resolved once into a shared `PulseActivation`. When inactive, Pulse is replaced with no-op dummies and only LiveKit carries traffic, with `LiveKitMessagesBroadcaster` broadcasting to all peers. When active, the broadcaster sends only to peers that announced over LiveKit (the rest receive over Pulse). If the Pulse server is unreachable at start-up, the client falls back fully to LiveKit. See [Transport Selection & Wiring](#transport-selection--wiring) below.

---

## Core Interfaces

All transport-neutral interfaces live under `Explorer/Assets/DCL/Multiplayer/`. Systems depend on these, not on the LiveKit/Pulse implementations directly.

| Interface | Location | Responsibility | Implementations |
|---|---|---|---|
| `IMovementMessageBus` | `Movement/IMovementMessageBus.cs` | Send `NetworkMovementMessage`; broadcast teleport | `LiveKitMovementMessageBus`, `PulseMultiplayerBus`, proxy |
| `IEmotesMessageBus` | `Emotes/IEmotesMessageBus.cs` | Send/receive emote start and stop intentions | `LiveKitEmotesMessageBus`, `PulseMultiplayerBus`, proxy |
| `IRemoteAnnouncements` | `Profiles/Announcements/IRemoteAnnouncements.cs` | Collect incoming profile-version announcements from remote players | `LiveKitRemoteAnnouncements`, `PulseIncomingProfileAnnouncements`, proxy |
| `IRemoveIntentions` | `Profiles/RemoveIntentions/IRemoveIntentions.cs` | Collect remote-player disconnect intents from the network thread | `LiveKitRemoveIntentions`, `PulseRemoveIntentions`, proxy |
| `IProfileBroadcast` | `Profiles/BroadcastProfiles/IProfileBroadcast.cs` | Announce the self profile version to other clients | `LiveKitProfileBroadcast` (wrapped by `DebounceLiveKitProfileBroadcast`) — **LiveKit only** |
| `IProfilePropagation` | `DCL.Profiles.Self.IProfilePropagation` | Announce self-profile version over the active transport | `PulseProfilePropagationBus` on Pulse, `IProfilePropagation.Dummy` when Pulse is off |
| `IMessageDeduplication` | `Deduplication/IMessageDeduplication.cs` | Suppress duplicate messages observed on multiple transports | `MessageDeduplication` |
| `IEntityParticipantTable` | `Profiles/Tables/IEntityParticipantTable.cs` | Bidirectional wallet ↔ ECS entity map with `RoomSource` reference counting | `EntityParticipantTable` |
| `IRemoteEntities` | `Profiles/Entities/IRemoteEntities.cs` | Pool and lifecycle of remote-player entities | `RemoteEntities` |
| `IOnlineUsersProvider` | `Connectivity/IOnlineUsersProvider.cs` | Fetch online players (HTTP) | `ArchipelagoHttpOnlineUsersProvider` (+ decorator) |

> `IProfileBroadcast` (LiveKit) and `IProfilePropagation` (Pulse) are intentionally asymmetric — LiveKit debounces self-profile version announcements via `DebounceLiveKitProfileBroadcast`; Pulse fires a one-shot propagate on startup. Both ship only the profile *version* on the wire; remote clients fetch the full profile through the HTTP profile repository.

---

## Transport Selection & Wiring

### `MultiplayerContainer` and the proxy pattern

`Movement/Systems/MultiplayerContainer.cs` composes one `PulseContainer` and one `LiveKitMultiplayerContainer`, then publishes four proxy objects that implement the shared interfaces:

- `MovementMessageBusProxy` → calls `Send` / `BroadcastTeleport` on **both** buses.
- `EmoteMessageBusProxy` → sends on both buses; for reads, it unions `EmoteIntentions()` / `EmoteStopIntentions()` from each source into a shared `HashSet` wrapped in an `OwnedBunch`.
- `RemoteAnnouncementsProxy` → fills a single list from both announcement sources; `Remove` fans out to both.
- `RemoveIntentionsProxy` → unions disconnect intents from both sources, deduplicating via `HashSet<RemoveIntention>`.

`IProfileBroadcast` is exposed directly from the LiveKit container; `IProfilePropagation` directly from the Pulse container. No proxy for those.

### Feature flag decision points

Pulse is gated by `FeatureId.PULSE`, read once in `MultiplayerContainer.CreateAsync` into a shared `PulseActivation` (the session-wide "is Pulse the active transport" flag):

- `Movement/Systems/PulseContainer.cs` receives the `PulseActivation` and, during init, chooses between the real `PulseMultiplayerService` / `PulseProfilePropagationBus` and their `Dummy` counterparts based on `IsActive`.
- `Movement/Systems/LiveKitMultiplayerContainer.cs` passes the same `PulseActivation` to `LiveKitMessagesBroadcaster`, which reads `IsActive` live: when active it targets only LiveKit-announced peers, when inactive it broadcasts to all.
- `StartPulseMultiplayerStartupOperation` calls `PulseActivation.Deactivate()` if Pulse is unreachable at start-up (full fallback to LiveKit). Runtime reconnection failures never deactivate.
- The `--pulse true` / `--pulse false` program argument overrides the remote flag at launch; when absent, the remote flag drives it.

### Construction order

1. `MultiplayerContainer.CreateAsync` awaits `PulseContainer.CreateAsync` (creates `ENetTransport`, `MessagePipe`, `PeerIdCache`, `ParcelEncoder` from landscape data, and the Pulse buses).
2. In parallel the `LiveKitMultiplayerContainer` constructor runs synchronously once a `IRoomHub` and `IMessagePipesHub` are available — it creates a single `LiveKitMessagesBroadcaster` shared by the movement / emotes / announcements / profile-broadcast buses.
3. The returned `MultiplayerContainer` subscribes to `ISelfProfile.ProfilePropagated` and forwards profile updates through `IProfilePropagation` (Pulse path).

Disposal flows symmetrically: `MultiplayerContainer.Dispose()` detaches the profile handler and disposes both sub-containers.

---

## `NetworkMovementMessage` & Encoding

### The payload

`Movement/NetworkMovementMessage.cs` is the transport-neutral struct carried by every movement send. It's produced by `PlayerMovementNetSendSystem` and consumed by `RemotePlayersMovementSystem`:

```csharp
public struct NetworkMovementMessage : IEquatable<NetworkMovementMessage>
{
    public double timestamp;          // Time.unscaledTime at send (double for precision)
    public Vector2Int parcel;         // Parcel coords (kept for diffs between messages)
    public Vector3 position;          // World position
    public Vector3 velocity;
    public float velocitySqrMagnitude;
    public float rotationY;           // Avatar yaw in degrees
    public bool headIKYawEnabled, headIKPitchEnabled;
    public Vector2 headYawAndPitch;
    public MovementKind movementKind; // IDLE / WALK / JOG / RUN
    public bool isSliding, isStunned, isInstant, isEmoting;
    public bool isPointingAt;
    public Vector3 pointAtWorldHitPoint;
    public AnimationStates animState; // Grounded, jump count, falling, etc.
    public byte velocityTier;         // 0..3 — picks encoding config
}
```

Notable fields:
- `timestamp` is a `double` (seconds since unscaled startup). It's used to order messages and compute interpolation duration.
- `isInstant` snaps the remote avatar to `position` instead of interpolating — set on first message and teleports.
- `velocityTier` selects which `MovementEncodingConfig` (of four) the encoder uses to compress position and velocity, trading precision against range at different speeds.
- `parcel` is not transmitted raw; it's derived inside the encoder, stored on the struct only so receivers can diff subsequent messages.

### Compressed wire form

`Movement/Encoder/CompressedNetworkMovementMessage.cs` is what actually goes on the wire (both LiveKit compressed-schema and Pulse):

```csharp
public struct CompressedNetworkMovementMessage
{
    public int  temporalData;  // 32 bits: timestamp, anim flags, rotation, tier
    public long movementData;  // 64 bits: parcel index + relative position + velocity
    public int  headSyncData;  // 32 bits: head yaw/pitch + enable flags
    public int  pointAtData;   // 32 bits: point-at relative hit point
}
```

That's **160 bits of packed state per frame per player**, plus a small Protobuf envelope, irrespective of transport.

### Encoding pipeline: `NetworkMessageEncoder`

`Movement/Encoder/NetworkMessageEncoder.cs` orchestrates the four sub-encoders. It is a public class (`public class NetworkMessageEncoder`) despite the lingering `"Encoder for compressed LiveKit movement"` doc comment — both LiveKit's `LiveKitMovementMessageBus` (when `UseCompression` is on) and Pulse's `PulseMultiplayerBus` use it.

`Compress(NetworkMovementMessage)` produces the four packed ints/longs:

| Field | Content (high → low bits, by layout logic) |
|---|---|
| `temporalData` (32b) | `tier` (2b) → `rotationY` (6b) → anim flags (`isLongFall`, `isFalling`, `isLongJump`, `jumpCount` 2b, `isGrounded`, `isStunned`, `isSliding`) → `movementKind` (2b) → `timestamp` (15b, circular) |
| `movementData` (64b) | `velocityZ`, `velocityY`, `velocityX` (per-tier bits, signed) → `y` (per-tier bits, absolute) → `z`, `x` (per-tier bits, parcel-relative) → `parcelIndex` (17b) |
| `headSyncData` (32b) | `yawEnabled` flag → `pitchEnabled` flag → `yaw` (6b) → `pitch` (6b). Zero when both disabled. |
| `pointAtData` (32b) | `isPointing` flag (bit 30) → `z` (10b) → `y` (10b) → `x` (10b) relative to the sender's position. Zero when not pointing. |

Bit positions are not hard-coded — they're derived from `MessageEncodingSettings` constants so the layout can be tuned without touching the encoder.

### `FloatQuantizer` — scalar compression

`Movement/Encoder/FloatQuantizer.cs` — five-line symmetric pair:

```csharp
public static int Compress(float value, float minValue, float maxValue, int sizeInBits)
{
    int maxStep = (1 << sizeInBits) - 1;
    float normalizedValue = (value - minValue) / (maxValue - minValue);
    return Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * maxStep);
}
```

Lossy. `sizeInBits` picks the precision ↔ bandwidth trade-off. The encoder calls it everywhere: rotation, relative position, head yaw/pitch, velocity magnitude, point-at axes (the last two wrap it with an explicit sign bit — see below).

### `ParcelEncoder` — 2D parcel flattening

`Movement/Encoder/ParcelEncoder.cs` flattens a `Vector2Int` parcel coord into a single 17-bit index using a Genesis-City-aware grid with border padding from `TerrainGenerationData`:

```csharp
public int Encode(Vector2Int parcel) =>
    parcel.x - MinX + ((parcel.y - MinY) * width);
```

The encoder then transmits only the position **relative to the parcel origin**, encoded with per-axis bit budgets from `MovementEncodingConfig`. That keeps each axis's dynamic range bounded to one parcel (`ParcelMathHelper.PARCEL_SIZE`), giving more precision per bit than encoding world-absolute coordinates would.

Notice that `ParcelEncoder` is constructed once from landscape data and **shared** between the encoder and other Pulse-internal uses — `PulseContainer` instantiates it (`new ParcelEncoder(landscapeData.terrainData)`) and re-exposes it via `MultiplayerContainer.ParcelEncoder`.

### `TimestampEncoder` — circular-buffer time

`Movement/Encoder/TimestampEncoder.cs` encodes a monotonic `double` timestamp into a fixed bit count (`TIMESTAMP_BITS`, default 15 → 32 768 steps) at a given quantum (`TIMESTAMP_QUANTUM`, default 0.02s → ~655s buffer).

Compression is `round(timestamp / quantum) % steps`. Decompression reconstructs the absolute time by tracking:
- `lastOriginalTimestamp` — the last timestamp it handed out
- `timestampOffset` — the accumulated buffer offset

When a decompressed value lands more than `0.75 × BufferSize` earlier than the last one, a full buffer has elapsed and `timestampOffset` advances by one buffer length. This handles senders whose compressed counter has wrapped.

> **Warning:** `TimestampEncoder` is stateful per remote sender (it holds `lastOriginalTimestamp` and `timestampOffset`). `PulseMultiplayerBus` / `LiveKitMovementMessageBus` must keep one encoder instance per *incoming stream* — sharing one across senders would corrupt wraparound detection.

### Velocity encoding (sign + magnitude)

Velocity axes are clamped and compressed with an explicit sign bit, not as centered values. From `NetworkMessageEncoder.CompressedVelocity`:

```csharp
int withoutSignBits = sizeInBits - 1;
float absVelocity = Mathf.Abs(velocity);
int compressed = FloatQuantizer.Compress(absVelocity, 0, range, withoutSignBits);
compressed <<= 1;
compressed |= NegativeSignFlag(velocity);
```

A `SAFE_ZONE = 0.05f` threshold zeroes out `sqrMagnitude` below that, preventing sub-noise jitter from consuming bits.

### Point-at encoding (sqrt-compressed)

Point-at coordinates are stored **relative to the sender's position** and axis-quantized with a square-root curve (`sqrt(|v|/max) → 9 bits` per axis, plus a sign bit and a global "is pointing" flag at bit 30). This concentrates precision near the pointer's hand and sacrifices it at long range — appropriate for close-range interaction and coarse pointing at distant targets.

### Bit budget summary

| Field | Bits | Source |
|---|---|---|
| Timestamp (quantized, circular) | 15 | `MessageEncodingSettings.TIMESTAMP_BITS` |
| `MovementKind` | 2 | `MOVEMENT_KIND_BITS` |
| `isSliding`, `isStunned`, `isGrounded` | 1 each | literal |
| `jumpCount` | 2 | literal |
| `isLongJump`, `isFalling`, `isLongFall` | 1 each | literal |
| `rotationY` | 6 | `ROTATION_Y_BITS` (≈ 5.6° precision) |
| Velocity tier | 2 | `TWO_BITS_MASK` (4 tiers) |
| Parcel index | 17 | `PARCEL_BITS` |
| Position X, Z (parcel-relative) | per-tier | `MovementEncodingConfig.XZ_BITS`, default 9 |
| Position Y (absolute) | per-tier | `Y_BITS`, default 13, max `Y_MAX=500` |
| Velocity X, Y, Z (signed) | per-tier | `VELOCITY_BITS`, default 1 + sign |
| Head yaw/pitch | 6 each | `HEAD_ROTATION_BITS` |
| Head yaw/pitch enabled | 1 each | literal |
| Point-at X, Y, Z | 9 + sign each | `AXIS_BITS = 10` |

### Where the encoder is called

- **LiveKit** — `LiveKitMovementMessageBus` calls `Compress` when `UseCompression` is enabled and serializes the four fields into the `MovementCompressed` Protobuf schema; the uncompressed `Decentraland.Kernel.Comms.Rfc4.Movement` schema bypasses the encoder and ships the raw struct.
- **Pulse** — `PulseMultiplayerBus.PlayerState.cs` always uses the compressed form.

Tests live in `Movement/Tests/MovementMessageCompressionTests.cs` and verify round-tripping across all four tiers.

---

## Send System & Adaptive Rate

`Movement/Systems/PlayerMovementNetSendSystem.cs` is the single entry point for outgoing player movement. It runs in `PostRenderingSystemGroup` on the global world and calls `IMovementMessageBus.Send` — which is the transport-fanout proxy, so every send goes to both LiveKit and Pulse (subject to their respective feature/compression flags).

### Per-second hard cap

A floor-level safety net: no more than `MAX_MESSAGES_PER_SEC = 10` messages leave the system per second, tracked on `PlayerMovementNetworkComponent`:

```csharp
private const int MAX_MESSAGES_PER_SEC = 10; // 10 Hz == 10 [msg/sec]

if (playerMovement.MessagesSentInSec >= MAX_MESSAGES_PER_SEC) return;
```

`UpdateMessagePerSecondTimer` resets the counter every 1s using a cooldown stored on the component. Every successful send bumps `MessagesSentInSec++`.

### Adaptive `sendRate`

`sendRate` is the minimum interval (seconds) between two *ordinary* sends. It is bounded by two settings on `MultiplayerMovementSettings`:

- `MoveSendRate` (default **0.1s** = 10 Hz) — fastest; used while the player is actively moving.
- `StandSendRate` (default **1s** = 1 Hz) — slowest; target cadence while idle.

The rate adapts each frame:

1. Initialized to `MoveSendRate` in the constructor.
2. **Reset to fast** — if anything changed this frame *and* current `sendRate > MoveSendRate`, clamp back down to `MoveSendRate`. Any motion instantly restores 10 Hz.
3. **Gate check** — if `timeDiff = Time.unscaledTime − LastSentMessage.timestamp` exceeds `sendRate`, a send is eligible.
4. **Exponential decay when idle** — if a send fires with `anythingChanged == false` and `sendRate < StandSendRate`, double `sendRate` (capped at `StandSendRate`). Cadence decays 0.1 → 0.2 → 0.4 → 0.8 → 1.0 s while the avatar is still.

### Bypass paths (not subject to `sendRate`)

Two discrete events send immediately, still counted against the 10/s cap:

- **First message** — `playerMovement.IsFirstMessage` is true on startup. Sent with `isInstant: true` so the remote receiver teleports to the position rather than interpolating from its default state.
- **Grounded / jump-count transitions** — comparing `LastSentMessage.animState.IsGrounded` / `JumpCount` against the current `animState`. These are state-machine edges that must not be throttled; delaying them would desynchronize the remote player's animation.

`PlayerTeleportIntent.JustTeleported` is also detected and forwarded through the `justTeleported` flag on the send, letting the receiver know to skip interpolation blending.

### Change detection

`AnythingChanged` inside the query checks five signals against the last sent message, each with its own epsilon:

| Signal | Threshold | Source |
|---|---|---|
| Position | `SqrMagnitude > POSITION_MOVE_EPSILON²` | `1e-4` m² → **1 mm** |
| Velocity | `SqrMagnitude > VELOCITY_MOVE_EPSILON²` | `0.01` (m/s)² → **1 cm/s** |
| Rotation Y | `Mathf.Abs > 0.1f` | 0.1° |
| Head IK yaw/pitch | `Math.Abs > HEAD_IK_EPSILON` | **1°** |
| Head IK enabled flags | any toggle | — |
| Point-at world hit | `SqrMagnitude > POSITION_MOVE_EPSILON²` | 1 mm |
| `isPointingAt` | any toggle | — |

Thresholds are constants in `PlayerMovementNetSendSystem`; they are *not* configurable via settings.

### Assembled `NetworkMovementMessage`

On a successful send, `SendMessage` builds the message from live components:

- `position` / `rotationY` from the `CharacterController` transform (not from pending move input, so the message reflects the rendered frame).
- `velocity` from `CharacterController.velocity`.
- Computed `speed` for tiering = `distance / time` **relative to the last sent message**, not from `Character.velocity` (that would read 0 on moving platforms where the controller is carried).
- `velocityTier` = result of `VelocityTierFromSpeed` stepping through `settings.VelocityTiers`.
- `headYawAndPitch` and enable flags, masked by the `SETTINGS_HEAD_SYNC_ENABLED` player pref.
- Full `AnimationStates` snapshot (blend values included for debug, though the encoder discards them).
- `isEmoting` from `CharacterEmoteComponent.IsPlayingEmote`, `isStunned` / `isSliding` from components.

After building, the message is stored in `playerMovement.LastSentMessage` for the next frame's diff and handed to `movementMessageBus.Send(...)`.

### Debug self-send

`MultiplayerDebugSettings.SelfSending` mirrors every non-RUN outgoing message back to the local player's inbox with a configurable latency and jitter:

```csharp
messageBus.SelfSendWithDelayAsync(playerMovement.LastSentMessage,
    debugSettings.Latency + (debugSettings.Latency * Random.Range(0, debugSettings.LatencyJitter)))
    .Forget();
```

`SelfSendWithDelayAsync` is on `LiveKitMovementMessageBus` specifically (the system holds a typed `LiveKitMovementMessageBus` reference alongside the proxy `IMovementMessageBus`, purely for this debug path). RUN motion is intentionally excluded to make packet-loss scenarios easier to observe.

> **Note:** `IMultiplayerMovementSettings.SendRules` / `Movement/Settings/Rules/*.cs` define a more granular send-rule framework (position-diff, velocity-angle, speed-tier crossing, etc.), but it is **not currently wired into** `PlayerMovementNetSendSystem` — the asset ships with an empty `SendRules` list and no code paths consume it. If this framework is activated in the future, document it here.

---

## Receive Pipeline

Outgoing messages go over *either* transport; incoming messages arrive over *both* and are funnelled into a single `MovementInbox`, then consumed on the main thread by `RemotePlayersMovementSystem` and `RemotePlayerAnimationSystem`.

### `MovementInbox` — the thread boundary

`Movement/MovementInbox.cs` is the single landing point for incoming movement from any transport. It's constructed once in `DynamicWorldContainer` and passed to both `LiveKitMovementMessageBus` and `PulseMultiplayerBus`.

Both buses call `Enqueue(message, walletId)` from their own background network threads:

```csharp
public void Enqueue(NetworkMovementMessage fullMovementMessage, string @for)
{
    ReportHub.Log(ReportCategory.MULTIPLAYER_MOVEMENT, $"Movement from {@for} - {fullMovementMessage}");
    incomingQueue.Enqueue((@for, fullMovementMessage));
}
```

`incomingQueue` is a `ConcurrentQueue` — thread-safe for enqueue. Everything after this point runs on the main thread.

`DrainToEntities()` is called every frame from `MultiplayerProfilesSystem.Update()`. It:
1. Dequeues every pending `(wallet, message)` pair.
2. Looks up the entity via `IReadOnlyEntityParticipantTable.TryGet(wallet, …)`.
3. On hit, calls `RemotePlayerMovementComponent.Enqueue(message)` on that entity — pushing the message into its per-entity priority queue.
4. On miss (wallet not in table yet — movement arrived before the participant's Join was processed), stashes the latest message in a `pendingMessages` dictionary keyed by wallet.

`TryFlushPending(walletId)` / `RemovePending(walletId)` are called by `RemoteEntities` when a participant registers or leaves — flushing the stashed message into the entity's queue, or discarding it.

> **Warning:** `pendingMessages` only retains the **latest** message per wallet (the last `Enqueue` wins). A transient miss is treated as "catch up on the current position" rather than replaying every message that arrived before the join was seen. Same applies to the main-thread branch — messages are enqueued in timestamp order and the priority queue truncates at `MAX_MESSAGES = 10` on `RemotePlayerMovementComponent.Enqueue`.

### `RemotePlayerMovementComponent`

`Movement/Components/RemotePlayerMovementComponent.cs` is the per-entity receive state:

- `Queue` — a pooled `SimplePriorityQueue<NetworkMovementMessage, double>` ordered by `timestamp`. Capacity cap: 10 messages; oldest are dequeued if exceeded.
- `PastMessage` — the last message that was fully applied (interpolation finished or teleport); the source of truth for "where/what the avatar was doing just before the current step".
- `Initialized` / `WasTeleported` / `WasPassedThisFrame` — stage flags used by `RemotePlayerAnimationSystem`.
- `InitialCooldownTime` — accumulated seconds since first message, compared against `2 * settings.MoveSendRate` to delay the first interpolation.
- Head IK / point-at mirror fields (`HeadIKYawEnabled`, `HeadIKPitchEnabled`, `HeadIKYawAndPitch`, `IsPointingAt`, `PointAtWorldHitPoint`) — smoothed separately on each frame via `Interpolation.InterpolateHeadIK` / `InterpolatePointAtIK` rather than via the main position-interpolation spline.
- `Dispose()` returns the priority queue to its pool; called from `CleanUpRemoteMotionSystem` on entity delete.

### `RemotePlayersMovementSystem` — the stage machine

`Movement/Systems/RemotePlayersMovementSystem.cs` runs in `PresentationSystemGroup`. Its query excludes `PlayerComponent` (local player), `PBAvatarShape` (scene-defined avatars, not remote players), and `DeleteEntityIntention`.

Each frame, per entity, the system runs this cascade (returning early between stages):

1. **First message** — if `!Initialized && queue.Count > 0`, dequeue the first message, **snap** transform to its position/rotation, apply head IK / point-at, mark `Initialized = true` with `WasTeleported = true`, and return. No interpolation on the first frame.

2. **Initial cooldown** — wait until `InitialCooldownTime ≥ 2 × MoveSendRate` (default 0.2s). This lets the inbox accumulate a second message before the system starts interpolating between them — avoids a first interpolation of almost zero duration.

3. **Continuous IK smoothing** — head yaw/pitch and point-at are smoothed *every frame* using per-axis lerp factors (`InterpolationSettings.HeadIKInterpolationFactor`, `PointAtIKInterpolationFactor`). This runs regardless of the stages below because IK changes shouldn't wait on the movement spline.

4. **Continue interpolation** — if `InterpolationComponent.Enabled`, advance the spline by `deltaTime`. If it completes within this frame, `AddPassed` the end message and fall through to the next stage with leftover time; otherwise return.

5. **Filter stale messages** — drop any messages in the queue whose `timestamp ≤ PastMessage.timestamp` (they arrived out of order or after a teleport).

6. **Extrapolate when the inbox is empty** — if `settings.UseExtrapolation && queue.Count == 0 && Initialized && !WasTeleported && !PastMessage.isInstant`, and the last message's speed exceeds `ExtrapolationSettings.MinSpeed`:
   - Start extrapolation if not already running (`extComp.Restart(PastMessage, TotalMoveDuration)`).
   - Advance `Extrapolation.Execute(deltaTime, ref transComp, ref extComp, ExtrapolationSettings)` and return.

7. **New message arrived** (`HandleNewMessage`):
   - If extrapolation was running, try to stop it cleanly via `TryStopExtrapolation` — filters up to `BEHIND_EXTRAPOLATION_BATCH` messages whose timestamp is *behind* the extrapolated position (to prevent running backward in time). If a future message is available, synthesize a local `NetworkMovementMessage` at the extrapolated position/time and mark the next interpolation as a `isBlend` transition.
   - If the new message is *very far* (`posDiff > MinTeleportDistance`) or (with speed-up enabled) has effectively the same pose as the current `PastMessage`, call `TeleportFiltered` — snap to the message, mark `WasTeleported = true`, and pop further near-identical messages from the queue.
   - Otherwise `StartInterpolation`: pick a spline, `intComp.Restart`, and optionally `SpeedUpForCatchingUp` or `SlowDownBlend` (see next topic).

The key flag produced each frame is `RemotePlayerMovementComponent.WasPassedThisFrame`, set by `AddPassed` when a message is fully consumed. The animation system consumes this to trigger one-shot animator state changes.

### `RemotePlayerAnimationSystem`

`Movement/Systems/RemotePlayerAnimationSystem.cs` runs in `PresentationSystemGroup` with `[UpdateAfter(typeof(RemotePlayersMovementSystem))]`. Query excludes `PlayerComponent`, `HiddenPlayerComponent`, and `DeleteEntityIntention`.

Three branches per frame:

- **Just-passed message** (`WasPassedThisFrame == true`) — consume the flag and call `UpdateAnimations`. This copies `PastMessage.animState` onto `CharacterAnimationComponent.States`, triggers `JUMP` on a jump-count increase, `GlideStateValue.OPENING_PROP` on glide-start, and requests `StopEmote` if the previous message was emoting but the new one isn't.
- **Interpolation active** — lerp `MovementBlendValue` and `SlideBlendValue` between `intComp.Start.animState` and `intComp.End.animState` using `intComp.Time / intComp.TotalDuration`. A special sub-case — `BlendBetweenTwoZeroMovementPoints` — handles the case where both endpoints are idle but the player visibly translated, inferring a mid-point blend from distance/duration.
- **No interpolation** — damp `MovementBlendValue` / `SlideBlendValue` toward zero at `movementSettings.IdleSlowDownSpeed` (seconds to reach idle).

`AnimateFutureJump` is a look-ahead trick: when the interpolation target has `JumpCount > current` and the Y-delta between start and end exceeds `JUMP_EPSILON`, the animator fires the jump trigger **at the start of the interpolation** (not when the end message is finally passed), so the avatar's jump animation matches the visible arc.

### `CleanUpRemoteMotionSystem`

`Movement/Systems/CleanUpRemoteMotionSystem.cs` runs in `CleanUpGroup`. When an entity has both `RemotePlayerMovementComponent` and `DeleteEntityIntention` (with `DeferDeletion == false`), it calls `Dispose()` on the component — returning the priority queue to its pool so it can be reused by the next remote player that joins.

---

## Interpolation & Extrapolation

The receive pipeline produces `InterpolationComponent` / `ExtrapolationComponent` states and the static helpers `Interpolation.Execute` / `Extrapolation.Execute` do the per-frame transform work. Spline math lives in `Movement/InterpolationSpline.cs`.

### Components

**`InterpolationComponent`** — one "in-flight interpolation" between two timestamps:

```csharp
public struct InterpolationComponent
{
    public NetworkMovementMessage Start, End;
    public float Time, TotalDuration;
    public InterpolationType SplineType;
    public bool UseMessageRotation;
    public bool Enabled { get; private set; }
    public double Present => Start.timestamp + Time; // current interpolated timestamp
}
```

- `Restart(from, to, splineType, controllerSettings)` zeros `Time` and sets `TotalDuration = End.timestamp - Start.timestamp` — so the duration is literally the time gap between the two messages. It also precomputes the animation blend targets on `End.animState`.
- `UseMessageRotation` defaults to `true`; `AccelerateVerySlowTransition` in `RemotePlayersMovementSystem` keeps it true but synthesizes a slower pseudo-motion that avoids teleport-looking interpolation across very long gaps.

**`ExtrapolationComponent`** — fired when the inbox runs dry:

```csharp
public struct ExtrapolationComponent
{
    public NetworkMovementMessage Start;
    public Vector3 Velocity;
    public float Time, TotalMoveDuration;
    public bool Enabled { get; private set; }
}
```

- `Restart(from, moveDuration)` captures the last message and its velocity.
- `TotalMoveDuration` comes from `ExtrapolationSettings.TotalMoveDuration = LinearTime + LinearTime * DampedSteps` (default `0.33 + 0.33 = 0.66s`).

### Spline variants — `InterpolationSpline`

`Movement/InterpolationSpline.cs` exposes seven spline functions, each `(start, end, time, totalDuration) → Vector3`:

| `InterpolationType` | Method | Behavior |
|---|---|---|
| `Linear` | `Vector3.Lerp` | Used as fallback for near-zero velocity, grounded-state toggles, or `MovementKind.IDLE` on either endpoint — see "Fallback rule" below. |
| `Hermite` | Cubic Hermite | Matches start/end positions **and** velocities via the standard four Hermite basis functions. |
| `MonotoneYHermite` | Hermite with Y monotonicity | Clamps start/end Y velocities so the spline cannot overshoot past the target on the vertical axis. Useful when jumps/falls would otherwise produce a visible "rubber band". |
| `FullMonotonicHermite` | Hermite with X/Y/Z monotonicity | Same clamp applied to all three axes. |
| `Bezier` | Cubic Bézier | Control points are `start.position + start.velocity × totalDuration/3` and `end.position − end.velocity × totalDuration/3`. |
| `VelocityBlending` | Projective Velocity Blending | From Murphy & Lengyel, *Believable Dead Reckoning for Networked Games* (Game Engine Gems 2, 2011). Lerps velocity first, then blends a local projection against a reconstructed remote projection. Smooth velocity transitions, no position overshoot. |
| `PositionBlending` | Projective Position Blending | Same paper. Uses constant start-velocity for the local projection (no velocity lerp). Slightly less responsive, more stable. |

Monotonizing logic (`InterpolationSpline.Monotonize`) checks whether the target is above or below the start and clamps both endpoint velocities into the matching sign, further capping them at `(end - start) / Δt`. This prevents Hermite's natural tendency to overshoot when start and end velocities disagree sharply.

### `Interpolation.Execute`

```csharp
public static float Execute(float deltaTime, ref CharacterTransform transComp,
                            ref InterpolationComponent intComp,
                            float lookAtTimeDelta, float rotationSpeed)
```

Per frame:
1. `intComp.Time += deltaTime`.
2. If `!isInstant` (i.e. `!End.isInstant && Time < TotalDuration`): compute `position` via `DoTransition(SplineType)`; sample the spline again at `Time + lookAtTimeDelta` (default 0.003s into the future) to derive a look direction. The avatar always looks slightly ahead of its current frame along the interpolation curve.
3. If `isInstant` (end message forces a snap, or interpolation just finished): set `position = End.position`, compute `remainedDeltaTime = TotalDuration - (Time - deltaTime)` and return it — `RemotePlayersMovementSystem` uses the leftover time to start the next interpolation this same frame.
4. `LookAt` flattens the look direction to the XZ plane, rotates via `Quaternion.RotateTowards(rotationSpeed × dt)` (or snaps if `isInstant`), and — when `UseMessageRotation` is true — overrides the computed yaw with the message's `rotationY`. The pitch/roll are derived from the spline direction.

The function returns **`-1`** while interpolation is still ongoing and **`remainedDeltaTime`** when it completes — this is what lets the stage machine chain into the next stage within a single frame.

### Fallback rule: when `Linear` wins

Inside `StartInterpolation`, the selected spline is downgraded to `Linear` when **any** of these is true:
- `PastMessage.velocitySqrMagnitude < ZERO_VELOCITY_SQR_THRESHOLD`
- `remote.velocitySqrMagnitude < ZERO_VELOCITY_SQR_THRESHOLD`
- `PastMessage.animState.IsGrounded != remote.animState.IsGrounded`
- `PastMessage.animState.JumpCount != remote.animState.JumpCount`
- Either endpoint has `MovementKind.IDLE`

All of these are scenarios where a curved spline would visibly misbehave — velocity-driven splines amplify near-zero noise into sideways drift; cross-ground transitions want a straight line; idle transitions are logically snaps.

If `InterpolationSettings.UseBlend && isBlend` (coming out of extrapolation), the `BlendType` from settings wins instead — typically a different spline than the normal `InterpolationType`, chosen for smooth resumption.

### `Extrapolation.Execute`

```csharp
public static void Execute(float deltaTime, ref CharacterTransform transComp,
                           ref ExtrapolationComponent ext,
                           RemotePlayerExtrapolationSettings settings)
```

Per frame, while the inbox is empty:
1. `ext.Time += deltaTime`.
2. `ext.Velocity = DampVelocity(ext.Start.velocity, Time, TotalMoveDuration, LinearTime)`:
   - For `Time ≤ LinearTime` (default 0.33s) → unchanged velocity (pure linear extrapolation).
   - For `LinearTime < Time < TotalMoveDuration` → `Vector3.Lerp(velocity, 0, (Time - LinearTime) / (TotalMoveDuration - LinearTime))` — linearly damped toward zero.
   - For `Time ≥ TotalMoveDuration` → zero.
3. If `|velocity|² > MinSpeed`, advance `transform.position += velocity × deltaTime`, with a **floor clamp**: if the new Y and current Y have opposite signs, set `newPosition.y = 0` and `velocity.y = 0` (prevents extrapolating through the ground when the last known velocity pointed downward).

Extrapolation is **not restarted** automatically when it expires — the stage machine simply stops advancing the avatar until a new message arrives.

### Catch-up — speeding up interpolation when behind

`RemotePlayersMovementSystem.SpeedUpForCatchingUp` runs once when a new interpolation starts, if the inbox is above a threshold:

```csharp
if (inboxMessages > settings.InterpolationSettings.CatchUpMessagesMin) // default 3
{
    float correctionTime = inboxMessages * UnityEngine.Time.smoothDeltaTime;
    intComp.TotalDuration = Mathf.Max(
        intComp.TotalDuration - correctionTime,
        intComp.TotalDuration / settings.InterpolationSettings.MaxSpeedUpTimeDivider);
}
```

Shortens `TotalDuration` proportional to how many messages are stacked up, floored at `TotalDuration / MaxSpeedUpTimeDivider` (default `1.0` — catch-up is effectively disabled in the shipping asset unless this is tuned up).

### Blend slow-down after extrapolation

`SlowDownBlend` runs after a successful `TryStopExtrapolation` that produced an `isBlend`-flagged interpolation. If the implied speed (`distance / duration`) exceeds `MaxBlendSpeed` (default 5 m/s), it **lengthens** the duration so the avatar doesn't visibly jump to the new target:

```csharp
if (speed > maxBlendSpeed)
    intComp.TotalDuration = positionDiff / maxBlendSpeed;
```

Combined with `TryStopExtrapolation`'s filtering of behind-in-time messages, this gives a smooth hand-off from "moving forward on inertia" to "caught up to the latest authoritative position".

### Head IK and point-at IK

These are not driven by the spline. `Interpolation.InterpolateHeadIK` uses a `Quaternion.Slerp` with factor `HeadIKInterpolationFactor × Time.deltaTime` (default factor 10 → ~10 rad/s snap speed); `InterpolatePointAtIK` uses `Vector3.Lerp` with `PointAtIKInterpolationFactor`. They run every frame regardless of whether a movement interpolation is active, letting the head/hand track independently of the body.

---

## Remote Players: Entities, Profiles, and Lifecycle

This layer owns everything about "who the remote players are and what ECS entities represent them". Inputs come from both transports' announcement and remove-intention streams; outputs are fully populated ECS entities ready for avatar rendering.

### `RoomSource` — presence-source flags

`Connections/Rooms/RoomSource.cs` is a `[Flags]` byte enum that identifies where a participant is currently visible from:

```csharp
[Flags]
public enum RoomSource : byte
{
    NONE       = 0,
    GATEKEEPER = 1,       // LiveKit Scene room (GateKeeper)
    ISLAND     = 1 << 1,  // LiveKit Island room (Archipelago)
    CHAT       = 1 << 2,  // LiveKit Chat room (metadata only, not player presence)
    PULSE      = 1 << 3,  // Pulse peer transport
}
```

Every profile announcement, profile download, and disconnect intent carries a `FromRoom` field — critical for correct lifecycle: a player visible via both Island (LiveKit) and Pulse must stay alive when one source drops. See `EntityParticipantTable.Release` below.

### DTOs on the receive path

All three are transport-neutral `readonly struct`s carrying a `WalletId` and a `RoomSource FromRoom`:

| DTO | File | Fields | Meaning |
|---|---|---|---|
| `RemoteAnnouncement` | `Profiles/Announcements/RemoteAnnouncement.cs` | `Version`, `WalletId`, `FromRoom` | "Participant *X* advertises profile version *V* on room *R*; fetch it if you don't already have that version." |
| `RemoveIntention` | `Profiles/RemoveIntentions/RemoveIntention.cs` | `WalletId`, `FromRoom` | "Participant *X* is no longer present on room *R*." Implements `IEquatable` so `HashSet` dedupes across transports. |
| `RemoteProfile` | `Profiles/RemoteProfiles/RemoteProfile.cs` | `Profile`, `WalletId`, `FromRoom` | A downloaded profile ready to be applied to an ECS entity. |

### `EntityParticipantTable` — bidirectional wallet ↔ entity map

`Profiles/Tables/EntityParticipantTable.cs` implements `IEntityParticipantTable` — it is the single source of truth for "does this wallet have an ECS entity, and which one".

```csharp
public class EntityParticipantTable : IEntityParticipantTable
{
    private readonly Dictionary<string, IReadOnlyEntityParticipantTable.Entry> walletIdToEntity = new(PoolConstants.AVATARS_COUNT);
    private readonly Dictionary<Entity, string> entityToWalletId = new(PoolConstants.AVATARS_COUNT);
    // ...
}
```

Entries carry `(WalletId, Entity, RoomSource ConnectedTo)` where `ConnectedTo` is an OR of every room the participant is currently known on:

- `Register(walletId, entity, fromRoom)` — first sight; creates entry.
- `AddRoomSource(walletId, fromRoom)` — later sighting from a different room; OR-in the flag.
- `Release(walletId, fromRoom)` — remove one room from the entry. Returns **`true` only when `ConnectedTo == RoomSource.NONE`** (last source gone) — in which case both dictionaries drop the entry. Callers use this boolean to decide whether to also send `DeleteEntityIntention` to the ECS entity.

> **Warning:** `EntityParticipantTable` is **not thread-safe** — the class doc-comment says so explicitly. All access must be from the main thread. Off-thread disconnect events from LiveKit and Pulse go through `*RemoveIntentions` implementations that buffer into thread-safe collections first; see [Deduplication & Concurrency Primitives](#deduplication--concurrency-primitives) (next topic).

### `RemoteProfiles` — announcement → profile download

`Profiles/RemoteProfiles/RemoteProfiles.cs` consumes `IReadOnlyCollection<RemoteAnnouncement>` and produces a list of `RemoteProfile` via `Bunch()`. Responsibilities:

- **Version-aware dedup** — for each wallet it tracks an in-flight `PendingRequest(version, cts, startedAt, fromRoom)`. A new announcement with a *lower or equal* version is merged into the existing request (adding any new `RoomSource` flag); a *higher* version cancels the old CTS and supersedes it.
- **Lambdas endpoint resolution** — asks `IRemoteMetadata.GetLambdaDomainOrNull(walletId)` for the profile server URL (the remote participant advertises it via LiveKit room metadata — see [livekit-networking.md](livekit-networking.md) for how that metadata arrives).
- **Catalyst fallback** — calls `IProfileRepository.GetAsync(..., FetchBehaviour.DELAY_UNTIL_RESOLVED)`, which routes around missing catalyst responses so failed lookups don't re-trigger every frame.
- **Main-thread discipline** — finishes with `await UniTask.SwitchToMainThread()` before touching the pending-dictionary, so the class itself remains single-thread-safe after the async hop.

The collected `RemoteProfile` list is consumed by `RemoteEntities.TryCreateOrUpdate` (below).

### `RemoteEntities` — ECS entity lifecycle

`Profiles/Entities/RemoteEntities.cs` is the factory/reaper for remote-player entities on the global ECS world. It owns two object pools (`Transform` and `RemoteAvatarCollider`) and the wallet → `RemoteAvatarCollider` lookup table.

**Entity creation** — `TryCreateOrUpdateRemoteEntity(profile, world)`:
1. If the wallet is already in `EntityParticipantTable` → `UpdateCharacter`: set the new `Profile` on the existing entity only if the version differs. Mark `IsDirty = true` so downstream avatar systems rebuild.
2. If not → `CreateCharacter`:
   - Acquire a pooled `Transform`, named `REMOTE_ENTITY_{walletId}`; under `#if UNITY_EDITOR`, parented to a `REMOTE_ENTITIES` GameObject for inspection.
   - Acquire a pooled `RemoteAvatarCollider`, parent to the transform.
   - Build the full component set on the entity: `Profile`, `RemoteAvatarCollider`, `CharacterTransform`, `CharacterAnimationComponent`, `CharacterEmoteComponent`, `RemotePlayerMovementComponent` (with pooled queue), `InterpolationComponent`, `ExtrapolationComponent`, `HeadIKComponent`, `HandPointAtComponent`, `TorsoIKComponent`.
   - Register with `IEntityCollidersGlobalCache.Associate(collider, entity)` so raycasts resolve back to the entity.
   - Register with `EntityParticipantTable` and **flush any pending movement** via `movementInbox.TryFlushPending(walletId)` (covers the race where movement arrives before the profile announcement completes).
3. In both paths, call `EntityParticipantTable.AddRoomSource(walletId, profile.FromRoom)`.

**Entity removal** — `Remove(walletId, roomSource, world)`:
1. `EntityParticipantTable.Release(walletId, roomSource)` — only proceeds to teardown when the participant has no remaining rooms.
2. `movementInbox.RemovePending(walletId)` — drop any stashed pre-profile movement.
3. Pool-release the `RemoteAvatarCollider` and unregister it from the global collider cache.
4. Add `DeleteEntityIntention` to the entity — the rest of the ECS teardown (avatar systems, `CleanUpRemoteMotionSystem`, etc.) takes it from there.

### `RemoteAvatarCollider`

`Profiles/Entities/RemoteAvatarCollider.cs` is a trivial `MonoBehaviour` holding a single `Collider` — one instance per remote player, pooled by `RemoteEntities`. The collider is what scene raycasts hit when probing "which avatar is this?".

### `MultiplayerProfilesSystem` — the per-frame orchestrator

`Profiles/Systems/MultiplayerProfilesSystem.cs` runs in `PresentationSystemGroup`, `[UpdateBefore(AvatarGroup)]`. Its comment documents the intent:

> 1. receive signal announce profile
> 2. fetch the profile
> 3. assign profile to the entity
> 4. auto flow of avatar

Each `Update(t)`:

```csharp
movementInbox.DrainToEntities();

if (realFlowLoadingStatus.CurrentStage.Value is not LoadingStage.Completed) return;
if (!realmData.Configured) return;

remoteMetadata.BroadcastSelfMetadata();
remoteMetadata.BroadcastSelfParcel(characterObject);
remoteProfiles.Download(remoteAnnouncements);
remoteEntities.TryCreate(remoteProfiles, World);
RemoteEntitiesExtensions.Remove(remoteEntities, remoteAnnouncements, removeIntentions, World);
profileBroadcast.NotifyRemotes();
```

- The **first** call drains the thread-safe `MovementInbox` regardless of loading state — so movement doesn't pile up unbounded while the client is still loading.
- Everything after is gated by the loading stage and realm configuration.
- `remoteAnnouncements` and `removeIntentions` here are the proxy implementations from `MultiplayerContainer` — they merge Pulse and LiveKit sources transparently.

> **Note:** `RemoteMetadata` (the class passed as `remoteMetadata` here) is **LiveKit-specific** — it subscribes to `IRoomHub.IslandRoom()` / `SceneRoom()` participant metadata events and calls `UpdateLocalMetadata` on those rooms to advertise the self parcel and lambdas endpoint. It stays documented in [livekit-networking.md](livekit-networking.md) because it reaches directly into LiveKit's participant model; the `IRemoteMetadata` interface that `MultiplayerProfilesSystem` depends on is transport-agnostic.

---

## Deduplication & Concurrency Primitives

Because LiveKit and Pulse both deliver the same logical events, and because network callbacks arrive off the main thread, the multiplayer layer leans on a small set of shared primitives for deduplication and thread handoff.

### `MessageDeduplication<T>` — hash-set deduplication

`Deduplication/MessageDeduplication.cs` implements `IMessageDeduplication<T>` (interface declared in `DCL.Chat.MessageBus.Deduplication` — shared with chat) and exposes a `TryPass(walletId, timestamp) → bool` extension that atomically "add if absent":

```csharp
public class MessageDeduplication<T> : IMessageDeduplication<T>
    where T : IComparable<T>, IEquatable<T>
{
    private readonly ISet<RegisteredStamp> registeredStamps = new HashSet<RegisteredStamp>();
    private readonly TimeSpan cleanPerPeriod; // default 5 minutes

    public bool Contains(string walletId, T timestamp) =>
        registeredStamps.Contains(new RegisteredStamp(walletId, timestamp));

    public void Register(string walletId, T timestamp)
    {
        if (DateTime.Now - previousClean > cleanPerPeriod)
        {
            previousClean = DateTime.Now;
            registeredStamps.Clear();
        }
        registeredStamps.Add(new RegisteredStamp(walletId, timestamp));
    }
}
```

Key quirks:
- The set is **wholesale cleared** every `cleanPerPeriod` (default 5 min) rather than evicting individual old entries. Simpler and cheaper at the cost of allowing a replay within the first N seconds after a clear.
- Not thread-safe on its own — `HashSet<T>.Add` is not safe for concurrent writers. Callers wrap it in a lock or only use it from the main thread.
- Generic over `T` so it dedupes by `(wallet, timestamp)` pairs, `(wallet, messageId)` pairs, etc. — the key is just "whatever identifier uniquely stamps the message".

### `EmotesScheduler` — monotonic-timestamp deduplication

`Emotes/EmotesDeduplication.cs` (file name differs from class name) defines `EmotesScheduler`, a different dedup strategy:

```csharp
public class EmotesScheduler
{
    private readonly Dictionary<string, double> lastProcessedTimestamps = new(PoolConstants.AVATARS_COUNT);

    public bool TryPass(string walletId, double timestamp)
    {
        if (lastProcessedTimestamps.TryGetValue(walletId, out double storedTimestamp))
        {
            if (timestamp < storedTimestamp) return false;
            lastProcessedTimestamps[walletId] = timestamp;
            return true;
        }
        lastProcessedTimestamps.Add(walletId, timestamp);
        return true;
    }
}
```

Instead of tracking every seen stamp, it tracks **only the most recent** per wallet — because emote IDs are monotonically incremented, any older arrival is a reorder or replay and can be dropped. `RemoveWallet` clears the entry on disconnect.

`EmotesScheduler` is currently wired only into `LiveKitEmotesMessageBus`; the Pulse emote path does its own scheduling inside `PulseMultiplayerBus.Emotes.cs`.

### `MutexSync` — scoped mutex

`DCL/PerformanceAndDiagnostics/Optimization/Multithreading/MutexSync.cs` is a thin wrapper around `System.Threading.Mutex` offering a RAII `Scope`:

```csharp
public Scope GetScope()
{
    SAMPLER.Begin();
    var scope = new Scope(this);
    SAMPLER.End();
    return scope;
}

public readonly struct Scope : IDisposable
{
    public Scope(MutexSync mutex) => (this.mutex = mutex).Acquire();
    public void Dispose() => mutex.Release();
}
```

The `CustomSampler("MutexSync.Wait")` wraps scope acquisition, so time spent blocked on the mutex shows up in the Unity Profiler. `MutexSync` is the primitive used by `OwnedBunch<T>` and by the thread-safe `*RemoveIntentions` implementations to hand collections from the network thread to the main thread without races.

### `IBunch<T>`, `Bunch<T>`, `OwnedBunch<T>` — collection handoff

All three live under `Profiles/Bunches/`. They standardize "drain a mutable collection, read it once, then clear it" — used for `RemoveIntention`s, `RemoteEmoteIntention`s, and related per-frame event batches.

```csharp
public interface IBunch<out T> : IDisposable
{
    IReadOnlyCollection<T> Collection();
    bool Available();
}
```

- **`Bunch<T>`** (`Bunches/Bunch.cs`) — wraps a plain `List<T>` with no threading guard. `Dispose()` calls `list.Clear()`. Use only when the producer and consumer are both on the main thread (e.g. `RemoteProfiles.Bunch()`).
- **`OwnedBunch<T>`** (`Bunches/OwnedBunch.cs`) — wraps a `HashSet<T>` plus a `MutexSync`. Construction acquires a `MutexSync.Scope`; `Dispose()` clears the set and releases the mutex:

```csharp
public readonly struct OwnedBunch<T> : IBunch<T> where T : struct
{
    public OwnedBunch(MutexSync ownership, HashSet<T> set)
    {
        this.ownership = ownership.GetScope();
        this.set = set;
    }

    public IReadOnlyCollection<T> Collection() => set;
    public void Dispose() { set.Clear(); ownership.Dispose(); }
}
```

> **Warning:** The collection returned by `Collection()` must not be stored beyond the `using` scope — it is cleared on dispose *and* may be mutated by other threads once the mutex is released. Always loop over it inside the `using` block.

Typical consumer pattern:

```csharp
using OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch();
foreach (RemoveIntention intent in bunch.Collection())
    …
// bunch.Dispose() runs here — collection cleared, mutex released
```

### `MovementInbox`'s concurrent queue

A counterpoint to all of the above: the movement receive path does **not** use `MutexSync`. Instead, `MovementInbox.incomingQueue` is a `System.Collections.Concurrent.ConcurrentQueue<(string, NetworkMovementMessage)>` — enqueued from any thread, drained by the main-thread `MultiplayerProfilesSystem` via `TryDequeue`. Movement messages are small value tuples and arrive at high frequency; lock-free queueing wins. See [Receive Pipeline](#receive-pipeline).

---

## SDK Propagation to Scene Worlds

Multiplayer state lives on the **global ECS world** (one world for the whole client). JS scenes run against their own **scene worlds** (one world per loaded scene) and receive data via CRDT messages. The SDK propagation layer — everything under `Explorer/Assets/DCL/Multiplayer/SDK/` — is the two-hop bridge that moves global-world multiplayer data into each scene's world and then out as CRDT writes.

For the broader cross-world patterns (bridge components, global-from-scene access), see [cross-world-ecs-access.md](cross-world-ecs-access.md).

### Data flow

```
global world                            scene world                       JS scene
────────────                            ───────────                       ────────
CharacterTransform ──────────────────►  SDKTransform ──────────────────►  CRDT PBTransform
  (PlayerTransformPropagationSystem)      (WritePlayerTransformSystem)

Profile ─────────────────────────────►  SDKProfile ───────────────────►  CRDT PBPlayerIdentityData
  (PlayerProfileDataPropagationSystem)    (WritePlayerIdentityDataSystem,
   via CharacterDataPropagationUtility)    WriteAvatarEquippedDataSystem,
                                            WriteSDKAvatarBaseSystem)

CharacterEmoteIntent ────────────────►  AvatarEmoteCommandComponent ──►  CRDT PBAvatarEmoteCommand
  (AvatarEmoteCommandPropagationSystem)   (WriteAvatarEmoteCommandSystem)
```

Three "propagation" systems run on the **global world** and write into the **scene world** of the currently assigned scene. Five "writer" systems run on each **scene world** and write into CRDT via `IECSToCRDTWriter`.

### Reserved CRDT entity IDs

`PlayerCRDTEntitiesHandlerSystem` owns a fixed-size boolean pool sized to `SpecialEntitiesID.OTHER_PLAYER_ENTITIES_TO − OTHER_PLAYER_ENTITIES_FROM`. Each remote player, on first sight, is assigned the next free slot in this range; the ID persists for as long as the player is present and is freed on disconnect.

Two consequences:
- The **main player** always uses `SpecialEntitiesID.PLAYER_ENTITY` — writer systems explicitly exclude that ID from remote-player paths (e.g., `WritePlayerTransformSystem` defers to `WriteMainPlayerTransformSystem`).
- The reserved ID for a given wallet stays stable **across scene transitions for that player**. JS scenes can cache per-entity state and the Scene Runtime won't re-number mid-session.

If all slots are taken, `ReserveNextFreeEntity` returns `-1` and the player is silently skipped on SDK side (they still render as an avatar — they just don't appear in scene CRDT state).

### Bridge components

| Component | Location | Purpose |
|---|---|---|
| `PlayerCRDTEntity` | `SDK/Components/PlayerCRDTEntity.cs`, global world | Holds the reserved `CRDTEntity` id, the current `ISceneFacade?` and the matching `SceneWorldEntity`. `AssignToScene` / `RemoveFromScene` set `IsDirty`. `AssignedToScene` is the guard used by every propagation system. |
| `PlayerSceneCRDTEntity` | `SDK/Components/PlayerSceneCRDTEntity.cs`, scene world | Carries the same `CRDTEntity` id into the scene world so writer systems know which CRDT entity to target. Constructed `IsDirty = true` so the initial batch of CRDT writes fires on the first frame. |
| `AvatarEmoteCommandComponent` | `SDK/Components/AvatarEmoteCommandComponent.cs`, scene world | Pending emote write for the scene's avatar: `PlayingEmote` (URN), `LoopingEmote`, `IsDirty`. |

### Global-world side: propagation systems

All three skip entities with `DeleteEntityIntention`, and all three skip entities that carry `PBAvatarShape` (scene-authored NPCs — their data flow is the opposite direction).

**`PlayerCRDTEntitiesHandlerSystem`** — `PresentationSystemGroup`, `[UpdateAfter(MultiplayerProfilesSystem)]`. Four queries per frame:

1. `RemoveComponentOnPlayerDisconnect` — entity has `DeleteEntityIntention` and `PlayerCRDTEntity`, excluding the host (`PlayerComponent`). Sends `DeleteEntityIntention` to the matching scene-world entity, frees the reserved ID, removes `PlayerCRDTEntity`.
2. `RemoveComponent` — entity has `PlayerCRDTEntity` but no `Profile` — covers the case where the profile was lost but the entity wasn't deleted yet. Same teardown as (1).
3. `ModifyPlayerScene` — entity has `PlayerCRDTEntity` and `CharacterTransform`. Re-resolves the current scene via `IScenesCache.TryGetByParcel(characterTransform.ParcelPosition())`. On change, detaches from the old scene (delete the old scene-world entity) and attaches to the new scene by creating a new scene-world entity with `PlayerSceneCRDTEntity(reservedId)`.
4. `AddPlayerCRDTEntity` — entity has `Profile` and is missing `PlayerCRDTEntity`. Reserves the next ID (or `PLAYER_ENTITY` if it's the host) and attaches `PlayerCRDTEntity(id)`, then resolves the current scene.

> **Note:** `globalPlayerCRDTEntity.AssignToScene` / `RemoveFromScene` set `IsDirty` — downstream systems (profile propagation, writer systems) use that to force a re-write when a player crosses a scene boundary.

**`PlayerTransformPropagationSystem`** — `PreRenderingSystemGroup`. Copies `CharacterTransform.Transform` into a pooled `SDKTransform` on the scene-world entity. Gates:
- Only when `characterTransform.Transform.hasChanged` (Unity's built-in transform-change tracking).
- Only when `playerCRDTEntity.AssignedToScene`.
- Skips `PLAYER_ENTITY`.

Position is stored as world coordinates at this stage — the scene-side writer converts to scene-relative.

**`PlayerProfileDataPropagationSystem`** — `PresentationSystemGroup`, `[UpdateAfter(PlayerCRDTEntitiesHandlerSystem)]`. When `playerCRDTEntity.IsDirty || profile.IsDirty` and `AssignedToScene`, calls `CharacterDataPropagationUtility.CopyProfileToSceneEntity(profile, scene, sceneWorldEntity)` which pool-creates an `SDKProfile` component on the scene entity (or reuses the existing one) and copies the profile into it.

**`AvatarEmoteCommandPropagationSystem`** — `PresentationSystemGroup`, `[UpdateBefore(CharacterEmoteSystem)]`. When a global-world entity has `CharacterEmoteIntent` and `PlayerCRDTEntity`, resolves the emote URN against `IEmoteStorage` (to read the looping flag), then writes (or adds) an `AvatarEmoteCommandComponent` on the scene-world entity with `IsDirty = true`.

**`CharacterDataPropagationUtility`** also exposes `PropagateGlobalPlayerToScenePlayer(globalWorld, globalPlayerEntity, sceneFacade)` — called once at scene startup for the **host** player. It writes into `sceneFacade.PersistentEntities.Player` (the pre-allocated host entity in the scene's world, identity `SpecialEntitiesID.PLAYER_ENTITY`) rather than creating a new entity, and tags it with `PlayerSceneCRDTEntity(PLAYER_ENTITY)` so scene-world writer systems emit host data through the same pipeline.

### Scene-world side: CRDT writer systems

All five run in `SyncedPreRenderingSystemGroup` and write through `IECSToCRDTWriter`. They fire on `IsDirty` flags set by the global-world propagation step. Each also has a `[All(DeleteEntityIntention)]` query that emits `DeleteMessage<TComponent>` for clean teardown.

| System | CRDT message | When it writes | Notes |
|---|---|---|---|
| `WritePlayerTransformSystem` | `PBTransform` (via `ExposedTransformUtils.Put`) | `sdkTransform.IsDirty` | Converts world → scene-relative using `sceneData.Geometry.BaseParcelPosition`. Skips `PLAYER_ENTITY` — main player's transform flows through `WriteMainPlayerTransformSystem` on a different path. |
| `WritePlayerIdentityDataSystem` | `PBPlayerIdentityData` `{ Address, IsGuest }` | `playerCRDTEntity.IsDirty` **or** force on `Initialize()` | `Initialize()` runs a force-write query so every already-registered player receives an identity message when the scene first boots. |
| `WriteAvatarEquippedDataSystem` | `PBAvatarEquippedData` `{ WearableUrns[], EmoteUrns[] }` | `profile.IsDirty` **or** force on `Initialize()` | Reads from `SDKProfile.Avatar.Wearables` / `.Emotes`. Force-write path same as above. Ordered `[UpdateAfter(WritePlayerIdentityDataSystem)]`, `[UpdateBefore(ResetDirtyFlagSystem<SDKProfile>)]`. |
| `WriteSDKAvatarBaseSystem` | `PBAvatarBase` `{ Name, BodyShapeUrn, SkinColor, EyesColor, HairColor }` | `profile.IsDirty` | Only regular-update path — no force-write query. |
| `WriteAvatarEmoteCommandSystem` | `PBAvatarEmoteCommand` `{ EmoteUrn, Loop, Timestamp, IsDirty }` | `emoteCommand.IsDirty && PlayingEmote.NotEmpty` **or** force on `Initialize()` | Uses `AppendMessage` (CRDT grow-only log) rather than `PutMessage` (last-writer-wins) — emotes need to arrive reliably in order. Timestamp is `sceneStateProvider.TickNumber`. Also removes the `AvatarEmoteCommandComponent` on delete. |

All writer queries use static lambdas for the message-mutation callbacks (e.g. `static (pbComponent, data) => { … }`) to avoid closure allocations on the hot path.

### Scene-world cleanup

`CleanUpAvatarPropagationComponentsSystem` runs in `CleanUpGroup` (last in the frame) and resets `IsDirty` to `false` on `SDKProfile`, `PlayerSceneCRDTEntity`, and `AvatarEmoteCommandComponent`.

The system's doc comment calls out *why* it can't use the standard `ResetDirtyFlagSystem<T>`:

> Components local to the client can't be cleaned-up via `ResetDirtyFlagSystem<T>` as their clean-up should not be throttled. Otherwise, components are reported every frame.

`ResetDirtyFlagSystem<T>` is throttled (the SDK's normal path); these components need *prompt* dirty-flag reset so the writer systems don't see them as dirty on the very next frame and re-emit.

---

## Connectivity — Online Users Lookup

`Explorer/Assets/DCL/Multiplayer/Connectivity/` is a **separate, transport-independent path** for answering "who is online right now". It does not use LiveKit rooms or Pulse peers — it's an HTTP fetch against the Archipelago "peers" endpoint and (optionally) per-user World endpoints. Used for social UI, friend-status, and nearby-player widgets, it runs independently of whether the client has any LiveKit rooms connected.

> **Note:** "Archipelago" here means the Archipelago HTTP service — same service whose WebSocket adapter drives the LiveKit Island room, but this is the REST API, not the live connection. See the LiveKit room wiring in [livekit-networking.md](livekit-networking.md) for the WebSocket side.

### `IOnlineUsersProvider`

Two lookup modes:

```csharp
public interface IOnlineUsersProvider
{
    UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(CancellationToken ct);
    UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(IEnumerable<string> userIds, CancellationToken ct);
}
```

- No-arg `GetAsync` — returns everyone currently known to Archipelago.
- `GetAsync(userIds, …)` — filters to a specified wallet set (appended as repeated `id` query parameters on the URL).

### `OnlineUserData`

```csharp
public struct OnlineUserData
{
    public bool IsInWorld => !string.IsNullOrEmpty(worldName);
    public Vector3 position;
    [JsonProperty("world")]  public string? worldName;
    [JsonProperty("wallet")] public string  avatarId;
}
```

`IsInWorld` distinguishes a player standing inside a published World vs. Genesis City — the UI uses it to render a world badge. `position` is a 2D parcel coordinate flattened onto Y=0 (deserialized from the HTTP response via `OnlinePlayersJsonDtoConverter`, which maps `peers[].position[0], position[2]` to `x, z`).

### `ArchipelagoHttpOnlineUsersProvider`

The base provider. It hits `baseUrl` via `IWebRequestController.GetAsync` and deserializes the response through the custom `OnlinePlayersJsonDtoConverter`:

```csharp
public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(CancellationToken ct) =>
    await webRequestController.GetAsync(baseUrl, ct, ReportCategory.MULTIPLAYER)
                              .CreateFromNewtonsoftJsonAsync<List<OnlineUserData>>(serializerSettings: SERIALIZER_SETTINGS);
```

For the filtered variant, a `URLBuilder` builds `baseUrl?id=0xA&id=0xB&…` — note that `URLBuilder` is reused across calls (`.Clear()` resets internal state) to avoid per-call allocation.

### `OnlinePlayersJsonDtoConverter`

The response envelope looks like `{ "peers": [ { "address": "...", "position": [x, y, z] }, … ] }` — not a direct list of `OnlineUserData`. The converter unwraps the `peers` array, maps `address → avatarId`, and drops Y (using `position[0], position[2]` with `Y = 0`). Marked `[Preserve]` so Unity IL2CPP doesn't strip it.

### `WorldInfoOnlineUsersProviderDecorator`

Wraps a base `IOnlineUsersProvider` to also query per-user World endpoints — used because a player can be online **inside a World** (separate server) rather than in Genesis City.

```csharp
public async UniTask<IReadOnlyCollection<OnlineUserData>> GetAsync(IEnumerable<string> userIds, CancellationToken ct)
{
    var alreadyReturnedIds = new HashSet<string>();
    var onlineUsers = (await baseProvider.GetAsync(userIds, ct)).ToList();
    for (var i = 0; i < onlineUsers.Count; i++)
        alreadyReturnedIds.Add(onlineUsers[i].avatarId);

    foreach (string userId in userIds)
    {
        if (alreadyReturnedIds.Contains(userId)) continue; // in-world and in-genesis are mutually exclusive
        // Fetch per-user world URL (baseUrlWorlds with [USER-ID] substituted)
        OnlineUserData worldUserData = await webRequestController.GetAsync(...)
            .CreateFromNewtonsoftJsonAsync<OnlineUserData>();
        if (!string.IsNullOrEmpty(worldUserData.worldName))
            onlineUsers.Add(worldUserData);
    }
    return onlineUsers;
}
```

Two subtleties from the code:
- Mutual exclusion — a player in a World cannot simultaneously be in Genesis City, so the decorator skips World lookups for IDs already returned by Archipelago.
- The no-arg `GetAsync(ct)` path **ignores the World dimension entirely** (World endpoints don't support multi-wallet bulk queries). The decorator is honest about this — the inline comment says *"Using only base one as world user provider doesn't support a multiple wallet request"*.
- `IGNORE_NOT_FOUND` is passed to the web-request controller so a missing user simply returns an empty `OnlineUserData` rather than raising.

The decorator is the production wiring — `IOnlineUsersProvider` is resolved to a `WorldInfoOnlineUsersProviderDecorator(ArchipelagoHttpOnlineUsersProvider(...), ...)` where cross-dimension presence matters.

---

## See Also

- **[Network Synchronization](livekit-networking.md)** — LiveKit transport: rooms, pipes, Archipelago, GateKeeper, voice, chat.
- **[Pulse](pulse.md)** — Pulse transport: ENet, peer identity, protocol, feature-flag gating.
- **[Cross-World ECS Access](cross-world-ecs-access.md)** — bridge components used by SDK propagation systems.
- **[Scene Runtime](scene-runtime.md)** — CRDT writer used by the SDK propagation layer.
- **[Chat](chat.md)** — text chat transport and services.
- **[Locomotion](locomotion.md)** — local character motion feeding `PlayerMovementNetSendSystem`.
