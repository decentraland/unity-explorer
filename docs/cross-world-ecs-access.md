# Cross-World ECS Access

## Overview

The Explorer runs two kinds of ECS worlds simultaneously: a single **global world** and many **scene worlds** (one per loaded JavaScript scene). The global world owns long-lived entities (player, camera, remote avatars). Each scene world is an isolated sandbox for entities created by its JavaScript runtime.

These worlds are independent by design -- a scene world cannot reference global-world entities through ECS queries. Yet many features need to cross that boundary: a scene must disable player movement via input modifiers, and the global world must push avatar transforms into scenes. This document explains the patterns that make cross-world communication safe and predictable.

For the full world architecture, see the [architecture overview](architecture-overview.md). For component mutation rules and system design, see the [development guide](development-guide.md).

## How the Global World Reaches Scene Systems

The global world reference flows through a plugin injection chain:

1. **`DynamicWorldContainer` creates the global `World`** instance and `GlobalWorldFactory`.
2. **Plugins receive it in constructors.** `StaticContainer` instantiates `IDCLWorldPlugin` instances, passing the global `World` (and optionally entities like `playerEntity`) to their constructors.
3. **Plugin stores it as a field.** The plugin holds onto these references as private fields.
4. **`InjectToWorld` forwards them to systems.** When a scene world is created, the plugin constructs scene-world systems, passing the global world into their constructors.

Here is `InputModifierPlugin` showing the full chain:

```csharp
public class InputModifierPlugin : IDCLWorldPlugin
{
    // Step 2 & 3: global world + player entity received and stored
    private readonly Arch.Core.World world;       // the global world
    private readonly Entity playerEntity;          // player entity in global world
    private readonly ISceneRestrictionBusController sceneRestrictionBusController;

    public InputModifierPlugin(Arch.Core.World world, Entity playerEntity,
        ISceneRestrictionBusController sceneRestrictionBusController)
    {
        this.world = world;
        this.playerEntity = playerEntity;
        this.sceneRestrictionBusController = sceneRestrictionBusController;
    }

    // Step 4: forward global world into scene-world system constructors
    public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
        in ECSWorldInstanceSharedDependencies sharedDependencies,
        in PersistentEntities persistentEntities,
        List<IFinalizeWorldSystem> finalizeWorldSystems,
        List<ISceneIsCurrentListener> sceneIsCurrentListeners)
    {
        ResetDirtyFlagSystem<PBInputModifier>.InjectToWorld(ref builder);

        var system = InputModifierHandlerSystem.InjectToWorld(
            ref builder, world, playerEntity,
            sharedDependencies.SceneStateProvider,
            sceneRestrictionBusController);

        // Register for scene-current callbacks and world disposal cleanup
        sceneIsCurrentListeners.Add(system);
        finalizeWorldSystems.Add(system);
    }

    public void Dispose() { }
}
```

> **Note:** `ECSWorldInstanceSharedDependencies` provides scene-specific metadata (scene data, partition, CRDT writer, collider caches) but does **not** carry the global world reference. The global world must always come through the plugin constructor.

## Access Patterns

There are two patterns for cross-world data flow. Choose based on direction and whether CRDT synchronization is involved.

### Pattern A: Direct Global World Access

**When to use:** A scene-world system needs to read or write a known entity in the global world -- typically the player entity for input, camera state, or movement settings. Best when the target entity is known at construction time and you need immediate, synchronous access.

The system stores `globalWorld` and `playerEntity` as fields, then calls `Get` or `TryGet` on them directly. The key parts of `InputModifierHandlerSystem`:

