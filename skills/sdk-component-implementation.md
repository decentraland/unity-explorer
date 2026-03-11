# SDK Component Implementation

## Activation

Use this skill when implementing a new SDK7 component end-to-end, modifying an existing SDK component's systems, or adding result components for scene-to-explorer communication.

## Sources

- `docs/how-to-implement-new-sdk-components.md` -- Step-by-step implementation guide
- `docs/scene-runtime.md` -- SDK7 scene execution and CRDT bridge
- `Explorer/Assets/DCL/Infrastructure/ProtobufPartialClasses/IDirtyMarker.cs` -- IDirtyMarker registration
- `Explorer/Assets/DCL/Infrastructure/Global/ComponentsContainer.cs` -- Component registration
- `Explorer/Assets/DCL/Infrastructure/Global/StaticContainer.cs` -- Plugin instantiation
- `Explorer/Assets/DCL/Infrastructure/ECS/LifeCycle/Systems/ResetDirtyFlagSystem.cs` -- Dirty flag reset
- `Explorer/Assets/Protocol/DecentralandProtocol/ComponentID.gen.cs` -- Generated component IDs

---

## End-to-End Workflow

### Step 1: Protocol Definition

Create protobuf definition in the `protocol` repository with a unique component ID:
- `12xx` -- Main components
- `14xx` -- Experimental components
- `16xx` -- Protocol Squad components

### Step 2: TypeScript Code Generation

In `js-sdk-toolchain`, generate serialization code and optional helper functions.

### Step 3: C# Code Generation + Unity Implementation

In `unity-explorer`:
1. Run protocol update: `npm install @dcl/protocol@experimental && npm run build-protocol`
2. Add partial class to `IDirtyMarker.cs`
3. Register in `ComponentsContainer.cs` using `SDKComponentBuilder<T>`
4. Create feature folder under `Explorer/Assets/DCL/SDKComponents/<Feature>/`
5. Create plugin at `Explorer/Assets/DCL/SDKComponents/<Feature>/Systems/<Feature>Plugin.cs`
6. Instantiate plugin in `StaticContainer.cs` (in the `ECSWorldPlugins` array)
7. Implement systems (lifecycle, properties, cleanup)

### Step 4: Test Scene

Create example test scene in `sdk7-test-scenes`.

### PR Merge Order

Protocol first -> update both `js-sdk-toolchain` and `unity-explorer` -> merge in any order.

---

## Registration Steps

### 1. IDirtyMarker Partial Class

**File:** `Explorer/Assets/DCL/Infrastructure/ProtobufPartialClasses/IDirtyMarker.cs`

Add a partial class with a manually implemented `IsDirty` property:

```csharp
public partial class PBMyComponent : IDirtyMarker
{
    public bool IsDirty { get; set; }
}
```

### 2. ComponentsContainer Registration

**File:** `Explorer/Assets/DCL/Infrastructure/Global/ComponentsContainer.cs`

Register using `SDKComponentBuilder<T>` fluent API. Choose the pattern that fits:

```csharp
// Standard protobuf component (most common)
.Add(SDKComponentBuilder<PBMyComponent>.Create(ComponentID.MY_COMPONENT).AsProtobufComponent())

// Result component (explorer -> scene, LWW)
.Add(SDKComponentBuilder<PBMyResult>.Create(ComponentID.MY_RESULT).AsProtobufResult())

// Custom pool + serializer (rare, e.g. SDKTransform)
.Add(SDKComponentBuilder<SDKTransform>.Create(ComponentID.TRANSFORM)
    .WithPool(t => { t.Clear(); SDKComponentBuilderExtensions.SetAsDirty(t); })
    .WithCustomSerializer(new SDKTransformSerializer())
    .Build())
```

### 3. StaticContainer Plugin Instantiation

**File:** `Explorer/Assets/DCL/Infrastructure/Global/StaticContainer.cs`

Add the plugin to the `ECSWorldPlugins` array (~line 274):

```csharp
new MyFeaturePlugin(poolsRegistry, assetsProvisioner),
```

---

## Feature Folder Structure

Real example from `Explorer/Assets/DCL/SDKComponents/LightSource/`:

```
Assets/DCL/SDKComponents/<Feature>/
+-- Components/
|   +-- <Feature>Component.cs              // Internal ECS struct component
+-- Prefab/                                 // Optional: Unity prefabs for pooled objects
+-- Systems/
|   +-- DCL.<Feature>.Systems.asmref       // { "reference": "DCL.Plugins" }
|   +-- <Feature>Plugin.cs                 // Plugin lives WITH the systems it injects
|   +-- <Feature>LifecycleSystem.cs         // Create/destroy internal components
|   +-- <Feature>ApplyPropertiesSystem.cs   // Apply SDK data to Unity objects
|   +-- CleanUp<Feature>System.cs           // Cleanup on removal/destruction
|   +-- <Feature>Group.cs                  // Custom SystemGroup for execution ordering
+-- Tests/
|   +-- EditMode/
|   |   +-- <Feature>SystemShould.cs
|   +-- EditMode.asmref                    // { "reference": "DCL.EditMode.Tests" }
```

