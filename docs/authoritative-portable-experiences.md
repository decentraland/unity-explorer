# Authoritative servers for Portable Experiences

This page explains how a Portable Experience (PX) loaded with `/loadpx` runs
against an authoritative server (codename Hammurabi), and how the Unity client
was wired to make that work. It covers the end-to-end flow across three
services, the client-side components, the constraints that shaped the design,
the three root causes that had to be solved, and the diagnostics used to find
them.

If you only need the short version: an authoritative PX scene must join its
**own world's scene-level comms room** so the server spawns and the SDK's CRDT
handshake can run over that room — neither of which happens for a PX by default.

For the transport-level background this builds on, see
[LiveKit Networking](livekit-networking.md), [Multiplayer](multiplayer.md), and
[Scene Runtime](scene-runtime.md). For how PX scenes load, see
[IPFS Realms](ipfs-realms.md).

## What "authoritative multiplayer" means here

A scene whose entity definition metadata has `authoritativeMultiplayer == true`
does not run its multiplayer logic peer-to-peer. Instead:

- A server-side runtime (Hammurabi) runs the scene and owns its CRDT state.
- The client routes the scene's CRDT to a single hardcoded LiveKit participant,
  `authoritative-server` (`LiveKitMessagesBroadcaster.AUTH_SERVER_IDENTITY`),
  instead of broadcasting to peers
  (`CommunicationsControllerAPIImplementationBase.SendBinary`).
- The server is spawned on demand when a user **joins the scene's LiveKit
  room**, then joins that room as `authoritative-server` and exchanges CRDT with
  every connected client.

This already works when you teleport directly into an authoritative world. The
work documented here makes it also work when the same world is loaded as a
Portable Experience overlay.

## End-to-end flow

Three services cooperate. The client owns only the first column; the other two
live in separate repositories (`comms-gatekeeper` and `sdk-multiplayer-server`).

```
unity-explorer (client)         comms-gatekeeper            sdk-multiplayer-server
─────────────────────────       ─────────────────          ──────────────────────
PX scene loads (authoritative)
   │
   ├─ connect scene room  ──►  POST /get-scene-adapter
   │  worlds/<realm>/             (signed-fetch, sceneId)
   │  scenes/<sceneId>/comms      │
   │                              ├─ room name encodes sceneId:
   │                              │  {prefix}{world}-{sceneId}
   │                              └─ returns LiveKit token   ──►  (LiveKit join webhook)
   │                                                              room-event-processor:
   │                                                              join carries sceneId
   │                                                              → spawn Hammurabi
   │  ◄──────────────────── authoritative-server joins the same room ◄──┘
   │
   ├─ RealmInfo.isConnectedSceneRoom = true
   │  → SDK emits REQ_CRDT_STATE  ──► authoritative-server
   │  ◄── RES_CRDT_STATE / CRDT ──────┘  (full state, then deltas)
   ▼
 scene synced
```

The decisive detail is that the join must reach the **scene-level** room
(`worlds/<realm>/scenes/<sceneId>/comms`), whose LiveKit room name encodes the
`sceneId`. The world-level room (`worlds/<realm>/comms`) carries no `sceneId`,
so the server's `room-event-processor` logs `Join event missing sceneId` and
never spawns.

## Client architecture

All client code lives in `DCL.Multiplayer` (the comms primitives), `SceneRuntime`
(the scene comms pipe), and `DCL.Plugins` (the system and plugin). No new
assembly was introduced.

| Component | Assembly | Responsibility |
|---|---|---|
| `PortableExperienceSceneRoom` | `DCL.Multiplayer` | A `ConnectiveRoom` that connects to `worlds/<realm>/scenes/<sceneId>/comms` with the `sceneId` in the handshake metadata. Always targets the scene endpoint. |
| `PortableExperienceWorldComms` | `DCL.Multiplayer` | Owns one `PortableExperienceSceneRoom` plus a `MessagePipe` per authoritative PX scene, keyed by sceneId. Exposes `EnsureConnected`, `TryGetRoom`, `IsConnected`, `RetainOnly`. |
| `SceneCommunicationPipe` (extended) | `SceneRuntime` | Gained multi-room routing: `RegisterSceneRoom` / `RetainOnlyRooms`. Outbound, inbound, and `IsSceneConnected` for a registered sceneId route to that scene's PX room instead of the host's current scene room. The host-room path is unchanged. |
| `PortableExperienceWorldCommsSystem` + `PortableExperienceCommsPlugin` | `DCL.Plugins` | Global-world system that, each frame, connects the room for every authoritative PX scene, registers the room with the pipe, and reconciles rooms whose scene unloaded. |
| `WriteRealmInfoSystem` (extended) | `DCL.Plugins` | Reports `RealmInfo.isConnectedSceneRoom == true` when the PX room is connected (not only the host scene room). |

