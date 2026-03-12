---
name: ecs-system-and-component-design
description: "ECS system and component design patterns using Arch ECS. Use when creating or modifying systems, components, queries, cleanup logic, singletons, or system tests — also when reviewing code that manipulates World, Entity, or component references, even if the user doesn't mention 'ECS' explicitly."
user-invocable: false
---

# ECS System & Component Design

## Sources

- `docs/development-guide.md` — Primary ECS development reference
- `docs/architecture-overview.md` — High-level ECS architecture and patterns
- `docs/systems.md` — Scene bounds and rendering system details
- `CLAUDE.md` — Condensed ECS rules

---

## System Design Rules

> Condensed rules are in CLAUDE.md §1–7. This skill provides expanded examples and patterns not covered there.

### Code Example — Clean System

From `BillboardSystem.cs`:

```csharp
[UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
[UpdateAfter(typeof(UpdateTransformSystem))]
public partial class BillboardSystem : BaseUnityLoopSystem
{
    private const float MINIMUM_DISTANCE_TO_ROTATE_SQR = 0.25f * 0.25f;
    private readonly IExposedCameraData exposedCameraData;

    // Internal constructor with shared dependencies only
    public BillboardSystem(World world, IExposedCameraData exposedCameraData) : base(world)
    {
        this.exposedCameraData = exposedCameraData;
    }

    protected override void Update(float t)
    {
        Vector3 cameraPosition;
        Quaternion cameraRotation;

        var activeVirtualCamera = exposedCameraData.CinemachineBrain?.ActiveVirtualCamera;
        if (activeVirtualCamera != null)
        {
            var cameraTransform = activeVirtualCamera.VirtualCameraGameObject.transform;
            cameraPosition = cameraTransform.position;
            cameraRotation = cameraTransform.rotation;
        }
        else
        {
            cameraPosition = exposedCameraData.WorldPosition;
            cameraRotation = exposedCameraData.WorldRotation.Value;
        }

        var cameraRotationAxisZ = Quaternion.Euler(0, 0, cameraRotation.eulerAngles.z);
        UpdateRotationQuery(World, cameraPosition, cameraRotationAxisZ);
    }

    [Query]
    private void UpdateRotation(
        [Data] in Vector3 cameraPosition,
        [Data] in Quaternion cameraRotationAxisZ,
        ref TransformComponent transform,
        in PBBillboard billboard)
    {
        // Bitwise billboard-mode filtering, early-exit guards
        // ...
    }
}
```

## Querying Best Practices

Ranked from most preferred to least:

1. **Source-generated queries** (preferred) — Use `[Query]` attribute with `[Data]` parameters
2. **`GetChunkIterator()`** — For generic systems
3. **`World.InlineQuery`** — Outside systems or generic cases; uses `IForEach<T>` struct
4. **`World.Query`** — Last resort due to delegate/closure overhead

**Rules:**
- Always filter out `DeleteEntityIntention` in queries that should not process dying entities
- No nested queries
- Use `TryGet` for known entities over queries
- Consolidate queries sharing the same filters

## Safe Component Mutation

This is critical — incorrect mutation causes silent data loss:

```csharp
// CORRECT — ref var modifies the component in-place
ref var component = ref world.Get<MyComponent>(entity);
component.Value = 42;

// WRONG — var copies the component; changes are lost
var component = world.Get<MyComponent>(entity);
component.Value = 42; // This modification is lost!
```

> See CLAUDE.md §4–5 for the full performance and mutation rules. Key additions here:

- Components should be data-only (no logic); may have pool, static factory, or non-empty constructor
- Prefer `struct` for components; use `class` for MonoBehaviors, existing class lifecycle, or cross-world references

## Component Cleanup Patterns

Cleanup must handle three triggers:

### 1. Component Removed

Query for entities that lost the SDK component but still have the internal component:

```csharp
[Query]
[None(typeof(PBAudioSource), typeof(DeleteEntityIntention))]
private void HandleComponentRemoval(ref AudioSourceComponent component)
{
    releaseAudioSourceComponent.Update(ref component);
}
```

### 2. Entity Destroyed

Query for entities marked for deletion:

```csharp
[Query]
[All(typeof(DeleteEntityIntention))]
private void HandleEntityDestruction(ref AudioSourceComponent component)
{
    releaseAudioSourceComponent.Update(ref component);
}
```

### 3. World Disposed

Implement `IFinalizeWorldSystem` for cleanup when the world shuts down:

