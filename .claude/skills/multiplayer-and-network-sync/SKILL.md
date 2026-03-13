---
name: multiplayer-and-network-sync
description: "Multiplayer networking and player synchronization — LiveKit rooms, movement encoding, interpolation, profile sync, and entity-participant mapping. Use when working with RoomHub, ConnectiveRoom, movement systems (ParcelEncoder, FloatQuantizer, InterpolationSpline), EntityParticipantTable, remote player profiles, emote propagation, or scene/island room architecture."
user-invocable: false
---

# Multiplayer & Network Sync

## Sources

- `docs/network-synchronization.md` -- Entity sync via LiveKit rooms, host vs remote entity semantics

---

## Dual-Room Architecture

`RoomHub` orchestrates four LiveKit rooms: **Island**, **Scene** (GateKeeper), **Chat**, and **VoiceChat**. Island + Scene are "local rooms" for entity synchronization; Chat + VoiceChat are independent.

From `RoomHub.cs`:

```csharp
public class RoomHub : IRoomHub
{
    private readonly IConnectiveRoom archipelagoIslandRoom;
    private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;
    private readonly IConnectiveRoom chatRoom;
    private readonly VoiceChatActivatableConnectiveRoom voiceChatRoom;

    public async UniTask<bool> StartAsync()
    {
        // Starts Island + Scene + Chat in parallel; VoiceChat starts on demand
        (bool, bool, bool) result = await UniTask.WhenAll(
            archipelagoIslandRoom.StartIfNotAsync(),
            gateKeeperSceneRoom.StartIfNotAsync(),
            chatRoom.StartIfNotAsync());
        return result is { Item1: true, Item2: true, Item3: true };
    }

    // Merges participants from Island + Scene (cached per frame)
    public IReadOnlyCollection<string> AllLocalRoomsRemoteParticipantIdentities() { ... }
}
```

### IConnectiveRoom Lifecycle

`ConnectiveRoom` runs an infinite reconnection loop: `PrewarmAsync` -> repeated `CycleStepAsync` with 1s heartbeat. State machine: `Stopped -> Starting -> Running -> Stopping -> Stopped`. Uses `Atomic<State>` for thread-safe state transitions (LiveKit callbacks arrive off main thread). Recovery delay on failure is 5 seconds.

**Why two rooms for entities:** The Scene room connects only to the scene the host is standing in, so only co-located players sync scene-specific CRDT state. The Island room provides global visibility -- remote avatars appear even when the host is in a different scene, but Island data is NOT used for scene CRDT synchronization (bandwidth concern).

---

## Host Entity Semantics (CRITICAL GOTCHA)

> See `docs/network-synchronization.md`

**Host components (`PBIdentityData`, `PBAvatarEquippedData`, `PBAvatarBase`) are ALWAYS present** on the scene from startup through the entire lifecycle -- regardless of whether the host is inside or outside the scene.

**Remote entities behave completely differently:**
- If the scene is NOT the current scene, remote entities cannot be present in CRDT state (even if visually nearby via Island room)
- When the host enters a scene, all remote participants within bounds are propagated as if they just entered
- When the host is within the scene and others enter/leave, it works as expected
- Remote entities themselves are never explicitly deleted; their CRDT entity range is reserved

**Implication for JS scenes:** Host identity components are always queryable. Do not assume their presence means the host is physically inside the scene. Remote player presence accurately reflects co-location only when the host is current.

---

## Movement Encoding & Transmission

### NetworkMovementMessage

The core message struct carrying position, velocity, rotation, animation state, head IK, and movement kind:

