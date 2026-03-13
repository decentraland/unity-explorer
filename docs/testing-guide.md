# Testing Guide

## Overview

The Unity Explorer project uses **NUnit** as its test framework and **NSubstitute** for mocking. Tests are split into two categories -- **EditMode** and **PlayMode** -- each compiled into a separate assembly. The vast majority of tests are EditMode tests for ECS systems, built on top of `UnitySystemTestBase<T>`, the project's purpose-built base class that manages world lifecycle and provides test helpers.

This guide covers how to set up, write, and organize tests in this codebase.

---

## Test Types

### EditMode Tests

Use EditMode tests for pure logic that does not require a running player loop: ECS systems, serialization, utility functions, math helpers, and anything that can be validated without MonoBehaviours or coroutines.

EditMode tests run inside the Unity Editor only. They do not load scenes and do not have access to `UniTask.Yield` or frame-based callbacks. This makes them fast and deterministic.

**Root assembly:** `DCL.EditMode.Tests` at `Explorer/Assets/DCL/Tests/Editor/DCL.EditMode.Tests.asmdef`

The assembly is configured with `includePlatforms: ["Editor"]`, explicit references to `nunit.framework.dll` and `NSubstitute.dll`, and the `UNITY_INCLUDE_TESTS` define constraint.

### PlayMode Tests

Use PlayMode tests when you need the full Unity runtime: MonoBehaviour lifecycle, scene loading, `UniTask.Yield`, or the complete dependency container via `IntegrationTestsSuite`.

PlayMode tests run with the runtime player loop on all platforms. They are slower than EditMode tests and should be reserved for scenarios that genuinely require runtime behavior.

**Root assembly:** `DCL.PlayMode.Tests` at `Explorer/Assets/DCL/Tests/PlayMode/DCL.PlayMode.Tests.asmdef` (compiles for all platforms, requires `UNITY_INCLUDE_TESTS`).

---

## Assembly Setup

### The `.asmref` Pattern

The two root test assemblies act as containers. Individual feature test folders do not define their own `.asmdef` files. Instead, each feature test folder contains a small **assembly reference file** (`.asmref`) that references one of the root assemblies by GUID, allowing test code to compile as part of that assembly.

A typical `.asmref` file (`SDKAudioSourceComponent.Tests.asmref`):

```json
{
    "reference": "GUID:da80994a355e49d5b84f91c0a84a721f"
}
```

The GUID above points to `DCL.EditMode.Tests`. For PlayMode references, the GUID points to `DCL.PlayMode.Tests` instead.

**Naming conventions for `.asmref` files:**

- `{Feature}.Tests.asmref` -- EditMode (most common)
- `{Feature}.EditMode.Tests.asmref` -- when a feature has both EditMode and PlayMode tests
- `{Feature}.PlayMode.Tests.asmref` -- PlayMode tests

### The `[InternalsVisibleTo]` Pattern

System constructors are marked `internal` by convention. To let test assemblies instantiate systems directly, production assemblies include an `AssemblyInfo.cs` that grants visibility to the test assemblies.

A real example from `Explorer/Assets/DCL/Infrastructure/SceneRuntime/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DCL.EditMode.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
```

The `DynamicProxyGenAssembly2` entry is required for NSubstitute to create proxies of `internal` types.

---

## Writing ECS System Tests

### Test Base Class: `UnitySystemTestBase<T>`

Located at `Explorer/Assets/DCL/Infrastructure/ECS/TestSuite/UnitySystemTestBase.cs`, this abstract class is the foundation for all ECS system tests. It provides:

- **Lazy world creation** -- accessing the `world` property creates a new `World` on demand, pre-populated with a `SceneShortInfo(Vector2Int.zero, "TEST")` entity.
- **Automatic cleanup** -- `[TearDown]` disposes both the system and the world, so tests do not leak resources.
- **Convenience helpers** -- `AddTransformToEntity`, `AddMaterialToEntity`, and `AddUITransformToEntity` delegate to `EcsTestsUtils` for common entity setup.