**Assembly & naming notes:**
- The `.asmref` for `DCL.Plugins` goes **inside `Systems/`**, named `DCL.<Feature>.Systems.asmref`.
- Tests: `.asmref` pointing to `DCL.EditMode.Tests` (or `DCL.PlayMode.Tests` for PlayMode).
- Do not create a standalone `.asmdef` for ECS systems.

See `plugin-architecture.md` § "Assembly Structure" for full assembly, naming, and plugin placement rules.

---

## Plugin Creation

Two interfaces exist: `IDCLWorldPlugin<TSettings>` (has `InitializeAsync` + nested `Settings` class) and `IDCLWorldPluginWithoutSettings` (no async init). See `LightSourcePlugin.cs` and `TweenPlugin.cs` for examples of each.

**File:** `Explorer/Assets/DCL/SDKComponents/<Feature>/Systems/<Feature>Plugin.cs`

> Plugins with ECS systems live in the `Systems/` folder alongside those systems. Only plugins **without** ECS systems go into `PluginSystem/Global/` or `PluginSystem/World/`. See `plugin-architecture.md` § "Plugin file placement".

```csharp
// With settings (LightSourcePlugin pattern)
public class MyPlugin : IDCLWorldPlugin<MyPlugin.MySettings>
{
    public MyPlugin(IComponentPoolsRegistry poolsRegistry, IAssetsProvisioner assetsProvisioner) { ... }

    public async UniTask InitializeAsync(MySettings settings, CancellationToken ct)
    {
        // Load prefabs, create pools, etc.
    }

    public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder,
        in ECSWorldInstanceSharedDependencies sharedDependencies,
        in PersistentEntities persistentEntities,
        List<IFinalizeWorldSystem> finalizeWorldSystems,
        List<ISceneIsCurrentListener> sceneIsCurrentListeners)
    {
        ResetDirtyFlagSystem<PBMyComponent>.InjectToWorld(ref builder);
        var lifecycle = MyLifecycleSystem.InjectToWorld(ref builder, ...);
        MyApplyPropertiesSystem.InjectToWorld(ref builder, ...);
        finalizeWorldSystems.Add(lifecycle);
    }

    [Serializable]
    public class MySettings : IDCLPluginSettings { public AssetReferenceGameObject Prefab; }
    public void Dispose() { }
}

// Without settings (TweenPlugin pattern) -- implement IDCLWorldPluginWithoutSettings instead
```

---

## System Patterns

### Lifecycle System (first-occurrence detection)

From `LightSourceLifecycleSystem.cs` -- `[None]` detects entities with SDK component but no internal component yet:

```csharp
[Query]
[None(typeof(LightSourceComponent))]  // Matches only NEW entities
private void CreateLightSourceComponent(in Entity entity, ref PBLightSource pbLightSource,
    in TransformComponent transform)
{
    Light light = poolRegistry.Get();
    light.transform.SetParent(transform.Transform, false);
    World.Add(entity, new LightSourceComponent(light));
    pbLightSource.IsDirty = true;  // Force downstream update
}
```

The system inherits `BaseUnityLoopSystem` and `IFinalizeWorldSystem`, with `[UpdateInGroup(typeof(LightSourcesGroup))]`.

### Apply Properties System (IsDirty guard)

From `LightSourceApplyPropertiesSystem.cs`:

```csharp
[Query]
private void UpdateLightSource(in PBLightSource pbLightSource, ref LightSourceComponent component)
{
    component.LightSourceInstance.enabled = LightSourceHelper.IsPBLightSourceActive(pbLightSource, ...);
    if (pbLightSource.IsDirty) ApplyPBLightSource(pbLightSource, ref component);
}
```

`ResetDirtyFlagSystem<T>` resets `IsDirty = false` at end of frame -- systems do NOT manually reset it.

### Cleanup System (three scenarios)

From `CleanUpAudioSourceSystem.cs` -- all three cleanup triggers with query attributes:

```csharp
// 1. SDK component removed (entity alive): [None(typeof(PBAudioSource), typeof(DeleteEntityIntention))]
// 2. Entity destroyed:                     [All(typeof(DeleteEntityIntention))]
// 3. World disposed:                       IFinalizeWorldSystem.FinalizeComponents(in Query)
[UpdateInGroup(typeof(CleanUpGroup))]
[ThrottlingEnabled]
public partial class CleanUpAudioSourceSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
{
    protected override void Update(float t)
    {
        HandleEntityDestructionQuery(World);
        HandleComponentRemovalQuery(World);
        World.Remove<AudioSourceComponent>(in HandleComponentRemoval_QueryDescription);
    }

    [Query] [None(typeof(PBAudioSource), typeof(DeleteEntityIntention))]
    private void HandleComponentRemoval(ref AudioSourceComponent component) { Release(ref component); }

    [Query] [All(typeof(DeleteEntityIntention))]
    private void HandleEntityDestruction(ref AudioSourceComponent component) { Release(ref component); }

    public void FinalizeComponents(in Query query) { /* InlineQuery to release all remaining */ }
}
```