### Wiring and ownership

The shared `SceneCommunicationPipe` is created once in `CommsContainer` (hoisted
out of `SceneSharedContainer` so both the scene factory and the PX plugin share
one instance, avoiding an `ObjectProxy`). `PortableExperienceWorldComms` is also
created in `CommsContainer`, which already has the LiveKit pools the per-room
message pipes need.

The `DCL.Plugins` system is the only place that legally sees both sides: the
rooms (in `DCL.Multiplayer`) and the pipe (in `SceneRuntime`). The dependency
direction is `SceneRuntime → DCL.Multiplayer`, never the reverse, so the service
never references the pipe — the system registers rooms with the pipe instead.

## Constraints that shaped the design

These are the non-obvious rules that ruled out simpler approaches.

- **The comms layer is single-realm.** `RoomHub` holds exactly four fixed rooms
  (Island, Scene, Chat, Voice) tied to the player's current realm. A PX is an
  overlay that does not change the current realm, so its `RealmData.CommsAdapter`
  is captured on `PortableExperienceRealmComponent` but never fed into `RoomHub`.
  A PX therefore needs its **own** room, modeled per-scene rather than added to
  `RoomHub`.
- **PX scenes are excluded from the by-parcel index.** `ControlSceneUpdateLoopSystem`
  stores PX scenes in `portableExperienceScenesByUrn`, not `scenesByParcels`, so
  `IScenesCache.TryGetByParcel` never returns a PX. This is why remote players
  are never propagated into a PX world and why the host scene room is never
  connected to a PX scene.
- **A PX realm reports `SingleScene == true`.** `ECSPortableExperiencesController`
  builds the PX `RealmData` with `WorldManifest.Empty`, so `RealmData.SingleScene`
  is `true`. `GateKeeperSceneRoomOptions.GetAdapterURL` only uses the world
  scene endpoint when `IsWorld() && !SingleScene`, so reusing `GateKeeperSceneRoom`
  for a PX would hit the wrong (world-level) endpoint. `PortableExperienceSceneRoom`
  bypasses this by always building the scene-level URL.
- **Assembly direction.** Systems must compile into `DCL.Plugins` (they reference
  many assemblies). `SceneRuntime` may reference `DCL.Multiplayer`, never the
  reverse. These rules dictated where each piece lives.

<!-- prettier-ignore -->
> [!NOTE]
> A namespace gotcha: code under `DCL.PluginSystem.*` cannot use the unqualified
> `World` type, because the `DCL.PluginSystem.World` namespace shadows
> `Arch.Core.World` and the Arch query source generator emits `World` unqualified.
> `PortableExperienceWorldCommsSystem` therefore lives in the
> `DCL.Multiplayer.Connections.PortableExperiences` namespace even though its
> file sits under `PluginSystem/Global/`.

## The three problems we solved

Getting a PX to talk to its authoritative server required fixing three distinct
gaps, each surfaced by a different symptom.

### 1. No spawn trigger

By default a PX never joins any scene comms room, so the server side never sees
a user and never spawns. The fix is `PortableExperienceWorldComms` plus its
driving system: when an authoritative PX scene is running, the client opens a
data-only LiveKit connection to that world's comms.

### 2. Wrong room — the join carried no sceneId

The first implementation connected to the **world-level** adapter
(`worlds/<realm>/comms`). The server received the join but logged
`Join event missing sceneId` and skipped the spawn, because the world-level
LiveKit room name carries no `sceneId`.

The fix is `PortableExperienceSceneRoom`, which connects to the **scene-level**
endpoint (`worlds/<realm>/scenes/<sceneId>/comms`) with the `sceneId` in the
signed handshake metadata. `comms-gatekeeper` then names the room
`{worldRoomPrefix}{world}-{sceneId}`, and its `getRoomMetadataFromRoomName`
parses the `sceneId` back out, so the server's join event carries it and
Hammurabi spawns.

### 3. No CRDT handshake — the SDK never sent `REQ_CRDT_STATE`

With the room connected and the server spawned, no CRDT flowed in either
direction. The Unity comms plumbing was correct (`SendBinary` reached the pipe),
but the SDK runtime handed it an **empty** outgoing batch every tick.

The cause is in `js-sdk-toolchain`. In
`packages/@dcl/sdk/src/network/message-bus-sync.ts`, the function that emits the
bootstrap `REQ_CRDT_STATE` is gated on the scene's `RealmInfo`:

