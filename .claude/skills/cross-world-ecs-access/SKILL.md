---
name: cross-world-ecs-access
description: "Cross-world ECS access — global world entities from scene systems, propagation systems, bridge components, PersistentEntities, and ISceneIsCurrentListener. Use when a scene-world system needs to read or modify global-world state (player entity, camera, input, avatar), implementing global-to-scene or scene-to-global propagation, working with PlayerCRDTEntity bridge components, accessing PersistentEntities, or using ISceneIsCurrentListener for scene-current safety guards."
user-invocable: false
---

# Cross-World ECS Access

## Sources

- `docs/architecture-overview.md` — World architecture (global vs scene worlds)
- `docs/development-guide.md` — ECS system design, component mutation rules

---

## Injection Chain

The global world reaches scene systems through a plugin injection chain:

1. `DynamicWorldContainer` creates the global `World` and `GlobalWorldFactory`
2. `StaticContainer` instantiates `IDCLWorldPlugin` instances, passing the global world to plugin constructors
3. Plugin stores `globalWorld` as a field
4. On scene creation, `InjectToWorld` passes `globalWorld` to system constructors

From `InputModifierPlugin.cs` — plugin receives global world + player entity, forwards to system:

```csharp
public class InputModifierPlugin : IDCLWorldPlugin
{
    private readonly Arch.Core.World world;       // global world
    private readonly Entity playerEntity;          // global player entity

    public InputModifierPlugin(Arch.Core.World world, Entity playerEntity,
        ISceneRestrictionBusController sceneRestrictionBusController) { ... }

    public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
        in ECSWorldInstanceSharedDependencies sharedDependencies,
        in PersistentEntities persistentEntities, ...)
    {
        var system = InputModifierHandlerSystem.InjectToWorld(
            ref builder, world, playerEntity, sharedDependencies.SceneStateProvider, ...);
        sceneIsCurrentListeners.Add(system);
        finalizeWorldSystems.Add(system);
    }
}
```

> **Note:** `ECSWorldInstanceSharedDependencies` provides scene-specific metadata (scene data, partition, CRDT writer) but does NOT carry the global world reference. The global world must be passed through the plugin constructor.

---

## Two Access Patterns

### Pattern A: Direct Global World Access

For O(1) reads/writes on known global entities. The scene system stores `globalWorld` and the target entity, then calls `Get`/`TryGet` directly.

From `InputModifierHandlerSystem.cs`:

```csharp
public partial class InputModifierHandlerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
{
    private readonly World globalWorld;
    private readonly Entity playerEntity;

    internal InputModifierHandlerSystem(World world, World globalWorld, Entity playerEntity, ...) : base(world)
    {
        this.globalWorld = globalWorld;
        this.playerEntity = playerEntity;
    }

    [Query]
    private void ApplyModifiers([Data] bool skipDirtyCheck, Entity entity,
        in PBInputModifier pbInputModifier, in CRDTEntity crdtEntity)
    {
        // Read scene-world SDK component, write to global-world component
        ref var inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
        inputModifier.DisableAll = pbInputModifier.Standard.DisableAll;
        // ...
    }
}
```

**When to use:** Fast singleton access — player input, camera state, settings. Best when the target entity is known at construction time.

### Pattern B: CRDT Bridge

For indirect sync without direct world references. Uses `IECSToCRDTWriter` to serialize data back to the scene runtime.

From `WriteSDKAvatarBaseSystem.cs`:

```csharp
public partial class WriteSDKAvatarBaseSystem : BaseUnityLoopSystem
{
    private readonly IECSToCRDTWriter ecsToCRDTWriter;

    [Query]
    [None(typeof(DeleteEntityIntention))]
    private void UpdateAvatarBase(PlayerSceneCRDTEntity playerCRDTEntity, SDKProfile profile)
    {
        if (!profile.IsDirty) return;
        ecsToCRDTWriter.PutMessage<PBAvatarBase, SDKProfile>(static (pb, profile) =>
        {
            pb.Name = profile.Name;
            pb.BodyShapeUrn = profile.Avatar.BodyShape;
        }, playerCRDTEntity.CRDTEntity, profile);
    }
}
```

**When to use:** Scene-to-runtime communication, CRDT-synchronized data. See the **sdk-component-implementation** skill for `PutMessage`/`AppendMessage`/`DeleteMessage` patterns.

---

## PersistentEntities

Well-known entities created once per scene world, guaranteed alive for the world's lifetime. Received via `InjectToWorld`'s `in PersistentEntities persistentEntities` parameter.

From `PersistentEntities.cs`:

```csharp
public struct PersistentEntities
{
    public readonly Entity Player;          // scene-local player entity
    public readonly Entity Camera;          // scene-local camera entity
    public readonly Entity SceneRoot;       // root entity (modifiable by scene creator)
    public readonly Entity SceneContainer;  // container of root (internal use only)
}
```