```csharp
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

### Encoding Pipeline

- **`FloatQuantizer`** -- Fixed-size quantization via scaled integers. `Compress(value, min, max, bits)` maps a float to an integer with `bits` resolution. `Decompress` reverses.
- **`ParcelEncoder`** -- Flattens 2D parcel coordinates `(x, y)` into a 1D index: `x - MinX + (y - MinY) * width`. Used for position encoding relative to the Genesis City grid.
- **`TimestampEncoder`** -- Circular buffer encoding. Timestamps are compressed modulo `2^TIMESTAMP_BITS * TIMESTAMP_QUANTUM` with wraparound detection on decompression.

### Send Throttling

`PlayerMovementNetSendSystem` caps at **10 messages/sec**. Adaptive send rate: drops to `MoveSendRate` on movement, doubles (up to `StandSendRate`) when idle. Immediate sends on grounded/jump state changes.

```csharp
// From PlayerMovementNetSendSystem.cs -- adaptive rate
if (anythingChanged && sendRate > settings.MoveSendRate)
    sendRate = settings.MoveSendRate;
if (timeDiff > sendRate)
{
    if (!anythingChanged && sendRate < settings.StandSendRate)
        sendRate = Mathf.Min(2 * sendRate, settings.StandSendRate);
    SendMessage(...);
}
```

Messages are sent to both Island and Scene pipes via `MultiplayerMovementMessageBus.Send()`, supporting both compressed (`MovementCompressed`) and uncompressed (`Decentraland.Kernel.Comms.Rfc4.Movement`) schemas.

---

## Movement Interpolation

### InterpolationSpline

Seven interpolation types in `InterpolationSpline`:

| Type | Description |
|------|-------------|
| `Linear` | `Vector3.Lerp` wrapper |
| `Hermite` | Cubic Hermite spline matching start/end positions and velocities |
| `MonotoneYHermite` | Hermite with Y-axis monotonicity (prevents vertical overshoot) |
| `FullMonotonicHermite` | Monotonic on all axes |
| `Bezier` | Cubic Bezier with velocity-derived control points |
| `VelocityBlending` | Projective velocity blending (Murphy & Lengyel 2011) |
| `PositionBlending` | Projective position blending (Murphy & Lengyel 2011) |

### RemotePlayersMovementSystem

Runs in `PresentationSystemGroup`. Processes a priority queue of `NetworkMovementMessage` per remote player:

1. **First message** -- teleport to position
2. **Cooldown** -- wait 2x `MoveSendRate` for stability
3. **Interpolation** -- starts between past and new message using selected spline; falls back to `Linear` when velocity is near-zero or grounded state changes
4. **Extrapolation** -- when no messages arrive and speed > threshold, `ExtrapolationComponent` continues movement along last velocity for up to `TotalMoveDuration`
5. **Blend** -- after extrapolation stops, blends back with speed-limited interpolation

```csharp
// ExtrapolationComponent -- simple velocity continuation
public struct ExtrapolationComponent
{
    public NetworkMovementMessage Start;
    public Vector3 Velocity;
    public float Time, TotalMoveDuration;
    public bool Enabled { get; private set; }

    public void Restart(NetworkMovementMessage from, float moveDuration) { ... }
    public void Stop() { Enabled = false; }
}
```

### Catch-Up Mechanism

When inbox has more messages than `CatchUpMessagesMin`, interpolation duration is shortened:

```csharp
float correctionTime = inboxMessages * Time.smoothDeltaTime;
intComp.TotalDuration = Mathf.Max(
    intComp.TotalDuration - correctionTime,
    intComp.TotalDuration / settings.InterpolationSettings.MaxSpeedUpTimeDivider);
```

---

## Profile & Entity Sync

### EntityParticipantTable

Bidirectional mapping between wallet IDs and ECS entities, with room source tracking. NOT thread-safe -- only accessed from the main thread.

```csharp
public class EntityParticipantTable : IEntityParticipantTable
{
    // walletId <-> Entity, with RoomSource (ISLAND, GATEKEEPER, or both)
    public void Register(string walletId, Entity entity, RoomSource fromRoom);
    public bool Release(string walletId, RoomSource fromRoom);  // returns true if fully disconnected
    public void AddRoomSource(string walletId, RoomSource fromRoom);
}
```

`Release` only removes the entity when `ConnectedTo == RoomSource.NONE` (disconnected from all rooms). This allows a player visible via Island to remain alive when they leave the Scene room.

### ThreadSafeRemoveIntentions

LiveKit participant callbacks fire off the main thread. `ThreadSafeRemoveIntentions` buffers disconnect events using `MutexSync`:

```csharp
public class ThreadSafeRemoveIntentions : IRemoveIntentions
{
    private readonly HashSet<RemoveIntention> list = new ();
    private readonly MutexSync multithreadSync = new();

