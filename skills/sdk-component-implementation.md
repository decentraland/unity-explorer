# SDK Component Implementation

## Activation

Use this skill when implementing a new SDK7 component end-to-end — from protocol definition through C# code generation to system implementation and testing.

## Sources

- `docs/how-to-implement-new-sdk-components.md` — Step-by-step implementation guide
- `docs/scene-runtime.md` — SDK7 scene execution and CRDT bridge

---

## End-to-End Workflow

### Step 1: Protocol Definition

Create protobuf definition in the `protocol` repository with a unique component ID:
- `12xx` — Main components
- `14xx` — Experimental components
- `16xx` — Protocol Squad components

### Step 2: TypeScript Code Generation

In `js-sdk-toolchain`, generate serialization code and optional helper functions.

### Step 3: C# Code Generation + Unity Implementation

In `unity-explorer`:
1. Run protocol update: `npm install @dcl/protocol@experimental && npm run build-protocol`
2. Add partial class to `IDirtyMarker`
3. Register in `ComponentsContainer`
4. Create feature folder and plugin
5. Implement systems

### Step 4: Test Scene

Create example test scene in `sdk7-test-scenes`.

### PR Merge Order

Protocol first → update both `js-sdk-toolchain` and `unity-explorer` → merge in any order.

## C# Implementation Details

### IDirtyMarker Registration

Each SDK component needs a partial class added to `IDirtyMarker` for dirty-flag tracking:

```csharp
public partial class PBMyComponent : IDirtyMarker
{
    // Auto-generated IsDirty property
}
```

### ComponentsContainer Registration

Register the component in the container chain so it can be deserialized from CRDT messages:

```csharp
// In ComponentsContainer
container.Register<PBMyComponent>(ComponentID.MY_COMPONENT);
```

### Feature Folder Structure

```
Assets/DCL/SDKComponents/<Feature>/
├── Components/
│   └── <Feature>Component.cs          // Internal ECS component
├── Systems/
│   ├── <Feature>LifecycleSystem.cs     // Create/destroy internal components
│   ├── <Feature>ApplyPropertiesSystem.cs // Apply SDK data to Unity objects
│   └── CleanUp<Feature>System.cs       // Cleanup on removal/destruction
├── Tests/
│   └── EditMode/
│       └── <Feature>SystemShould.cs
└── <Feature>.asmdef
```

### Plugin Creation

Create a world plugin at `DCL/PluginSystem/World/` and instantiate it in `StaticContainer`:

```csharp
public class MyFeaturePlugin : IDCLWorldPlugin<MyFeaturePlugin.Settings>
{
    public void InjectToWorld(
        ref ArchSystemsWorldBuilder<World> builder,
        in ECSWorldInstanceSharedDependencies sharedDependencies,
        in PersistentEntities persistentEntities,
        List<IFinalizeWorldSystem> finalizeWorldSystems,
        List<ISceneIsCurrentListener> sceneIsCurrentListeners)
    {
        // Always inject dirty-flag reset for SDK components
        ResetDirtyFlagSystem<PBMyComponent>.InjectToWorld(ref builder);

        var lifecycleSystem = MyFeatureLifecycleSystem.InjectToWorld(ref builder, ...);
        MyFeatureApplyPropertiesSystem.InjectToWorld(ref builder, ...);

        finalizeWorldSystems.Add(lifecycleSystem);
    }
}
```

### ResetDirtyFlagSystem Injection

From `LightSourcePlugin.cs` — always inject the dirty-flag reset for SDK components:

```csharp
ResetDirtyFlagSystem<PBLightSource>.InjectToWorld(ref builder);
```

## LWW vs GOVS Components

- **LWW (Last Write Wins):** Uses `PUT` operation. Standard for most components. The latest value wins.
- **GOVS (Grow-Only Value Set):** Uses `APPEND` operation. For components that accumulate values. Must be listed in `generateIndex.ts`.

## IsDirty Handling

Systems must check `IsDirty` before processing SDK component updates:

```csharp
[Query]
private void UpdateFeature(ref PBMyComponent sdkComponent, ref MyComponent internalComponent)
{
    if (!sdkComponent.IsDirty)
        return;

    // Apply changes from SDK component
    internalComponent.Value = sdkComponent.SomeValue;
    sdkComponent.IsDirty = false;
}
```

## Scene-Current Awareness

Systems that should only run in the currently active scene implement `ISceneIsCurrentListener`:

```csharp
// Register in plugin
sceneIsCurrentListeners.Add(mySystem);

// Or check via ISceneStateProvider
if (!sceneStateProvider.IsCurrent)
    return;
```

## CRDT Bridge

- Custom allocation-free CRDT implementation runs off main thread
- `MutexSync` for thread safety between scene thread and main thread
- `SyncedGroup` for system updates synchronized with scene state
- `IECSToCRDTWriter` for outgoing messages: `PUT`, `APPEND`, `DELETE`
- Uses `PoolableCollection` with `ArrayPool<T>.Shared` for zero-allocation deserialization

## Implementation Checklist

- [ ] Systems separation (lifecycle, properties, cleanup)
- [ ] Promise handling for async assets
- [ ] Pool usage for Unity objects
- [ ] Minimal intention components
- [ ] `UpdateGroup` ordering attributes
- [ ] Scene-current checks where needed
- [ ] Test coverage
- [ ] Allocation optimization (no LINQ, no closures in Update)
- [ ] `ResetDirtyFlagSystem<T>` injection in plugin
- [ ] Registration in `ComponentsContainer`
