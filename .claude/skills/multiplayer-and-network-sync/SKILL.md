---
name: multiplayer-and-network-sync
description: "Multiplayer networking -- LiveKit rooms, movement encoding, interpolation, profile sync, entity-participant mapping. Use when working with RoomHub, movement systems, EntityParticipantTable, or remote player sync."
user-invocable: false
---

# Multiplayer & Network Sync

## Sources

- `docs/network-synchronization.md` -- Entity sync via LiveKit rooms, host vs remote entity semantics

---

## Dual-Room Architecture

`RoomHub` orchestrates four LiveKit rooms: **Island**, **Scene** (GateKeeper), **Chat**, and **VoiceChat**. Island + Scene are "local rooms" for entity synchronization; Chat + VoiceChat are independent.

`RoomHub.StartAsync()` starts Island + Scene + Chat in parallel; VoiceChat starts on demand. `AllLocalRoomsRemoteParticipantIdentities()` merges participants from Island + Scene (cached per frame).

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

### NetworkMovementMessage Fields

`timestamp`, `position`, `velocity`, `velocitySqrMagnitude`, `rotationY`, `headIKYawEnabled`, `headIKPitchEnabled`, `headYawAndPitch`, `movementKind`, `animState`, `velocityTier`, `isSliding`, `isStunned`, `isInstant`, `isEmoting`.

### Encoding Pipeline

- **`FloatQuantizer`** -- Fixed-size quantization via scaled integers. `Compress(value, min, max, bits)` / `Decompress`.
- **`ParcelEncoder`** -- Flattens 2D parcel coordinates `(x, y)` into a 1D index relative to the Genesis City grid.
- **`TimestampEncoder`** -- Circular buffer encoding with wraparound detection on decompression.

### Send Throttling

`PlayerMovementNetSendSystem` caps at **10 messages/sec**. Adaptive send rate: drops to `MoveSendRate` on movement, doubles (up to `StandSendRate`) when idle. Immediate sends on grounded/jump state changes. Messages are sent to both Island and Scene pipes via `MultiplayerMovementMessageBus.Send()`, supporting both compressed and uncompressed schemas.

---

## Movement Interpolation

### InterpolationSpline Types

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

**Catch-Up:** When inbox has more messages than `CatchUpMessagesMin`, interpolation duration is shortened proportionally to smooth DeltaTime.

---

## Profile & Entity Sync

### EntityParticipantTable

Bidirectional mapping between wallet IDs and ECS entities, with room source tracking (`ISLAND`, `GATEKEEPER`, or both). NOT thread-safe -- only accessed from main thread.

Key API: `Register(walletId, entity, fromRoom)`, `Release(walletId, fromRoom)` (returns true only when `ConnectedTo == RoomSource.NONE`), `AddRoomSource(walletId, fromRoom)`.

`Release` only removes the entity when disconnected from all rooms. This allows a player visible via Island to remain alive when they leave the Scene room.

### ThreadSafe Disconnect Buffering

LiveKit participant callbacks fire off the main thread. `ThreadSafeRemoveIntentions` buffers disconnect events using `MutexSync`. Main thread consumes via `OwnedBunch<RemoveIntention>` which acquires the lock on construction, exposes the collection, and clears + releases on `Dispose()`.

### ProfileBroadcast

Sends `AnnounceProfileVersion` to both Island and Scene pipes. Async: fetches self-profile version before sending via `SendAndDisposeAsync` with `KindReliable`.

---

## SDK Propagation Systems

- **`PlayerTransformPropagationSystem`** (Global -> Scene) -- Copies `CharacterTransform` into the scene world's `SDKTransform` when transform has changed and player is assigned to scene.
- **`WritePlayerTransformSystem`** (Scene -> CRDT) -- Converts `SDKTransform` to scene-relative coordinates and writes via `IECSToCRDTWriter`.
- **`WritePlayerIdentityDataSystem`** (Scene -> CRDT) -- Writes `PBPlayerIdentityData` (address + isGuest) on dirty, with force-write on `Initialize()`. Uses static lambda to avoid closures.
- **`PlayerProfileDataPropagationSystem`** (Global -> Scene) -- Copies `Profile` data to scene entities via `CharacterDataPropagationUtility` when profile or CRDT entity assignment is dirty.

---

## Detailed Reference

For detailed code examples, see [reference.md](reference.md).

---

## Cross-References

- **ecs-system-and-component-design** -- System patterns, `ref var` mutation rules, cleanup lifecycle
- **async-programming** -- LiveKit async flows, `CancellationTokenSource` management, `UniTaskVoid` exception handling
- **scene-runtime-and-crdt** -- CRDT bridge (`IECSToCRDTWriter`), scene state machine, `MultiThreadSync`
- **cross-world-ecs-access** -- Global/scene world access, `PlayerCRDTEntity`/`PlayerSceneCRDTEntity` bridge, `ISceneIsCurrentListener`