    // Subscribed to LiveKit events (off main thread)
    private void ParticipantsOnUpdatesFromParticipant(Participant participant,
        UpdateFromParticipant update, RoomSource roomSource)
    {
        if (update is UpdateFromParticipant.Disconnected)
            ThreadSafeAdd(new RemoveIntention(participant.Identity, roomSource));
    }

    // Main thread consumes via OwnedBunch
    public OwnedBunch<RemoveIntention> Bunch() => new(multithreadSync, list);
}
```

### OwnedBunch -- Thread-Safe Collection Access

`OwnedBunch<T>` acquires a `MutexSync.Scope` on construction, exposes the collection for reading, and clears + releases the mutex on `Dispose()`:

```csharp
public readonly struct OwnedBunch<T> : IBunch<T> where T : struct
{
    public OwnedBunch(MutexSync ownership, HashSet<T> set)
    {
        this.ownership = ownership.GetScope();  // acquires lock
        this.set = set;
    }
    public void Dispose() { set.Clear(); ownership.Dispose(); }  // clears + releases
}
```

**Usage pattern:** `using var bunch = removeIntentions.Bunch(); foreach (var item in bunch.Collection()) { ... }`

### ProfileBroadcast

Sends `AnnounceProfileVersion` to both Island and Scene pipes to notify remotes of profile updates. Async: fetches self-profile version before sending via `SendAndDisposeAsync` with `KindReliable`.

---

## SDK Propagation Systems

### PlayerTransformPropagationSystem (Global -> Scene)

Runs in `PreRenderingSystemGroup` on the global world. Copies `CharacterTransform` into the scene world's `SDKTransform`:

```csharp
[Query] [None(typeof(DeleteEntityIntention))]
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

### WritePlayerTransformSystem (Scene -> CRDT)

Converts `SDKTransform` to scene-relative coordinates and writes via `IECSToCRDTWriter`:

```csharp
[Query] [None(typeof(DeleteEntityIntention))]
private void UpdateSDKTransform(in PlayerSceneCRDTEntity playerCRDTEntity, ref SDKTransform sdkTransform)
{
    if (!sdkTransform.IsDirty) return;
    if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;
    ExposedTransformUtils.Put(ecsToCRDTWriter, sdkTransform, playerCRDTEntity.CRDTEntity,
        sceneData.Geometry.BaseParcelPosition, false);
}
```

### WritePlayerIdentityDataSystem (Scene -> CRDT)

Writes `PBPlayerIdentityData` (address + isGuest) on dirty, with force-write on `Initialize()`. Uses `ecsToCRDTWriter.PutMessage<PBPlayerIdentityData>` with a static lambda to avoid closures.

### PlayerProfileDataPropagationSystem (Global -> Scene)

Copies `Profile` data to scene entities via `CharacterDataPropagationUtility` when either the profile or the CRDT entity assignment is dirty.

---

## Cross-References

- **ecs-system-and-component-design** -- System patterns, `ref var` mutation rules, cleanup lifecycle
- **async-programming** -- LiveKit async flows, `CancellationTokenSource` management, `UniTaskVoid` exception handling
- **scene-runtime-and-crdt** -- CRDT bridge (`IECSToCRDTWriter`), scene state machine, `MultiThreadSync`
- **cross-world-ecs-access** -- Global/scene world access, `PlayerCRDTEntity`/`PlayerSceneCRDTEntity` bridge, `ISceneIsCurrentListener`
