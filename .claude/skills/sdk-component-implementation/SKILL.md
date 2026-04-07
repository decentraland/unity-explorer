---
name: sdk-component-implementation
description: "End-to-end SDK7 component implementation from protocol to C# systems. Use when implementing new SDK components (PB* types, protobuf, CRDT), modifying SDK component systems, or registering in ComponentsContainer."
user-invocable: false
---

# SDK Component Implementation

## Sources

- `docs/how-to-implement-new-sdk-components.md` -- Step-by-step implementation guide
- `docs/scene-runtime.md` -- SDK7 scene execution and CRDT bridge

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

```csharp
public partial class PBMyComponent : IDirtyMarker
{
    public bool IsDirty { get; set; }
}
```

### 2. ComponentsContainer Registration

**File:** `Explorer/Assets/DCL/Infrastructure/Global/ComponentsContainer.cs`

```csharp
// Standard protobuf component (most common)
.Add(SDKComponentBuilder<PBMyComponent>.Create(ComponentID.MY_COMPONENT).AsProtobufComponent())

// Result component (explorer -> scene, LWW)
.Add(SDKComponentBuilder<PBMyResult>.Create(ComponentID.MY_RESULT).AsProtobufResult())
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
|   |   +-- EditMode.asmref                // { "reference": "DCL.EditMode.Tests" }
```

**Assembly notes:** `.asmref` for `DCL.Plugins` goes inside `Systems/`. Tests use `DCL.EditMode.Tests`. Do not create standalone `.asmdef` for ECS systems. See **plugin-architecture** skill for full rules.

---

## Detailed Reference

For plugin code examples, system patterns (lifecycle, apply-properties, cleanup, groups), CRDT bridge usage (`IECSToCRDTWriter`, LWW vs GOVS), and test patterns, see [reference.md](reference.md).

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