**Usage pattern:** In your `[SetUp]`, create the system by calling its constructor directly and assign it to the `system` field. Override `OnTearDown()` if you need custom cleanup beyond the automatic system/world disposal.

### Complete Test Example

Below is a real test from the codebase (`StartAudioClipLoadingSystemShould.cs`) that demonstrates the standard pattern:

```csharp
public class StartAudioClipLoadingSystemShould : UnitySystemTestBase<StartAudioSourceLoadingSystem>
{
    private PBAudioSource pbAudioSource;
    private Entity entity;

    [SetUp]
    public void SetUp()
    {
        // Create mocks for system dependencies
        IPerformanceBudget budget = Substitute.For<IPerformanceBudget>();
        budget.TrySpendBudget().Returns(true);
        ISceneData sceneData = ECSTestUtils.SceneDataSub();

        // Instantiate system directly via internal constructor
        system = new StartAudioSourceLoadingSystem(world, sceneData, budget);

        // Populate the world with test data
        pbAudioSource = AudioSourceTestsUtils.CreatePBAudioSource();
        entity = world.Create(pbAudioSource, PartitionComponent.TOP_PRIORITY);
    }

    [Test]
    public void CreateAudioSourceComponentForPBAudioSource()
    {
        // Act -- simulate one frame
        system.Update(0);

        // Assert -- verify the system added the expected component
        Assert.That(world.TryGet(entity, out AudioSourceComponent comp), Is.True);
        Assert.That(comp.AudioClipUrl, Is.EqualTo(pbAudioSource.AudioClipUrl));
        Assert.That(comp.ClipPromise, Is.Not.Null);
    }
}
```

The pattern is always the same: extend `UnitySystemTestBase<T>`, build the system in `[SetUp]`, call `system.Update(0)` in each test, and assert against world state.

### Multi-Frame Simulation

Call `system.Update(0)` repeatedly to simulate multiple frames. This is essential for testing systems that process dirty flags, state transitions, or multi-step pipelines:

```csharp
// Frame 1: system creates component from PB source
system.Update(0);

// Modify the protobuf component to simulate a scene update
ref var pbTween = ref world.TryGetRef<PBTween>(entity, out bool exists);
pbTween.IsDirty = true;
pbTween.Playing = false;

// Frame 2: system processes the dirty flag
system.Update(0);

// Assert the state transition
SDKTweenComponent comp = world.Get<SDKTweenComponent>(entity);
Assert.That(comp.TweenStateStatus, Is.EqualTo(TweenStateStatus.TsPaused));
```

### Testing Cleanup

ECS systems must clean up when entities are destroyed or the world is disposed.

#### `DeleteEntityIntention` for Entity Destruction

Add `DeleteEntityIntention` to an entity to signal that it should be destroyed. Cleanup systems should detect this and release resources:

```csharp
[Test]
public void DisposeLoadedScene()
{
    // Arrange
    ISceneFacade scene = Substitute.For<ISceneFacade>();
    Entity e = world.Create(scene, new DeleteEntityIntention(), new SceneDefinitionComponent());

    // Act
    system.Update(0f);

    // Assert
    scene.Received(1).DisposeAsync();
}
```

#### `IFinalizeWorldSystem` for World Disposal

When a world is disposed (e.g., player moves away from a scene), systems implementing `IFinalizeWorldSystem` run their `FinalizeComponents` method. Test this by mocking the interface and verifying the call:

```csharp
[Test]
public void DisposeProperly()
{
    // Arrange -- finalizeWorldSystem is Substitute.For<IFinalizeWorldSystem>()
    // wired into ECSWorldFacade during SetUp

    // Act
    ecsWorldFacade.Dispose();

    // Assert
    finalizeWorldSystem.Received(1).FinalizeComponents(Arg.Any<Query>());
}
```

---

## Test Utilities

### EcsTestsUtils (Static Helpers)

Located at `Explorer/Assets/DCL/Infrastructure/ECS/TestSuite/EcsTestsUtils.cs`. These methods create common component setups needed by many tests.

