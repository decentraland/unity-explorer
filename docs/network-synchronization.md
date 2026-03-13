# Network Synchronization

## Overview

Decentraland Explorer uses [LiveKit](https://livekit.io/) as its real-time transport layer for multiplayer. The networking stack is built around a **dual-room architecture** -- an Island room for global player visibility and a Scene room for CRDT-based scene state synchronization. On top of this, separate rooms handle text chat and voice chat.

The system synchronizes:
- **Player movement** -- position, velocity, rotation, animation state, head IK (10 Hz adaptive send rate with lossy compression)
- **Player profiles** -- identity, avatar equipment, equipped wearables
- **Emotes** -- broadcast to nearby players
- **Scene state** -- CRDT messages between co-located players in the same scene

All entity-level networking is coordinated through `RoomHub`, which manages room lifecycles and merges participant lists from multiple rooms. Movement data flows through a dedicated encoding pipeline (`FloatQuantizer`, `ParcelEncoder`, `TimestampEncoder`) and is interpolated on the receiving end using configurable spline types.

---

## Room Architecture

### RoomHub

`RoomHub` orchestrates four LiveKit rooms and acts as the single entry point for room management:

| Room | Purpose | Lifecycle |
|------|---------|-----------|
| **Island** (Archipelago) | Global player visibility -- avatars appear even across scene boundaries | Starts on login |
| **Scene** (GateKeeper) | CRDT scene state sync -- only connects to the host's current scene | Starts on login, reconnects on scene change |
| **Chat** | Text chat messages | Starts on login |
| **VoiceChat** | Spatial voice chat | Starts on demand |

On startup, Island, Scene, and Chat rooms connect in parallel. VoiceChat starts only when a voice session is active:

```csharp
// From RoomHub.cs
public async UniTask<bool> StartAsync()
{
    (bool, bool, bool) result = await UniTask.WhenAll(
        archipelagoIslandRoom.StartIfNotAsync(),
        gateKeeperSceneRoom.StartIfNotAsync(),
        chatRoom.StartIfNotAsync());

    return result is { Item1: true, Item2: true, Item3: true };
}
```

`RoomHub.AllLocalRoomsRemoteParticipantIdentities()` merges participant identity sets from Island and Scene rooms into a single `HashSet<string>`, cached per frame via `MultithreadingUtility.FrameCount` to avoid repeated allocations.

### ConnectiveRoom Lifecycle

`ConnectiveRoom` is the base class for all persistent room connections. It runs an infinite reconnection loop that survives transient failures:

1. **PrewarmAsync** -- one-time initialization (e.g., fetch connection credentials)
2. **CycleStepAsync** -- repeated heartbeat, called every 1 second
3. On failure, wait 5 seconds (`CONNECTION_LOOP_RECOVER_INTERVAL`) then retry

State machine: `Stopped -> Starting -> Running -> Stopping -> Stopped`

Thread safety is critical because LiveKit callbacks arrive off the main thread. `ConnectiveRoom` uses `Atomic<State>` for all state transitions:

```csharp
// From ConnectiveRoom.cs
private readonly Atomic<IConnectiveRoom.State> roomState = new (IConnectiveRoom.State.Stopped);
private readonly Atomic<IConnectiveRoom.ConnectionLoopHealth> connectionLoopHealth =
    new (IConnectiveRoom.ConnectionLoopHealth.Stopped);
```

When a `DuplicateIdentity` disconnect reason is detected (same wallet connected from another client), the reconnection loop stops entirely rather than entering an infinite reconnect cycle.

### Why Two Entity Rooms

- The **Scene room** connects only to the scene the host is standing in, so only co-located players exchange scene-specific CRDT state. This keeps bandwidth manageable.
- The **Island room** provides global visibility -- remote avatars appear even when the host is in a different scene, preventing the world from looking empty.

> **Warning:** Island data is NOT used for scene CRDT synchronization. Using it would broadcast irrelevant data and consume excessive bandwidth.

---

## Entity Availability: `OnUserEnter` & `OnUserLeave`

### Our User

All components (`PBIdentityData`, `PBAvatarEquippedData`, `PBAvatarBase`) related to the host identity are present on the scene on start-up.

The original necessity of it is described [here](https://github.com/decentraland/unity-explorer/issues/1086).

Thus, these components are present throughout the whole lifecycle of the scene.

> **Warning:** It may lead to unexpected behaviour from the JS perspective: Host Components will be always present, no matter if the user is inside the scene or outside!

### Remote Entities

In turn Remote Entities function completely differently:
- If the scene is not the current scene (the host is not standing there) remote entities can't be present on the scene (in CRDT state)
   - It might be confusing because visually they could be there:
     - In worlds as the same LiveKit room is used for the whole world
     - In Genesis City as remote entities are retrieved from an Island which is not related to the current room
- When the host enters the scene all remote participants standing within the bounds of that scene are propagated as they were just entered that scene:
   - Corresponding `PBIdentityData`, `PBAvatarEquippedData`, `PBAvatarBase` added
   - As a result the initial state of the scene is broadcasted through the `Scene` LiveKit room
- When the host is within scene boundaries and other players enter and leave the scene it works as expected:
   - When the remote participant enters the corresponding components are added
   - When they leave - components are removed

> **Warning:** Entities themselves are never deleted explicitly: their range for remote participants is reserved.

### Why It Works This Way

- `Scene Synchronization` is driven through `Scene` LiveKit room
- `Host` can only have one connection to the `Scene` room at a time so the client connects to the room of the scene the host is currently located on
- So even if remote participants stand on non-current scenes we can't synchronize scene state that is caused by users' actions
- Apart from `Scene` LiveKit room there is an `Island` room
   - We don't use `Island` for scene synchronization as it leads to broadcasting of irrelevant data and it can consume all bandwidth
   - However, we use it in `unity-renderer` as there is no concept of `Scene` rooms. It can lead to additional confusion if two versions are compared face-to-face
   - But we use `Island` to display other participants (as otherwise the world would be empty and users would pop-up out of nowhere when the host enters the scene)

---

## Movement Encoding & Transmission

### NetworkMovementMessage

The core message struct carrying all per-frame player state across the wire:

```csharp
// From NetworkMovementMessage.cs
public struct NetworkMovementMessage : IEquatable<NetworkMovementMessage>
{
    public float timestamp;
    public Vector3 position;
    public Vector3 velocity;
    public float velocitySqrMagnitude;
    public float rotationY;
    public bool headIKYawEnabled, headIKPitchEnabled;
    public Vector2 headYawAndPitch;
    public MovementKind movementKind;
    public AnimationStates animState;
    public byte velocityTier;
    public bool isSliding, isStunned, isInstant, isEmoting;
}
```

Key fields:
- `timestamp` -- `Time.unscaledTime` at send, used for interpolation duration and message ordering
- `velocity` + `velocitySqrMagnitude` -- used by spline interpolation and extrapolation
- `velocityTier` -- quantized speed bucket for animation blending
- `isInstant` -- true on first message or teleport, triggers immediate position snap on receive
- `animState` -- complete animation state (grounded, jumping, falling, gliding, sliding)

### Encoding Pipeline

Three encoders compress `NetworkMovementMessage` into the `MovementCompressed` protobuf schema:

**`FloatQuantizer`** -- Fixed-size quantization via scaled integers. Maps a float in `[min, max]` to an integer with `bits` resolution:

```csharp
// From FloatQuantizer.cs
public static int Compress(float value, float minValue, float maxValue, int sizeInBits)
{
    int maxStep = (1 << sizeInBits) - 1;
    float normalizedValue = (value - minValue) / (maxValue - minValue);
    return Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * maxStep);
}
```

> **Note:** This is lossy compression. The `bits` parameter controls the trade-off between precision and bandwidth. Higher bit counts yield more precision but larger messages.

**`ParcelEncoder`** -- Flattens 2D parcel coordinates `(x, y)` into a 1D index for efficient encoding:

```csharp
// From ParcelEncoder.cs
public int Encode(Vector2Int parcel) =>
    parcel.x - MinX + ((parcel.y - MinY) * width);

public Vector2Int Decode(int index) =>
    new ((index % width) + MinX, (index / width) + MinY);
```

The parcel grid range is based on Genesis City bounds plus terrain border padding.

**`TimestampEncoder`** -- Circular buffer encoding. Timestamps are compressed modulo `2^TIMESTAMP_BITS * TIMESTAMP_QUANTUM`. On decompression, wraparound is detected by comparing against the last timestamp -- if the decompressed value is below 75% of the buffer size relative to the previous timestamp, a full buffer offset is added.

### Send System and Throttling

`PlayerMovementNetSendSystem` runs in `PostRenderingSystemGroup` and enforces a hard cap of **10 messages per second** (`MAX_MESSAGES_PER_SEC`). Within that cap, the actual send rate adapts:

- When movement/rotation/head IK changes are detected, send rate drops to `MoveSendRate` (fastest)
- When idle (no changes), send rate doubles each interval up to `StandSendRate` (slowest)
- Grounded/jump state changes trigger immediate sends regardless of rate

```csharp
// From PlayerMovementNetSendSystem.cs -- adaptive rate logic
if (anythingChanged && sendRate > settings.MoveSendRate)
    sendRate = settings.MoveSendRate;

if (timeDiff > sendRate)
{
    if (!anythingChanged && sendRate < settings.StandSendRate)
        sendRate = Mathf.Min(2 * sendRate, settings.StandSendRate);

    SendMessage(...);
}
```

Change detection checks position (1mm threshold), velocity (1cm/s threshold), rotation (0.1 degree), and head IK angles (1 degree).

### Message Bus and Dual-Pipe Sending

`MultiplayerMovementMessageBus` sends every movement message to **both** the Island and Scene pipes. It also subscribes to incoming messages from both pipes and routes them to the correct remote player's inbox via `EntityParticipantTable`:

```csharp
// From MultiplayerMovementMessageBus.cs
public void Send(NetworkMovementMessage message)
{
    WriteAndSend(message, messagePipesHub.IslandPipe());
    WriteAndSend(message, messagePipesHub.ScenePipe());
}
```

Both compressed (`MovementCompressed`) and uncompressed (`Decentraland.Kernel.Comms.Rfc4.Movement`) schemas are supported. The `UseCompression` setting controls which schema is used for outgoing messages; incoming messages of either type are accepted.

---

## Movement Interpolation

### InterpolationSpline

`InterpolationSpline` provides seven interpolation types, each operating on a pair of `NetworkMovementMessage` (start and end) with time `t` and `totalDuration`:

| Type | Description | When Used |
|------|-------------|-----------|
| `Linear` | `Vector3.Lerp` wrapper | Near-zero velocity, grounded state changes, idle movement |
| `Hermite` | Cubic Hermite spline matching start/end positions and velocities | Default for active movement |
| `MonotoneYHermite` | Hermite with Y-axis monotonicity (prevents vertical overshoot) | Vertical movement scenarios |
| `FullMonotonicHermite` | Monotonic on all axes | When all-axis overshoot must be prevented |
| `Bezier` | Cubic Bezier with velocity-derived control points | Alternative to Hermite |
| `VelocityBlending` | Projective velocity blending (Murphy & Lengyel 2011) | Smooth velocity-aware transitions |
| `PositionBlending` | Projective position blending (Murphy & Lengyel 2011) | Smooth position-aware transitions |

The system falls back to `Linear` interpolation when velocity is near zero, grounded state changes between messages, or either message has `MovementKind.IDLE`. This avoids position overshooting caused by spline math when the player is barely moving.

### Receive Pipeline: RemotePlayersMovementSystem

`RemotePlayersMovementSystem` runs in `PresentationSystemGroup` and processes a priority queue of `NetworkMovementMessage` per remote player. The pipeline follows a strict sequence:

1. **First message** -- teleport to position (snap), mark player as initialized, wait for more messages
2. **Cooldown** -- wait `2 * MoveSendRate` for stability before starting interpolation
3. **Interpolation** -- interpolate between the past (completed) message and the new message using the selected spline type
4. **Extrapolation** -- when no new messages arrive and speed exceeds `MinSpeed`, `ExtrapolationComponent` continues movement along the last known velocity
5. **Blend** -- when a new message arrives during extrapolation, the system blends from the extrapolated position to the new target with speed-limited interpolation

```csharp
// From RemotePlayersMovementSystem.cs -- pipeline decision flow (simplified)
if (!remotePlayerMovement.Initialized && playerInbox.Count > 0)
{
    HandleFirstMessage(...);  // Step 1: teleport
    return;
}

if (remotePlayerMovement.InitialCooldownTime < 2 * settings.MoveSendRate)
{
    remotePlayerMovement.InitialCooldownTime += deltaTime;  // Step 2: cooldown
    return;
}

if (intComp.Enabled)
{
    Interpolate(deltaTime, ...);  // Step 3: continue current interpolation
    return;
}

if (settings.UseExtrapolation && playerInbox.Count == 0 ...)
{
    Extrapolation.Execute(deltaTime, ...);  // Step 4: extrapolate
    return;
}

if (playerInbox.Count > 0)
    HandleNewMessage(deltaTime, ...);  // Step 5: new message arrived, possibly blend
```

### ExtrapolationComponent

When no messages arrive and the remote player was moving above `MinSpeed`, `ExtrapolationComponent` continues movement along the last known velocity for at most `TotalMoveDuration` seconds. It stores the starting `NetworkMovementMessage` and its velocity, advancing `Time` each frame.

When a new message finally arrives, the system filters out messages that are behind the extrapolation timestamp (to avoid running backward), then blends from the current extrapolated position to the new target.

### Catch-Up Mechanism

When the inbox accumulates more messages than `CatchUpMessagesMin` (indicating the receiver is falling behind), the interpolation duration is shortened to catch up:

```csharp
// From RemotePlayersMovementSystem.cs
float correctionTime = inboxMessages * Time.smoothDeltaTime;
intComp.TotalDuration = Mathf.Max(
    intComp.TotalDuration - correctionTime,
    intComp.TotalDuration / settings.InterpolationSettings.MaxSpeedUpTimeDivider);
```

This ensures the remote player's rendered position converges toward real-time without teleporting.

---

## Profile & Entity Sync

### EntityParticipantTable

Bidirectional mapping between wallet IDs and ECS entities, with room source tracking. Two dictionaries provide O(1) lookup in both directions:

```csharp
// From EntityParticipantTable.cs
public class EntityParticipantTable : IEntityParticipantTable
{
    private readonly Dictionary<string, Entry> walletIdToEntity = new (PoolConstants.AVATARS_COUNT);
    private readonly Dictionary<Entity, string> entityToWalletId = new (PoolConstants.AVATARS_COUNT);

    public void Register(string walletId, Entity entity, RoomSource fromRoom);
    public bool Release(string walletId, RoomSource fromRoom);  // true if fully disconnected
    public void AddRoomSource(string walletId, RoomSource fromRoom);
}
```

Each entry tracks which rooms the participant is connected through via `RoomSource` (flags: `ISLAND`, `GATEKEEPER`, or both). `Release` only removes the entity when `ConnectedTo == RoomSource.NONE` -- this allows a player visible via Island to remain alive when they leave the Scene room.

> **Warning:** `EntityParticipantTable` is NOT thread-safe. It must only be accessed from the main thread. For off-thread participant events, use `ThreadSafeRemoveIntentions`.

### ThreadSafeRemoveIntentions

LiveKit participant callbacks (connect/disconnect) fire off the main thread. `ThreadSafeRemoveIntentions` buffers these disconnect events using `MutexSync` so the main thread can consume them safely:

```csharp
// From ThreadSafeRemoveIntentions.cs -- individual participant disconnect
private void ParticipantsOnUpdatesFromParticipant(
    Participant participant, UpdateFromParticipant update, RoomSource roomSource)
{
    if (update is UpdateFromParticipant.Disconnected)
        ThreadSafeAdd(new RemoveIntention(participant.Identity, roomSource));
}

// Main thread consumes via OwnedBunch
public OwnedBunch<RemoveIntention> Bunch() => new(multithreadSync, list);
```

When an entire room disconnects, all remote participants in that room are buffered as remove intentions at once (under both `MutexSync` and a `lock` on the participants collection).

### OwnedBunch -- Thread-Safe Collection Access

`OwnedBunch<T>` is a disposable struct that acquires a `MutexSync.Scope` on construction, exposes the collection for reading, and clears + releases the mutex on `Dispose()`. Usage: `using var bunch = removeIntentions.Bunch(); foreach (var item in bunch.Collection()) { ... }`

> **Warning:** The collection reference must not be stored beyond the `using` scope -- it will be cleared on dispose and mutated by other threads afterward.

### ProfileBroadcast

`ProfileBroadcast` sends `AnnounceProfileVersion` messages to both the Island and Scene pipes to notify remote clients of profile updates. The send is async: it fetches the self-profile version, then sends via `SendAndDisposeAsync` with `KindReliable` delivery. Remote clients receive this version announcement and, if they have a stale or missing profile for that wallet, fetch the updated profile from the content server.

---

## SDK Propagation Systems

These systems bridge the gap between the global ECS world (where multiplayer data lives) and individual scene worlds (where JS scene code runs). For the full cross-world access pattern, see [cross-world-ecs-access.md](cross-world-ecs-access.md).

### PlayerTransformPropagationSystem (Global -> Scene)

Runs in `PreRenderingSystemGroup` on the **global world**. Copies `CharacterTransform` (the rendered avatar position) into the target scene world's `SDKTransform` component. Skips the main player entity (handled separately by `WriteMainPlayerTransformSystem`):

```csharp
// From PlayerTransformPropagationSystem.cs
[Query]
[None(typeof(DeleteEntityIntention))]
private void PropagateTransformToScene(in CharacterTransform characterTransform,
    in PlayerCRDTEntity playerCRDTEntity)
{
    if (!characterTransform.Transform.hasChanged || !playerCRDTEntity.AssignedToScene) return;
    if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;

    World sceneEcsWorld = playerCRDTEntity.SceneFacade!.EcsExecutor.World;

    if (!sceneEcsWorld.TryGet<SDKTransform>(playerCRDTEntity.SceneWorldEntity, out SDKTransform? sdkTransform))
        sceneEcsWorld.Add(playerCRDTEntity.SceneWorldEntity, sdkTransform = sdkTransformPool.Get());

    sdkTransform!.Position.Value = characterTransform.Transform.position;
    sdkTransform.Rotation.Value = characterTransform.Transform.rotation;
    sdkTransform.IsDirty = true;
}
```

> **Note:** Position is in world coordinates at this stage. The conversion to scene-relative coordinates happens in `WritePlayerTransformSystem` on the scene world side.

### WritePlayerTransformSystem (Scene -> CRDT)

Runs in `SyncedPreRenderingSystemGroup` on **each scene world**. Converts `SDKTransform` to scene-relative coordinates and writes it through `IECSToCRDTWriter` so JS scenes receive the data:

```csharp
// From WritePlayerTransformSystem.cs
[Query]
[None(typeof(DeleteEntityIntention))]
private void UpdateSDKTransform(in PlayerSceneCRDTEntity playerCRDTEntity, ref SDKTransform sdkTransform)
{
    if (!sdkTransform.IsDirty) return;
    if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;

    ExposedTransformUtils.Put(ecsToCRDTWriter, sdkTransform, playerCRDTEntity.CRDTEntity,
        sceneData.Geometry.BaseParcelPosition, false);
}
```

Also handles cleanup -- when a `DeleteEntityIntention` is present, it sends a delete message for the `SDKTransform` CRDT component.

### WritePlayerIdentityDataSystem (Scene -> CRDT)

Runs in `SyncedPreRenderingSystemGroup` on **each scene world**. Writes `PBPlayerIdentityData` (wallet address + `isGuest` flag) when dirty, with a force-write on `Initialize()` to ensure all existing players are written at scene start. Uses a static lambda to avoid closure allocations:

```csharp
// From WritePlayerIdentityDataSystem.cs
ecsToCRDTWriter.PutMessage<PBPlayerIdentityData, (string address, bool isGuest)>(
    static (pbComponent, data) =>
    {
        pbComponent.Address = data.address;
        pbComponent.IsGuest = data.isGuest;
    },
    playerCRDTEntity.CRDTEntity, (profile.UserId!, !profile.HasConnectedWeb3));
```

### PlayerProfileDataPropagationSystem (Global -> Scene)

Runs in `PresentationSystemGroup` on the **global world**, after `PlayerCRDTEntitiesHandlerSystem`. Copies `Profile` data to scene entities via `CharacterDataPropagationUtility` when either the profile or the CRDT entity assignment is dirty.

### Data Flow Summary

```
Global World                          Scene World                    JS Scene
-----------                          -----------                    --------
CharacterTransform ──────────────> SDKTransform ─────────────> CRDT Transform
  (PlayerTransformPropagationSystem)   (WritePlayerTransformSystem)

Profile ─────────────────────────> SDKProfile ──────────────> CRDT PBPlayerIdentityData
  (PlayerProfileDataPropagationSystem) (WritePlayerIdentityDataSystem)
```

---

## See Also

- [cross-world-ecs-access.md](cross-world-ecs-access.md) -- Cross-world patterns used by propagation systems, `PlayerCRDTEntity` / `PlayerSceneCRDTEntity` bridge
- [scene-runtime.md](scene-runtime.md) -- CRDT bridge details, `IECSToCRDTWriter`, scene state machine
- [architecture-overview.md](architecture-overview.md) -- ECS world architecture, system groups, global vs scene worlds