Use `SingleInstanceEntity` + `WorldExtensions` for cached global-world singletons:

```csharp
// From WorldExtensions.cs
public static SingleInstanceEntity CacheCamera(this World world) =>
    new (in QUERY, world);

public static ref CameraComponent GetCameraComponent(this in SingleInstanceEntity instance, World world) =>
    ref world.Get<CameraComponent>(instance);
```

---

## Bridge Components

### PlayerCRDTEntity (global world)

Lives on global-world avatar entities. Holds references to the scene the player is assigned to and the corresponding scene-world entity.

```csharp
public struct PlayerCRDTEntity : IDirtyMarker
{
    public CRDTEntity CRDTEntity { get; }
    public ISceneFacade? SceneFacade { get; private set; }
    public Entity SceneWorldEntity { get; private set; }
    public bool AssignedToScene => SceneFacade != null;

    public void AssignToScene(ISceneFacade sceneFacade, Entity sceneWorldEntity) { ... }
    public void RemoveFromScene() { ... }
}
```

### PlayerSceneCRDTEntity (scene world)

Lives on scene-world entities that represent a player. Holds the CRDT entity ID for writing back to the scene runtime.

```csharp
public struct PlayerSceneCRDTEntity : IDirtyMarker
{
    public readonly CRDTEntity CRDTEntity;
}
```

**Relationship:** A global `PlayerCRDTEntity` points to a scene via `SceneFacade` + `SceneWorldEntity`. The scene entity has a `PlayerSceneCRDTEntity` pointing back via `CRDTEntity`. Propagation systems use this bidirectional link to sync data across worlds.

---

## Propagation Systems

### Global-to-Scene: PlayerTransformPropagationSystem

Runs in the global world, queries global entities, writes into scene worlds via `playerCRDTEntity.SceneFacade.EcsExecutor.World`:

```csharp
[UpdateInGroup(typeof(PreRenderingSystemGroup))]
public partial class PlayerTransformPropagationSystem : BaseUnityLoopSystem
{
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
}
```

### Scene-to-Global: Direct globalWorld Access

Scene systems write to the global world directly using Pattern A (see `InputModifierHandlerSystem` above). The system reads scene-world SDK components and writes the result to `globalWorld.Get<T>(playerEntity)`.

---

## ISceneIsCurrentListener Safety Pattern

When a scene system modifies global state, it must only do so while the scene is current. Implement `ISceneIsCurrentListener` to apply state when becoming current and reset to defaults when leaving.

From `InputModifierHandlerSystem.cs`:

```csharp
public partial class InputModifierHandlerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
{
    protected override void Update(float t)
    {
        if (!sceneStateProvider.IsCurrent) return;   // guard every frame
        ApplyModifiersQuery(World, false);
        HandleComponentRemovalQuery(World);
    }

    public void OnSceneIsCurrentChanged(bool value)
    {
        if (value)
            ApplyModifiersQuery(World, true);        // re-apply on becoming current
        else
            ResetModifiers();                        // reset global state on leaving
    }

    private void ResetModifiers()
    {
        ref InputModifierComponent inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
        inputModifier.RemoveAllModifiers();
    }
}
```

Register the system in the plugin:

```csharp
sceneIsCurrentListeners.Add(system);
```

> `ISceneIsCurrentListener.OnSceneIsCurrentChanged` is called even if the scene has stopped with an error, ensuring global state is always cleaned up.

---

## Safety Rules

> See CLAUDE.md SS5 for full mutation rules.

1. **Use `ref var` for mutations, never `var` alone** — `var` copies the component; changes are lost
2. **No structural changes after obtaining `ref`** — `World.Add`/`World.Remove` invalidates outstanding `ref`/`in`/`out` pointers. Complete all reads/writes first, then apply structural changes
3. **Prefer `TryGet` for safety on known entities** — avoids exceptions when the component may not exist
4. **Guard global writes with `IsCurrent` check** — always check `sceneStateProvider.IsCurrent` in `Update()` and implement `ISceneIsCurrentListener` to reset on scene exit
5. **`ECSWorldInstanceSharedDependencies` provides scene metadata, NOT global world** — the global world must be injected through the plugin constructor
6. **Register for cleanup** — systems that modify global state should implement `IFinalizeWorldSystem` and be added to `finalizeWorldSystems` so global state is reset on world disposal

---

## Cross-References

- **ecs-system-and-component-design** — `ref var` mutation rules (CLAUDE.md SS5), component cleanup lifecycle, system design patterns
- **plugin-architecture** — `InjectToWorld` signature, `ECSWorldInstanceSharedDependencies`, plugin constructor injection chain
- **sdk-component-implementation** — `IECSToCRDTWriter` for CRDT bridge pattern (`PutMessage`/`AppendMessage`/`DeleteMessage`)