```csharp
[UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
public partial class InputModifierHandlerSystem : BaseUnityLoopSystem,
    ISceneIsCurrentListener, IFinalizeWorldSystem
{
    private readonly Entity playerEntity;
    private readonly World globalWorld;
    private readonly ISceneStateProvider sceneStateProvider;

    // Constructor receives both the scene world (via base) and global world
    public InputModifierHandlerSystem(World world, World globalWorld, Entity playerEntity,
        ISceneStateProvider sceneStateProvider, ...) : base(world)
    {
        this.globalWorld = globalWorld;
        this.playerEntity = playerEntity;
        this.sceneStateProvider = sceneStateProvider;
    }

    protected override void Update(float t)
    {
        if (!sceneStateProvider.IsCurrent) return;  // safety guard (see section 7)
        ApplyModifiersQuery(World, false);
        HandleComponentRemovalQuery(World);
    }

    [Query]
    private void ApplyModifiers([Data] bool skipDirtyCheck, Entity entity,
        in PBInputModifier pbInputModifier, in CRDTEntity crdtEntity)
    {
        if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY
            || pbInputModifier.ModeCase == PBInputModifier.ModeOneofCase.None
            || (!skipDirtyCheck && !pbInputModifier.IsDirty)) return;

        // Read from scene world (pbInputModifier), write to global world
        ref var inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
        inputModifier.DisableAll = pbInputModifier.Standard.DisableAll;
        // ... remaining fields follow the same pattern

        // Mark the scene entity so we detect when the SDK component is removed
        World.AddOrGet<InputModifierComponent>(entity);
    }
}
```

The query runs against the scene world (`World`), reading the SDK protobuf component, while `globalWorld.Get<InputModifierComponent>(playerEntity)` returns a `ref` into the global world -- changes apply directly to global component data.

> **Warning:** Always use `ref var` when modifying components. Writing `var inputModifier = globalWorld.Get<...>(...)` copies the struct -- your changes silently vanish.

### Pattern B: CRDT Bridge (IECSToCRDTWriter)

**When to use:** A scene-world system needs to push data back to the scene's JavaScript runtime through the CRDT protocol. This is the standard path for SDK-visible data such as avatar profiles, transforms, and emote states.

The system receives `IECSToCRDTWriter` from `ECSWorldInstanceSharedDependencies` and calls `PutMessage`, `AppendMessage`, or `DeleteMessage` to serialize component data. From `WriteSDKAvatarBaseSystem`:

```csharp
public partial class WriteSDKAvatarBaseSystem : BaseUnityLoopSystem
{
    private readonly IECSToCRDTWriter ecsToCRDTWriter;

    public WriteSDKAvatarBaseSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
    {
        this.ecsToCRDTWriter = ecsToCRDTWriter;
    }

    [Query]
    [None(typeof(DeleteEntityIntention))]
    private void UpdateAvatarBase(PlayerSceneCRDTEntity playerCRDTEntity, SDKProfile profile)
    {
        if (!profile.IsDirty) return;

        // Serialize profile data into a PBAvatarBase protobuf message for the scene runtime
        ecsToCRDTWriter.PutMessage<PBAvatarBase, SDKProfile>(static (pbComponent, profile) =>
        {
            pbComponent.Name = profile.Name;
            pbComponent.BodyShapeUrn = profile.Avatar.BodyShape;
            // ... remaining avatar fields
        }, playerCRDTEntity.CRDTEntity, profile);
    }

    [Query]
    [All(typeof(DeleteEntityIntention))]
    private void HandleComponentRemoval(PlayerSceneCRDTEntity playerCRDTEntity)
    {
        ecsToCRDTWriter.DeleteMessage<PBAvatarBase>(playerCRDTEntity.CRDTEntity);
    }
}
```

**Choosing between patterns:** Use Pattern A when modifying global singleton state (input, camera, settings) that does not need CRDT serialization. Use Pattern B when the data must be visible to the scene's JavaScript runtime.

## PersistentEntities

**PersistentEntities** are well-known entities created once when a scene world is instantiated. They are guaranteed to remain alive for the entire lifetime of that world. Plugins receive them through the `InjectToWorld` method's `in PersistentEntities persistentEntities` parameter.

```csharp
/// Entities that are created in a world factory and never destroyed
/// while the ECS World is alive
public struct PersistentEntities
{
    public readonly Entity Player;          // scene-local player entity
    public readonly Entity Camera;          // scene-local camera entity
    public readonly Entity SceneRoot;       // root entity (modifiable by the scene creator)
    public readonly Entity SceneContainer;  // container of the root (internal use only)
}
```