| Method | Purpose |
|--------|---------|
| `AddTransformToEntity(world, entity, isDirty)` | Creates a GameObject, attaches a `TransformComponent` and an `SDKTransform` with default values |
| `AddMaterialToEntity(world, entity)` | Creates a GameObject, adds a `MaterialComponent` with a default URP Lit material |
| `SetUpFeaturesRegistry(params string[] flags)` | Initializes `FeatureFlagsConfiguration` and `FeaturesRegistry` with the given flags enabled |
| `TearDownFeaturesRegistry()` | Resets `FeatureFlagsConfiguration` and `FeaturesRegistry` singletons |

> **Note:** `UnitySystemTestBase<T>` exposes instance wrappers for these helpers, so you can call `AddTransformToEntity(entity)` directly without passing the world.

### ECSTestUtils (Mock Helpers)

Located at `Explorer/Assets/DCL/Tests/Editor/ECSTestUtils.cs`. Provides pre-configured mocks for commonly needed interfaces.

```csharp
public static ISceneData SceneDataSub()
{
    ISceneData sceneData = Substitute.For<ISceneData>();
    sceneData.TryGetContentUrl(Arg.Any<string>(), out Arg.Any<URLAddress>())
             .Returns(args =>
             {
                 args[1] = URLAddress.FromString(args.ArgAt<string>(0));
                 return true;
             });
    return sceneData;
}
```

Use `ECSTestUtils.SceneDataSub()` whenever a system constructor requires `ISceneData`. It maps any content path string directly to a `URLAddress`, which is sufficient for most unit tests.

---

## Mocking with NSubstitute

### Common Mocks

The following interfaces are frequently mocked across the test suite. Each entry shows the typical setup pattern:

| Interface | Setup Pattern |
|-----------|---------------|
| `ISceneData` | `ECSTestUtils.SceneDataSub()` -- maps content paths to URLs |
| `ISceneStateProvider` | `sceneStateProvider.IsCurrent.Returns(true)` |
| `IPerformanceBudget` | `budget.TrySpendBudget().Returns(true)` |
| `IComponentPoolsRegistry` | `poolsRegistry.GetReferenceTypePool<T>().Returns(pool)` |
| `IECSToCRDTWriter` | `Substitute.For<IECSToCRDTWriter>()` -- then verify with `.Received()` |
| `IFinalizeWorldSystem` | `Substitute.For<IFinalizeWorldSystem>()` -- verify `FinalizeComponents` called |
| `IRealmData` | `Substitute.For<IRealmData>()` |

### Verification Patterns

NSubstitute's `.Received()` is the standard way to verify mock interactions:

```csharp
// Verify a method was called exactly once
scene.Received(1).DisposeAsync();

// Verify with argument matching
ecsToCRDTWriter.Received(1).PutMessage(...);

// Verify a pool released the correct type, 100 times
pool.Received(100).Release(Arg.Is<object>(o => o.GetType() == typeof(TestComponent1)));

// Verify a specific count of calls to a registry lookup
componentsPoolRegistry.Received(100).GetPool(typeof(TestComponent1));
```

Use `Arg.Any<T>()` for arguments you do not care about and `Arg.Is<T>(predicate)` for arguments that must match a condition.

---

## Async Test Patterns

### `async Task` with `[Test]`

NUnit supports `async Task` test methods natively. Always use `async Task` (never `async void`) for async tests:

```csharp
[Test]
public async Task TweenMoveUpdatesToFinalValueAfterDuration()
{
    // Arrange
    Vector3 startValue = CreateVector3(0, 0, 0);
    Vector3 endValue = CreateVector3(10, 0, 5);
    Entity testEntity = CreateTransformTween<Move>(500, startValue, endValue);

    // Act -- simulate time passing
    await RunSystemForSeconds(500, testEntity);

    // Assert
    SDKTweenComponent comp = world.Get<SDKTweenComponent>(testEntity);
    var tweener = (Vector3Tweener)comp.CustomTweener;
    Assert.AreEqual(endValue.X, tweener.CurrentValue.x, 0.01f);
    Assert.AreEqual(TweenStateStatus.TsCompleted, comp.TweenStateStatus);
}
```

### AssetPromise Consumption Simulation