```ts
if (!RealmInfo.getOrNull(engine.RootEntity)?.isConnectedSceneRoom) return
binaryMessageBus.emit(CommsMessage.REQ_CRDT_STATE, new Uint8Array())
// ...and requestState() itself is triggered by:
RealmInfo.onChange(... if (value?.isConnectedSceneRoom && !stateIsSyncronized) requestState())
```

The client writes `RealmInfo` through `WriteRealmInfoSystem`, which computed
`isConnectedSceneRoom` from the **host** scene room (`roomHub.SceneRoom()`) —
never connected to a PX scene, so always `false`. The SDK therefore never
requested state.

The fix makes `WriteRealmInfoSystem` also report `true` when the PX room is
connected, via `PortableExperienceWorldComms.IsConnected(sceneId)`. When the PX
room finishes connecting, the flag flips, `RealmInfo.onChange` fires,
`requestState()` runs, and `REQ_CRDT_STATE` finally reaches `authoritative-server`,
which responds with `RES_CRDT_STATE` and ongoing CRDT deltas.

## Message types and the handshake

The binary message bus uses a one-byte message type that must stay aligned with
the SDK (`CommunicationsControllerAPIImplementationBase.CommsMessageType`):

| Value | Type | Direction |
|---|---|---|
| 1 | `CRDT` | Both — incremental state |
| 2 | `REQ_CRDT_STATE` | Client → server — request full state on connect |
| 3 | `RES_CRDT_STATE` | Server → client — full state response |

`REQ_CRDT_STATE` is sent with `DELIVERY_ASSERTED` and the SDK retries it for a
few seconds; `CRDT` deltas use `DROP_IF_NOT_CONNECTED`.

## Diagnostics and gotchas

These cost real time during development; keep them in mind when debugging comms.

- **Use `ReportHub.LogProductionInfo` for must-see logs.** Plain `Debug.Log`
  /`Debug.LogWarning` is swallowed or redirected by the project's custom log
  handler, and category logs (for example `COMMS_SCENE_HANDLER`) are filtered by
  the severity matrix. `LogProductionInfo` renders as the always-visible
  `[ALWAYS]` lines. See [Diagnostics](diagnostics.md) and
  [Override Debug Log Matrix](override-debug-log-matrix.md).
- **The Console error count is the source of truth, not `Editor.log`.**
  `Editor.log` is append-only across every recompile, so grepping it for
  `error CS` mixes results from many transient builds (the Editor auto-compiles
  after every saved file). Check the live Console count instead.
- **Script changes do not compile during Play mode.** Unity defers recompilation
  until you exit Play. A `⌘R` refresh while playing reports `compile time=0`.
- **`InteriorRoom.DataPipe` is a stable proxy.** A `MessagePipe` built over a
  room's data pipe before the room connects keeps receiving across LiveKit room
  swaps, because `InteriorDataPipe.Assign` re-points to each new underlying room.
  This is why the PX message pipe can be built eagerly.

### Verify a PX is talking to its authoritative server

1. Confirm the world is reachable: open
   `https://worlds-content-server.decentraland.org/world/<name>.dcl.eth/about`
   and check `configurations.scenesUrn` is non-empty.
2. With `explorer-alfa-portable-experiences-chat-commands` enabled, run
   `/loadpx <name>` once you are in-world.
3. In `sdk-multiplayer-server` logs, confirm `Spawning process for
   authoritativeMultiplayer scene` with the scene's entity hash.
4. In the Unity Console, confirm the scene room connects, the SDK emits a
   `REQ_CRDT_STATE` (`firstByte=2`), and the client receives `RES_CRDT_STATE`
   from the server.

<!-- prettier-ignore -->
> [!NOTE]
> Temporary `[PX-COMMS]` diagnostic logs were added to `SceneCommunicationPipe`,
> `PortableExperienceWorldComms`, and `CommunicationsControllerAPIImplementationBase`
> to trace the connect → register → send → receive chain. Remove them once the
> feature is verified.

## See also

- [LiveKit Networking](livekit-networking.md) — rooms, message pipes, GateKeeper
- [Multiplayer](multiplayer.md) — transport-neutral hub and SDK propagation
- [Scene Runtime](scene-runtime.md) — CRDT bridge and the comms controller
- [IPFS Realms](ipfs-realms.md) — realms, worlds, and `RealmData`
- [Architecture Overview](architecture-overview.md) — containers and the
  "deferred dependencies" rules that shaped the wiring
