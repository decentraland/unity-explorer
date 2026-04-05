# SDK Component Implementation — Detailed Reference

## Plugin Creation

Two interfaces exist: `IDCLWorldPlugin<TSettings>` (has `InitializeAsync` + nested `Settings` class) and `IDCLWorldPluginWithoutSettings` (no async init). See `LightSourcePlugin.cs` and `TweenPlugin.cs` for examples.

**File:** `Explorer/Assets/DCL/SDKComponents/<Feature>/Systems/<Feature>Plugin.cs`

> Plugins with ECS systems live in the `Systems/` folder alongside those systems. Only plugins **without** ECS systems go into `PluginSystem/Global/` or `PluginSystem/World/`. See the **plugin-architecture** skill for rules.

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

Cleanup must handle three triggers — see the **ecs-system-and-component-design** skill for full patterns and the `CleanUpAudioSourceSystem` example:

1. **SDK component removed** — `[None(typeof(PBMyComponent), typeof(DeleteEntityIntention))]`
2. **Entity destroyed** — `[All(typeof(DeleteEntityIntention))]`
3. **World disposed** — implement `IFinalizeWorldSystem.FinalizeComponents(in Query)`

Place in `[UpdateInGroup(typeof(CleanUpGroup))]` with `[ThrottlingEnabled]`.

### Custom SystemGroup (only when needed)

Most systems can use existing groups. Only create a custom group when you need fine-grained ordering between multiple systems in the same feature:

```csharp
[UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
[UpdateAfter(typeof(ComponentInstantiationGroup))]
[ThrottlingEnabled]
public partial class LightSourcesGroup { }
```

---

## CRDT Bridge (Result Components)

Use `IECSToCRDTWriter` (via `sharedDependencies.EcsToCRDTWriter`) to send data back to the scene runtime:

```csharp
// LWW PUT -- overwrites prior state
ecsToCRDTWriter.PutMessage<PBAudioAnalysis>(sdkComponent, entity);

// LWW PUT -- with prepare callback (use static lambda to avoid allocation)
ecsToCRDTWriter.PutMessage<PBRealmInfo, (IRealmData rd, CommsRoomInfo ci)>(
    static (c, d) => { c.BaseUrl = d.rd.Ipfs.CatalystBaseUrl.Value; },
    SpecialEntitiesID.SCENE_ROOT_ENTITY, (realmData, commsRoomInfo));

// GOVS APPEND -- accumulates values (events); DELETE -- removes component
ecsToCRDTWriter.AppendMessage<PBAudioEvent, ...>(static (e, d) => { ... }, sdkEntity, tickNumber, data);
ecsToCRDTWriter.DeleteMessage<PBTweenState>(sdkEntity);
```

### LWW vs GOVS Components

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