Many systems create an `AssetPromise` on the first frame and consume the result on a subsequent frame. To simulate this in tests, manually add a `StreamableLoadingResult<T>` to the promise entity:

```csharp
// Frame 1: system creates the promise
system.Update(0);

// Retrieve the promise from the component
ref var component = ref world.Get<AudioSourceComponent>(entity);
Entity promiseEntity = component.ClipPromise.Entity;

// Simulate the asset loading system resolving the promise
world.Add(promiseEntity, new StreamableLoadingResult<AudioClipData>(testAudioData));

// Frame 2: system consumes the resolved promise
system.Update(0);

// Assert the result was applied
Assert.That(world.Get<AudioSourceComponent>(entity).AudioSource, Is.Not.Null);
```

### CancellationToken Testing

Use `CancellationTokenSource` to test cancellation paths. Create the source, pass `cts.Token` to the code under test, then call `cts.Cancel()` and verify resources are cleaned up.

> **Warning:** In production code, always check `ct.IsCancellationRequested` rather than calling `ThrowIfCancellationRequested()`. Verify that the code under test handles cancellation without throwing `OperationCanceledException` into unobserved contexts.

---

## Integration Tests

### `IntegrationTestsSuite.CreateStaticContainer()`

Located at `Explorer/Assets/DCL/Infrastructure/Global/Tests/PlayMode/IntegrationTestsSuite.cs`, this static method spins up the full dependency container -- `StaticContainer` and `SceneSharedContainer` -- with real Addressables and properly initialized singletons. It is the entry point for PlayMode integration tests that need the complete application context.

```csharp
var (staticContainer, sceneSharedContainer) = await IntegrationTestsSuite.CreateStaticContainer(ct);
```

This initializes feature flags, loads `PluginSettingsContainer` assets from Addressables, creates a real `StaticContainer` with mocked externals (identity, Ethereum API), initializes all ECS world plugins, and returns a `SceneSharedContainer` for scene-level dependencies.

**When to use integration tests vs EditMode tests:** Prefer EditMode tests for isolated system logic -- they are fast and reliable in CI. Reserve integration tests for verifying that real containers wire up correctly or when behavior depends on the full plugin initialization chain.

---

## Test Organization Conventions

### Naming

- **Test classes:** `{Feature}Should` (preferred) or `{Feature}Tests`. Examples: `StartAudioClipLoadingSystemShould`, `InstantiateTransformUnitySystemShould`, `ReleasePoolableComponentSystemShould`.
- **Test methods:** Describe expected behavior in plain language. Examples: `CreateAudioSourceComponentForPBAudioSource`, `DisposeLoadedScene`, `ReleaseAllComponentsToPools`.

### AAA Pattern

Structure test bodies with `// Arrange`, `// Act`, `// Assert` comments. This is the project convention and makes test intent immediately clear:

```csharp
[Test]
public void InstantiateTransformComponent()
{
    // Arrange
    world.Create(sdkTransform, new CRDTEntity(10));

    // Act
    system.Update(0f);

    // Assert
    QueryDescription entityWithUnityTransform = new QueryDescription().WithAll<SDKTransform, TransformComponent>();
    Assert.AreEqual(1, world.CountEntities(in entityWithUnityTransform));
}
```

### Folder Structure

Tests live alongside the feature code they test, connected to the root assembly via `.asmref`:

```
Feature/
  Systems/
    MySystem.cs
  Tests/
    MySystemShould.cs
    Feature.Tests.asmref           -- references DCL.EditMode.Tests
```

When a feature has both EditMode and PlayMode tests:

```
Feature/
  Tests/
    EditMode/
      Feature.EditMode.Tests.asmref
      FeatureShould.cs
    PlayMode/
      Feature.PlayMode.Tests.asmref
      FeatureIntegrationShould.cs
```

---

## See Also

- [standards.md](standards.md) -- memory allocation rules, testing expectations, performance tests
- [development-guide.md](development-guide.md) -- ECS system design, component manipulation, cleanup patterns, `UnitySystemTestBase` reference
- [async-programming.md](async-programming.md) -- `SuppressToResultAsync`, cancellation patterns, exception-free `Result` type