- **Player** and **Camera** represent the local player and camera within the scene world. These are distinct from the global-world player and camera entities.
- **SceneRoot** is the top-level entity that the scene's JavaScript code can modify (position, rotation, etc.).
- **SceneContainer** wraps `SceneRoot` and is managed internally by the engine -- scene code cannot touch it.

### Caching with SingleInstanceEntity and WorldExtensions

For global-world singletons, use `SingleInstanceEntity` to cache entity lookups and avoid per-frame queries. `WorldExtensions` provides convenience methods:

```csharp
// Cache the entity once in the system constructor
SingleInstanceEntity cameraEntity = world.CacheCamera();

// Access the component by ref without a per-frame query
ref CameraComponent cam = ref cameraEntity.GetCameraComponent(world);
```

Under the hood, `CacheCamera` creates a `SingleInstanceEntity` from a `QueryDescription` and `GetCameraComponent` calls `world.Get<CameraComponent>(instance)`. This is roughly twice as fast as running a query each frame -- see the [development guide](development-guide.md#rule-of-thumb-tryget-vs-query) for benchmarks.

## Bridge Components

Two components form a bidirectional link between global-world avatar entities and their corresponding scene-world representations.

### PlayerCRDTEntity (global world)

Lives on avatar entities in the global world. Holds a reference to the scene the player is currently assigned to and the corresponding entity in that scene's ECS world:

```csharp
public struct PlayerCRDTEntity : IDirtyMarker
{
    public CRDTEntity CRDTEntity { get; }
    public ISceneFacade? SceneFacade { get; private set; }  // null when on a road/empty parcel
    public Entity SceneWorldEntity { get; private set; }     // Entity.Null when unassigned

    public void AssignToScene(ISceneFacade sceneFacade, Entity sceneWorldEntity) { ... }
    public void RemoveFromScene() { ... }

    // false when the player is outside any scene (road, empty parcels, LOD)
    public bool AssignedToScene => SceneFacade != null;
    public bool IsDirty { get; set; }
}
```

### PlayerSceneCRDTEntity (scene world)

Lives on scene-world entities that represent a player. Holds the CRDT entity ID needed for writing data back to the scene runtime:

```csharp
/// Dedicated to the scene world
public struct PlayerSceneCRDTEntity : IDirtyMarker
{
    public readonly CRDTEntity CRDTEntity;
    public bool IsDirty { get; set; }

    public PlayerSceneCRDTEntity(CRDTEntity crdtEntity)
    {
        CRDTEntity = crdtEntity;
        IsDirty = true;
    }
}
```

**How the bidirectional link works:** A global `PlayerCRDTEntity` points into a scene via `SceneFacade` (the scene reference) and `SceneWorldEntity` (the entity handle inside that scene's world). The corresponding scene entity carries a `PlayerSceneCRDTEntity` that points back via its `CRDTEntity` ID. Propagation systems traverse this link in both directions to sync data across worlds.

## Propagation Systems

### Global-to-Scene: PlayerTransformPropagationSystem

This system runs in the **global world**. It queries global avatar entities, reads their `CharacterTransform`, and writes the position and rotation into the corresponding scene-world entity's `SDKTransform`. This is how other players' movements become visible to scene JavaScript code.

```csharp
// Runs in the global world — queries global entities, writes into scene worlds
[UpdateInGroup(typeof(PreRenderingSystemGroup))]
public partial class PlayerTransformPropagationSystem : BaseUnityLoopSystem
{
    private readonly IComponentPool<SDKTransform> sdkTransformPool;

    [Query]
    [None(typeof(DeleteEntityIntention))]
    private void PropagateTransformToScene(in CharacterTransform characterTransform,
        in PlayerCRDTEntity playerCRDTEntity)
    {
        if (!characterTransform.Transform.hasChanged) return;
        if (!playerCRDTEntity.AssignedToScene) return;
        if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;

        // Reach into the scene world through the bridge component
        World sceneEcsWorld = playerCRDTEntity.SceneFacade!.EcsExecutor.World;

        // Lazily add SDKTransform if the scene entity doesn't have one yet
        if (!sceneEcsWorld.TryGet<SDKTransform>(playerCRDTEntity.SceneWorldEntity,
                out SDKTransform? sdkTransform))
            sceneEcsWorld.Add(playerCRDTEntity.SceneWorldEntity,
                sdkTransform = sdkTransformPool.Get());

        sdkTransform!.Position.Value = characterTransform.Transform.position;
        sdkTransform.Rotation.Value = characterTransform.Transform.rotation;
        sdkTransform.IsDirty = true;
    }
}
```

Key points: the system accesses the scene world through `playerCRDTEntity.SceneFacade.EcsExecutor.World` rather than holding a scene world reference. It guards against unassigned players and skips the main player entity (handled by a separate system). `SDKTransform` is pooled and lazily added to avoid allocations.

### Scene-to-Global: Direct globalWorld Access

Scene-to-global propagation uses Pattern A (direct global world access). The scene-world system reads its own SDK components via a query and writes results into the global world using the stored `globalWorld` reference. See `InputModifierHandlerSystem` in the [Access Patterns](#pattern-a-direct-global-world-access) section for the full example.

## Safety: ISceneIsCurrentListener

Multiple scene worlds can be loaded simultaneously, but only one scene is **current** at any given time -- the scene the player is standing in. When a scene-world system modifies global state, it must only do so while its scene is current. Otherwise, a background scene could overwrite the active scene's input modifiers or camera settings.

The `ISceneIsCurrentListener` interface provides the lifecycle hook:

```csharp
public interface ISceneIsCurrentListener
{
    void OnSceneIsCurrentChanged(bool value);
}
```

A system that modifies global state implements the full lifecycle. From `InputModifierHandlerSystem`:

```csharp
protected override void Update(float t)
{
    if (!sceneStateProvider.IsCurrent) return;  // 1. Guard every frame
    ApplyModifiersQuery(World, false);
}

public void OnSceneIsCurrentChanged(bool value)
{
    if (value)
        ApplyModifiersQuery(World, true);       // 2. Re-apply on becoming current
    else
        ResetModifiers();                       // 3. Reset global state on leaving
}

private void ResetModifiers()
{
    ref InputModifierComponent inputModifier =
        ref globalWorld.Get<InputModifierComponent>(playerEntity);
    inputModifier.RemoveAllModifiers();
}
```

The plugin registers the system so the engine calls `OnSceneIsCurrentChanged`:

```csharp
sceneIsCurrentListeners.Add(system);
```

> **Warning:** `OnSceneIsCurrentChanged(false)` is called even if the scene has stopped with an error. This ensures global state is always cleaned up regardless of how the scene exits.

## Safety Rules

1. **Use `ref var` for mutations, never `var` alone.** Writing `var copy = globalWorld.Get<T>(entity)` copies the struct. Your modifications apply to the copy and are silently lost.

2. **No structural changes after obtaining `ref`.** Calling `World.Add` or `World.Remove` relocates entity data in memory, invalidating all outstanding `ref`, `in`, and `out` pointers. Complete all reads and writes first, then apply structural changes.

3. **Prefer `TryGet` for safety on known entities.** When a component may not exist yet, `TryGet` avoids exceptions and makes the intent explicit.

4. **Guard global writes with `IsCurrent` checks.** Always check `sceneStateProvider.IsCurrent` in `Update()` and implement `ISceneIsCurrentListener` to reset state when the scene loses current status.

5. **`ECSWorldInstanceSharedDependencies` provides scene metadata, not the global world.** The global world reference must be injected through the plugin constructor -- it is not available in shared dependencies.

6. **Register for cleanup on world disposal.** Systems that modify global state should implement `IFinalizeWorldSystem` and be added to `finalizeWorldSystems`. This ensures global state is reset when the scene world is destroyed (e.g., when the player moves away).

## See Also

- [Architecture Overview](architecture-overview.md) -- world architecture, plugin system, containers
- [Development Guide](development-guide.md) -- component mutation rules, system design, query patterns
- [Scene Runtime](scene-runtime.md) -- scene lifecycle, JavaScript integration, CRDT protocol