```csharp
public void FinalizeComponents(in Query query)
{
    World.InlineQuery<ReleaseAudioSourceComponent, AudioSourceComponent>(
        in new QueryDescription().WithAll<AudioSourceComponent>(),
        ref releaseAudioSourceComponent);
}
```

**Cleanup tasks include:** pool returns, promise invalidation/dereferencing, cache dereferencing, custom logic (e.g., avatar teardown).

Prefer `ReleasePoolableComponentSystem<T, TProvider>` for pooled disposals.

### Code Example — Full Cleanup Lifecycle

From `CleanUpAudioSourceSystem.cs` — shows all three triggers + `IForEach<T>` struct pattern:

```csharp
[UpdateInGroup(typeof(CleanUpGroup))]
[LogCategory(ReportCategory.SDK_AUDIO_SOURCES)]
[ThrottlingEnabled]
public partial class CleanUpAudioSourceSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
{
    private ReleaseAudioSourceComponent releaseAudioSourceComponent;

    protected override void Update(float t)
    {
        HandleEntityDestructionQuery(World);
        HandleComponentRemovalQuery(World);
        World.Remove<AudioSourceComponent>(in HandleComponentRemoval_QueryDescription);
    }

    [Query] [None(typeof(PBAudioSource), typeof(DeleteEntityIntention))]
    private void HandleComponentRemoval(ref AudioSourceComponent component)
        => releaseAudioSourceComponent.Update(ref component);

    [Query] [All(typeof(DeleteEntityIntention))]
    private void HandleEntityDestruction(ref AudioSourceComponent component)
        => releaseAudioSourceComponent.Update(ref component);

    public void FinalizeComponents(in Query query)
    {
        World.InlineQuery<ReleaseAudioSourceComponent, AudioSourceComponent>(
            in new QueryDescription().WithAll<AudioSourceComponent>(),
            ref releaseAudioSourceComponent);
    }

    // IForEach<T> struct — allocation-free callback for InlineQuery
    private readonly struct ReleaseAudioSourceComponent : IForEach<AudioSourceComponent>
    {
        private readonly World world;
        private readonly IComponentPool componentPool;

        public void Update(ref AudioSourceComponent component)
        {
            component.CleanUp(world);
            if (component.AudioSource != null) componentPool.Release(component.AudioSource);
            component.Dispose();
        }
    }
}
```

## ECS Singletons

Single-instance entities for `Player`, `Input`, `Camera`, `Time`, etc.

- Cache via `WorldExtensions` (`world.CacheCamera()`, `world.CachePlayer()`)
- Use `TryGet` for mutation, `Has` for presence checks
- **`TryGet` is 2x faster than `Query` for single entities** — prefer `TryGet`
- Use `SingleInstanceEntity` struct; cache once, store in system

## Testing Systems

- Use `UnitySystemTestBase<T>` for world lifecycle management
- Expose system constructors via `[InternalsVisibleTo]`
- Use NSubstitute for mocking dependencies
- Test multiple scenarios: creation, update (dirty flag), cancellation, cleanup

### Code Example — System Test

From `AvatarLoaderSystemShould.cs`:

```csharp
public class AvatarLoaderSystemShould : UnitySystemTestBase<AvatarLoaderSystem>
{
    [SetUp]
    public void Setup()
    {
        IRealmData realmData = Substitute.For<IRealmData>();
        system = new AvatarLoaderSystem(world);
    }

    [Test]
    public void StartAvatarLoad()
    {
        //Arrange
        Entity entity = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY);

        //Act
        system.Update(0);

        //Assert
        AvatarShapeComponent comp = world.Get<AvatarShapeComponent>(entity);
        Assert.AreEqual(comp.BodyShape.Value, BODY_SHAPE_MALE);
    }

    [Test]
    public void CancelAvatarLoad()
    {
        Entity entity = world.Create(pbAvatarShape, PartitionComponent.TOP_PRIORITY);
        system.Update(0);

        ref AvatarShapeComponent comp = ref world.Get<AvatarShapeComponent>(entity);
        Entity originalPromise = comp.WearablePromise.Entity;

        pbAvatarShape.BodyShape = BODY_SHAPE_FEMALE;
        pbAvatarShape.IsDirty = true;
        system.Update(0);

        // Old promise destroyed, new one created
        Assert.That(world.IsAlive(originalPromise), Is.False);
        Assert.AreNotEqual(comp.WearablePromise.Entity, originalPromise);
    }
}
```