### Custom SystemGroup (only when needed)

Most systems can use existing groups (e.g., `ComponentInstantiationGroup`, `CleanUpGroup`). Only create a custom group when you need fine-grained ordering between multiple systems in the same feature:

```csharp
[UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
[UpdateAfter(typeof(ComponentInstantiationGroup))]
[ThrottlingEnabled]
public partial class LightSourcesGroup { }
```

---

## CRDT Bridge (Result Components)

Use `IECSToCRDTWriter` (available via `sharedDependencies.EcsToCRDTWriter`) to send data back to the scene runtime.

```csharp
// LWW PUT -- simple (overwrites prior state)
ecsToCRDTWriter.PutMessage<PBAudioAnalysis>(sdkComponent, entity);

// LWW PUT -- with prepare callback
ecsToCRDTWriter.PutMessage<PBRealmInfo, (IRealmData rd, CommsRoomInfo ci)>(
    static (c, d) => { c.BaseUrl = d.rd.Ipfs.CatalystBaseUrl.Value; c.RealmName = d.rd.RealmName; },
    SpecialEntitiesID.SCENE_ROOT_ENTITY, (realmData, commsRoomInfo));

// GOVS APPEND -- accumulates values (events)
ecsToCRDTWriter.AppendMessage<PBAudioEvent, (MediaState state, uint ts)>(
    static (e, d) => { e.State = d.state; e.Timestamp = d.ts; },
    sdkEntity, (int)sceneStateProvider.TickNumber, (mediaState, sceneStateProvider.TickNumber));

// DELETE
ecsToCRDTWriter.DeleteMessage<PBTweenState>(sdkEntity);
```

---

## LWW vs GOVS Components

- **LWW (Last Write Wins):** Standard for most components. Uses `PutMessage`. The latest value overwrites previous. Registered with `.AsProtobufComponent()` or `.AsProtobufResult()`.
- **GOVS (Grow-Only Value Set):** For components that accumulate values (e.g., events). Uses `AppendMessage`. Must be listed in `generateIndex.ts` in the protocol repo.

---

## Test Pattern

From `TweenUpdaterSystemShould.cs`:

```csharp
[TestFixture]
public class TweenUpdaterSystemShould : UnitySystemTestBase<TweenUpdaterSystem>
{
    [SetUp]
    public void SetUp()
    {
        var sceneStateProvider = Substitute.For<ISceneStateProvider>();
        sceneStateProvider.IsCurrent.Returns(true);
        system = new TweenUpdaterSystem(world, Substitute.For<IECSToCRDTWriter>(),
            new TweenerPool(), sceneStateProvider);
    }

    [Test]
    public void ChangingPBTweenUpdatesState()
    {
        Entity entity = world.Create(new PBTween { ... }, new SDKTweenComponent { ... });
        system!.Update(0);
        Assert.IsTrue(world.Get<SDKTweenComponent>(entity).TweenStateStatus == TweenStateStatus.TsActive);
    }
}
```

`UnitySystemTestBase<T>` provides `world` and `system` fields. Mock with `Substitute.For<T>()`. Constructors are `internal` + `[InternalsVisibleTo]`.

Prefer **EditMode** tests -- they are faster and sufficient for most system logic. Only use **PlayMode** tests when you need Unity lifecycle callbacks (Awake, Start, coroutines) or real frame timing that EditMode cannot provide.

---

## Implementation Checklist

- [ ] **IDirtyMarker** -- add partial class in `IDirtyMarker.cs`
- [ ] **ComponentsContainer** -- register with `SDKComponentBuilder<T>` in `ComponentsContainer.cs`
- [ ] **StaticContainer** -- instantiate plugin in `ECSWorldPlugins` array in `StaticContainer.cs`
- [ ] **Plugin** -- create at `DCL/SDKComponents/<Feature>/Systems/<Feature>Plugin.cs`
- [ ] **ResetDirtyFlagSystem** -- inject `ResetDirtyFlagSystem<PBComponent>.InjectToWorld(ref builder)` in plugin
- [ ] **Lifecycle system** -- `[None(typeof(InternalComponent))]` for first-occurrence detection
- [ ] **Properties system** -- `IsDirty` guard on expensive updates
- [ ] **Cleanup system** -- handle component removal, entity destruction, world finalization
- [ ] **SystemGroup** -- create custom group with `[UpdateInGroup]` and `[ThrottlingEnabled]`
- [ ] **Tests** -- `UnitySystemTestBase<T>` with NSubstitute mocks
- [ ] **Result component** (if needed) -- `IECSToCRDTWriter` PUT/APPEND in `ComponentsContainer`
- [ ] Allocation optimization (no LINQ, no closures in Update)
- [ ] Scene-current checks where needed (`ISceneStateProvider.IsCurrent`)
