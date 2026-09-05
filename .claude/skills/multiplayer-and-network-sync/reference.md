# Multiplayer & Network Sync -- Detailed Reference

## RoomHub Code

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

---

## NetworkMovementMessage Struct

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

---

## Send Throttling Code

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

---

## ExtrapolationComponent Code

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

---

## Catch-Up Mechanism Code

```csharp
float correctionTime = inboxMessages * Time.smoothDeltaTime;
intComp.TotalDuration = Mathf.Max(
    intComp.TotalDuration - correctionTime,
    intComp.TotalDuration / settings.InterpolationSettings.MaxSpeedUpTimeDivider);
```

---

## EntityParticipantTable Code

```csharp
public class EntityParticipantTable : IEntityParticipantTable
{
    // walletId <-> Entity, with RoomSource (ISLAND, GATEKEEPER, or both)
    public void Register(string walletId, Entity entity, RoomSource fromRoom);
    public bool Release(string walletId, RoomSource fromRoom);  // returns true if fully disconnected
    public void AddRoomSource(string walletId, RoomSource fromRoom);
}
```

---

## ThreadSafeRemoveIntentions Code

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

---

## OwnedBunch Code

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

---

## SDK Propagation Code

### PlayerTransformPropagationSystem (Global -> Scene)

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
