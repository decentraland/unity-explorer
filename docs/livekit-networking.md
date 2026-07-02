# Network Synchronization

## Overview

This page covers the **LiveKit transport** — the original real-time transport layer for multiplayer in Unity Explorer. LiveKit now runs alongside [Pulse](pulse.md) (ENet peer transport); the two are coordinated by `MultiplayerContainer` and documented in the transport-neutral hub, **[multiplayer.md](multiplayer.md)**. Anything below is specific to the LiveKit stack.

Key pieces owned by this layer:

- **Dual-room architecture** — an [Island room](#room-architecture) for global player visibility and a [Scene room](#room-architecture) for CRDT-based scene state synchronization; plus dedicated rooms for text chat and voice chat.
- **`RoomHub` and `ConnectiveRoom` lifecycle** — the reconnection loop, state machine, and atomic state transitions that keep LiveKit rooms alive across transient failures.
- **[Entity availability semantics](#entity-availability-onuserenter--onuserleave)** — how remote avatars appear and disappear from scene CRDT state as the host moves between scenes, and why the Island room is deliberately excluded from scene synchronization.
- **LiveKit-specific implementations** of the transport-neutral interfaces: `LiveKitMovementMessageBus`, `LiveKitEmotesMessageBus`, `LiveKitRemoteAnnouncements`, `LiveKitProfileBroadcast` / `DebounceLiveKitProfileBroadcast` / `LiveKitMessagesBroadcaster`, `LiveKitRemoveIntentions`, and `RemoteMetadata`.
- **Message pipes** — `IMessagePipesHub`, Protobuf message wrapping, throughput measurement.

The movement pipeline (`NetworkMovementMessage`, encoding, send/receive systems, interpolation, profile/entity tables, SDK propagation) is transport-neutral and lives in [multiplayer.md](multiplayer.md). The sections below that still look movement-related are pointers to that hub.

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

## Voice Chat

Voice chat is LiveKit-only — there is no Pulse equivalent. The LiveKit side provides an on-demand room that connects when the user starts a call and disconnects when the call ends. Higher-level call state (incoming/outgoing/connecting/in-call UI, community voice rooms, microphone handling) lives under `Explorer/Assets/DCL/VoiceChat/` and is orchestrated by `VoiceChatOrchestrator` and `VoiceChatRoomManager`; this section only covers the LiveKit plumbing.

### `VoiceChatActivatableConnectiveRoom`

`Connections/Archipelago/Rooms/VoiceChatActivatableConnectiveRoom.cs` implements `IActivatableConnectiveRoom` — a variant of `ConnectiveRoom` designed for on-demand activation rather than permanent background connection. It is one of the four rooms returned by `IRoomHub` (alongside Island, Scene, Chat), but unlike those it does **not** start on login.

Key differences vs. the regular `ConnectiveRoom` lifecycle:

- **Activation model** — `ActivateAsync()` / `DeactivateAsync()` wrap `Start`/`Stop`. `TrySetConnectionStringAndActivateAsync(newConnectionString)` is the usual entry point — it deactivates any current session, sets the connection string, re-activates, and returns `true` if the room reaches `Running`.
- **Connection string, not sign flow** — there is no Archipelago/GateKeeper sign flow; credentials (`Url` + `AuthToken`) are parsed out of a `ConnectionStringCredentials(connectionString)` at connection time. The connection string is produced by the voice-chat backend service and pushed through the orchestrator.
- **Fresh `IRoom` per reconnect** — `CreateFreshRoom()` constructs a new `Room` with fresh `ParticipantsHub`, `TracksFactory`, `DataPipe`, etc. every time `TryConnectToRoomAsync` runs. The existing `InteriorRoom` is reassigned to the fresh instance via `room.Assign(freshRoom, out _)`. This ensures voice reconnects start from a clean track/participant state rather than reusing a half-disposed room.
- **Same heartbeat cadence** — `HEARTBEATS_INTERVAL = 1s`, `CONNECTION_LOOP_RECOVER_INTERVAL = 5s`. `connectionLoopHealth` tracks `Running` / `CycleFailed` / `Stopped`; on `CycleFailed`, the recovery loop waits 5s and retries.
- **`AttemptToConnectState`** — a small atomic state (`NONE` / `SUCCESS` / `ERROR`) used by the UI to surface "connecting…" vs. "connected" vs. "failed" while `StartAsync` is awaiting the first connect.
- **`Null` singleton** — `VoiceChatActivatableConnectiveRoom.Null.INSTANCE` is handed out when voice is disabled (same idiom the other rooms use for no-op fallbacks).

### Audio pipeline

`Connections/Audio/ThreadedAudioRemixConveyor.cs` (and `OptimizedThreadedAudioRemixConveyor.cs`) is the off-main-thread audio mixer that consumes LiveKit's `AudioStreams` from the voice room and feeds Unity `AudioSource`s. The "threaded" part is critical for voice — main-thread hitches during avatar loading or scene streaming would otherwise crackle the audio stream.

### See also

- `Explorer/Assets/DCL/VoiceChat/` — orchestrator, room manager, reconnection manager, UI presenters, microphone handling.
- `VoiceChatConstants` / `VoiceChatSettingsAsset` — tunables.

---

## Movement Encoding & Transmission

### NetworkMovementMessage and encoding pipeline

Moved to **[Multiplayer → `NetworkMovementMessage` & Encoding](multiplayer.md#networkmovementmessage--encoding)** — the message struct, the four-field compressed wire format (`CompressedNetworkMovementMessage`), `FloatQuantizer`, `ParcelEncoder`, and `TimestampEncoder` are transport-neutral and live there.

LiveKit-specific detail: the `LiveKitMovementMessageBus.UseCompression` flag decides whether outgoing messages go over the `MovementCompressed` Protobuf schema (via `NetworkMessageEncoder`) or the uncompressed `Decentraland.Kernel.Comms.Rfc4.Movement` schema. Incoming messages of either shape are accepted regardless.

### Send System and Throttling

Moved to **[Multiplayer → Send System & Adaptive Rate](multiplayer.md#send-system--adaptive-rate)** — `PlayerMovementNetSendSystem`, the 10 Hz cap, adaptive `sendRate` between `MoveSendRate` / `StandSendRate`, change-detection thresholds, and the `SelfSending` debug path are transport-neutral.

### LiveKit Message Bus and Dual-Pipe Sending

`LiveKitMovementMessageBus` sends every movement message to **both** the Island and Scene pipes supplied by `IMessagePipesHub`, and subscribes to incoming messages on both pipes — routing them to the correct remote player's inbox via `EntityParticipantTable`:

```csharp
// From LiveKitMovementMessageBus.cs
public void Send(NetworkMovementMessage message)
{
    WriteAndSend(message, messagePipesHub.IslandPipe());
    WriteAndSend(message, messagePipesHub.ScenePipe());
}
```

Both compressed (`MovementCompressed` via `NetworkMessageEncoder`) and uncompressed (`Decentraland.Kernel.Comms.Rfc4.Movement`) schemas are supported. The `UseCompression` setting on `MultiplayerMovementSettings` controls which schema is used for outgoing messages; incoming messages of either type are accepted.

> **Note:** `LiveKitMessagesBroadcaster` reads the shared `PulseActivation` live on every send. When Pulse is active it sends movement / emotes / profile announcements only to the peers that announced over LiveKit (the rest receive them over Pulse, avoiding double-delivery); when Pulse is absent (disabled or fallen back) it broadcasts to every peer in the rooms.

---

## Movement Interpolation and Extrapolation

Moved to **[Multiplayer → Interpolation & Extrapolation](multiplayer.md#interpolation--extrapolation)** — the seven `InterpolationSpline` variants (Linear, Hermite, MonotoneYHermite, FullMonotonicHermite, Bezier, VelocityBlending, PositionBlending), `Interpolation.Execute` and `Extrapolation.Execute`, the `Linear` fallback rules, the catch-up and blend-slow-down mechanisms, and head-IK / point-at IK smoothing are transport-neutral.

### Receive Pipeline

Moved to **[Multiplayer → Receive Pipeline](multiplayer.md#receive-pipeline)** — `MovementInbox` (thread boundary for incoming messages from both transports), `RemotePlayerMovementComponent`, the stage machine in `RemotePlayersMovementSystem` (first message → cooldown → continue interpolation → filter stale → extrapolate → new message blend/teleport/interpolate), `RemotePlayerAnimationSystem`, and `CleanUpRemoteMotionSystem` are transport-neutral.

---

## Profile & Entity Sync

### Remote players — entities, profiles, lifecycle

Moved to **[Multiplayer → Remote Players: Entities, Profiles, and Lifecycle](multiplayer.md#remote-players-entities-profiles-and-lifecycle)** — `RoomSource` flags, `EntityParticipantTable`, the announcement/remove/profile DTOs, `RemoteProfiles` (version-aware download), `RemoteEntities` (ECS entity factory and reaper), `RemoteAvatarCollider`, and `MultiplayerProfilesSystem` are transport-neutral.

### `LiveKitRemoveIntentions` — off-thread disconnect buffering

LiveKit participant callbacks (connect/disconnect) fire off the main thread. `LiveKitRemoveIntentions` (`Profiles/RemoveIntentions/LiveKitRemoveIntentions.cs`) subscribes to `IRoomHub.IslandRoom()` and `SceneRoom()` participant events and buffers disconnects using `MutexSync` so the main thread can consume them safely through the generic `IRemoveIntentions` interface:

```csharp
// Simplified from LiveKitRemoveIntentions.cs
private void ParticipantsOnUpdatesFromParticipant(
    LKParticipant participant, UpdateFromParticipant update, RoomSource roomSource)
{
    if (update is UpdateFromParticipant.Disconnected)
        ThreadSafeAdd(new RemoveIntention(participant.Identity, roomSource));
}

public OwnedBunch<RemoveIntention> Bunch() => new(multithreadSync, list);
```

When an entire room disconnects, all remote participants in that room are buffered as remove intentions at once (under both `MutexSync` and a `lock` on the participants collection). See [multiplayer.md → Deduplication & Concurrency Primitives](multiplayer.md#deduplication--concurrency-primitives) for `MutexSync`, `OwnedBunch<T>`, and the handoff pattern used here.

`PulseRemoveIntentions` is the Pulse-side equivalent — both implement `IRemoveIntentions`, and `MultiplayerContainer.RemoveIntentionsProxy` unions their outputs before handing them to `MultiplayerProfilesSystem`. See [multiplayer.md → Transport Selection & Wiring](multiplayer.md#transport-selection--wiring).

### `RemoteMetadata` — LiveKit room-metadata broadcast

`Profiles/Poses/RemoteMetadata.cs` is the LiveKit-coupled helper that advertises the host's parcel and lambdas endpoint via LiveKit's per-participant `UpdateLocalMetadata` — and reads the same fields from remote participants' metadata to drive `RemoteProfiles`' lambdas-endpoint lookup. The class:

- Subscribes to `IRoomHub.IslandRoom().Participants.UpdatesFromParticipant` and `SceneRoom().Room().Participants.UpdatesFromParticipant` for `MetadataChanged` / `Connected`.
- Deserializes `IslandMetadata` (`x`, `y`, `lambdasEndpoint`) or `SceneRoomMetadata` (`lambdasEndpoint` only — parcel comes from `IGateKeeperSceneRoom.ConnectedScene.BaseParcel`) as JSON.
- Exposes `IReadOnlyDictionary<string, ParticipantMetadata>` keyed by wallet.
- Sends self-metadata in `BroadcastSelfParcel` / `BroadcastSelfMetadata` — the latter re-sends when the scene room SID changes (so metadata survives room swaps).

The `IRemoteMetadata` interface is transport-agnostic; `RemoteMetadata` is LiveKit-specific. Pulse peers don't participate in this metadata channel today.

### `LiveKitProfileBroadcast` — self-profile-version announcement

`Profiles/BroadcastProfiles/LiveKitProfileBroadcast.cs` sends `AnnounceProfileVersion` messages on both the Island and Scene pipes to notify remote clients of self-profile updates. It's wrapped by `DebounceLiveKitProfileBroadcast` to avoid spam when the profile changes in quick succession. The send is async: it fetches the self-profile version, then sends via `SendAndDisposeAsync` with `KindReliable` delivery. Remote clients receive this version announcement and, if they have a stale or missing profile for that wallet, fetch the updated profile from the content server.

Pulse has a parallel path — `PulseProfilePropagationBus` — which announces `profile.Version` over the Pulse reliable channel. Both transports only ship the version; remote clients fetch the full profile through the normal HTTP profile repository. The difference is the *trigger mechanism*: LiveKit runs on a timer-debounced self-observer, Pulse fires once from `StartPulseMultiplayerStartupOperation` (and any later explicit calls). See [pulse.md → Profile propagation over Pulse](pulse.md#profile-propagation-over-pulse) for details.

---

## SDK Propagation Systems

Moved to **[Multiplayer → SDK Propagation to Scene Worlds](multiplayer.md#sdk-propagation-to-scene-worlds)** — the global-world propagation systems (`PlayerCRDTEntitiesHandlerSystem`, `PlayerTransformPropagationSystem`, `PlayerProfileDataPropagationSystem`, `AvatarEmoteCommandPropagationSystem`), the scene-world CRDT writer systems (`WritePlayerTransformSystem`, `WritePlayerIdentityDataSystem`, `WriteAvatarEquippedDataSystem`, `WriteSDKAvatarBaseSystem`, `WriteAvatarEmoteCommandSystem`), `CleanUpAvatarPropagationComponentsSystem`, the `PlayerCRDTEntity` / `PlayerSceneCRDTEntity` bridge components, and the reserved-entity-ID scheme are all transport-neutral.

---

## See Also

- [cross-world-ecs-access.md](cross-world-ecs-access.md) -- Cross-world patterns used by propagation systems, `PlayerCRDTEntity` / `PlayerSceneCRDTEntity` bridge
- [scene-runtime.md](scene-runtime.md) -- CRDT bridge details, `IECSToCRDTWriter`, scene state machine
- [architecture-overview.md](architecture-overview.md) -- ECS world architecture, system groups, global vs scene worlds
