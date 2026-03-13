---
name: testing-infrastructure
description: "Testing patterns and infrastructure — UnitySystemTestBase, ECS test utilities, mocking strategies, EditMode vs PlayMode tests. Use when writing tests for ECS systems, controllers, or async code, setting up test worlds, mocking ECS dependencies with NSubstitute, creating test entities, or choosing between EditMode and PlayMode test types."
user-invocable: false
---

# Testing Infrastructure

## Sources

- `docs/standards.md` (testing section) -- NUnit, NSubstitute, coverage expectations, performance tests
- `docs/development-guide.md` (system testing section) -- `UnitySystemTestBase<T>`, `[InternalsVisibleTo]`

---

## Test Base Classes

### `UnitySystemTestBase<TSystem>`

Located at `Explorer/Assets/DCL/Infrastructure/ECS/TestSuite/UnitySystemTestBase.cs`. Provides world lifecycle for ECS system tests:

```csharp
public abstract class UnitySystemTestBase<TSystem> where TSystem : BaseUnityLoopSystem
{
    protected TSystem? system;
    protected World world { get; } // Lazy-created; includes SceneShortInfo entity

    [TearDown] public void DestroyWorld() { OnTearDown(); system?.Dispose(); world?.Dispose(); }
    protected virtual void OnTearDown() { }

    // Convenience helpers (delegate to EcsTestsUtils)
    protected TransformComponent AddTransformToEntity(in Entity entity, bool isDirty = false, World world = null);
    protected MaterialComponent AddMaterialToEntity(in Entity entity, bool isDirty = false, World world = null);
    protected UITransformComponent AddUITransformToEntity(in Entity entity, bool isDirty = false);
}
```

**Key behaviors:**
- World is lazily created on first access with a default `SceneShortInfo(Vector2Int.zero, "TEST")` entity.
- `[TearDown]` disposes system and world automatically. Override `OnTearDown()` for custom cleanup.
- Assign `system` in `[SetUp]` by calling the system constructor directly.

---

## Test Utilities

### `EcsTestsUtils` (static helpers)

Located at `Explorer/Assets/DCL/Infrastructure/ECS/TestSuite/EcsTestsUtils.cs`:

| Method | Purpose |
|--------|---------|
| `AddTransformToEntity(world, entity, isDirty)` | Creates a GameObject, adds `TransformComponent` + `SDKTransform` |
| `AddMaterialToEntity(world, entity)` | Creates a GameObject, adds `MaterialComponent` with a default URP Lit material |
| `SetUpFeaturesRegistry(params string[] flags)` | Initializes `FeatureFlagsConfiguration` and `FeaturesRegistry` for tests |
| `TearDownFeaturesRegistry()` | Resets feature flags singletons |

### `ECSTestUtils` (mocking helpers)

Located at `Explorer/Assets/DCL/Tests/Editor/ECSTestUtils.cs`:

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

Use `ECSTestUtils.SceneDataSub()` whenever a system requires `ISceneData` -- it maps any content path to a `URLAddress`.

---

## EditMode vs PlayMode Tests

### When to use each

| Type | Use when | Requires |
|------|----------|----------|
| **EditMode** | Pure logic, ECS systems, serialization, utilities -- no scene or coroutines needed | Editor only |
| **PlayMode** | Integration tests needing MonoBehaviours, scene loading, `UniTask.Yield`, or full containers | Runtime player loop |

### Assembly structure

Two root test assemblies exist:

- `DCL.EditMode.Tests` -- `Explorer/Assets/DCL/Tests/Editor/DCL.EditMode.Tests.asmdef` (platform: Editor only)
- `DCL.PlayMode.Tests` -- `Explorer/Assets/DCL/Tests/PlayMode/DCL.PlayMode.Tests.asmdef` (all platforms)

Both require `UNITY_INCLUDE_TESTS` define constraint and reference `nunit.framework.dll` and `NSubstitute.dll`.

### `.asmref` pattern

Feature-local test folders reference the root assembly via `.asmref` files:

```json
{
    "reference": "GUID:da80994a355e49d5b84f91c0a84a721f"
}
```

Naming convention: `{Feature}.Tests.asmref` for EditMode (most common), `{Feature}.EditMode.Tests.asmref` or `{Feature}.PlayMode.Tests.asmref` when both types exist for the same feature.

### `[InternalsVisibleTo]` pattern

System constructors are `internal`. Expose them to tests via an `AssemblyInfo.cs` in the production assembly:

```csharp
[assembly: InternalsVisibleTo("DCL.EditMode.Tests")]
[assembly: InternalsVisibleTo("DCL.PlayMode.Tests")]
```

---

## Mocking with NSubstitute

### Common mocks

| Interface | Typical setup |
|-----------|---------------|
| `ISceneData` | `ECSTestUtils.SceneDataSub()` (maps content paths to URLs) |
| `ISceneStateProvider` | `sceneStateProvider.IsCurrent.Returns(true)` |
| `IPerformanceBudget` | `budget.TrySpendBudget().Returns(true)` |
| `IComponentPoolsRegistry` | Mock pool chain: `poolsRegistry.GetReferenceTypePool<T>().Returns(pool)` |
| `IECSToCRDTWriter` | `Substitute.For<IECSToCRDTWriter>()` then verify with `.Received()` |
| `IFinalizeWorldSystem` | `Substitute.For<IFinalizeWorldSystem>()` then verify `FinalizeComponents` called |
| `IRealmData` | `Substitute.For<IRealmData>()` |

### Verification example

```csharp
// Verify a mock was called
ecsToCRDTWriter.Received(1).PutMessage(...);

// Verify pool release count
pool.Received(100).Release(Arg.Is<object>(o => o.GetType() == typeof(TestComponent1)));
```

---

## System Test Patterns

> See CLAUDE.md sections 1 and 10 for condensed testing rules.

### Lifecycle testing pattern

Test creation, update, dirty-flag re-processing, cancellation, and cleanup:

```csharp
public class StartAudioClipLoadingSystemShould : UnitySystemTestBase<StartAudioSourceLoadingSystem>
{
    [SetUp]
    public void SetUp()
    {
        system = CreateSystem(world);
        entity = world.Create(pbAudioSource, PartitionComponent.TOP_PRIORITY);
    }

    [Test]
    public void CreateAudioSourceComponentForPBAudioSource()
    {
        // Act
        system.Update(0);

        // Assert
        Assert.That(world.TryGet(entity, out AudioSourceComponent comp), Is.True);
        Assert.That(comp.AudioClipUrl, Is.EqualTo(pbAudioSource.AudioClipUrl));
    }
}
```

### Multi-frame simulation

Call `system.Update(0)` repeatedly to simulate multiple frames:

```csharp
system.Update(0); // Frame 1: creates component
pbAudioSource.IsDirty = true;
system.Update(0); // Frame 2: processes dirty flag
```

### `DeleteEntityIntention` testing

Add `DeleteEntityIntention` to trigger entity destruction cleanup:

```csharp
Entity e = world.Create(scene, new DeleteEntityIntention(), new SceneDefinitionComponent());
system.Update(0f);
scene.Received(1).DisposeAsync();
```

### `IFinalizeWorldSystem` testing

Mock the interface and verify `FinalizeComponents` is called on dispose:

```csharp
var finalizeSystem = Substitute.For<IFinalizeWorldSystem>();
// ... wire into ECSWorldFacade ...
ecsWorldFacade.Dispose();
finalizeSystem.Received(1).FinalizeComponents(Arg.Any<Query>());
```

---

## Async Test Patterns

### UniTask in tests

Use `async Task` (not `async void`) with `[Test]`:

```csharp
[Test]
public async Task RestoreCameraDataOnFailureAsync(
    [Values(UniTaskStatus.Faulted, UniTaskStatus.Canceled)] UniTaskStatus status)
{
    var loadReport = AsyncLoadProcessReport.Create(CancellationToken.None);
    Entity e = world.Create(characterController, new CharacterRigidTransform(), teleportIntent);

    if (status == UniTaskStatus.Faulted)
    {
        loadReport.SetException(new Exception("test"));
        LogAssert.Expect(LogType.Exception, new Regex(".*test.*"));
    }
    else
        loadReport.SetCancelled();

    await loadReport.WaitUntilFinishedAsync();
    system!.Update(0);

    Assert.That(cameraSamplingData.IsDirty, Is.True);
}
```

### `AssetPromise` consumption in tests

Simulate promise resolution by adding `StreamableLoadingResult<T>` to the promise entity:

```csharp
// Create promise during SetUp or first Update
system.Update(0);

// Simulate asset loaded
world.Add(component.ClipPromise.Entity, new StreamableLoadingResult<AudioClipData>(data));

// Process the resolved promise
system.Update(0);

// Assert consumption
Assert.That(world.Get<AudioSourceComponent>(entity).AudioSource, Is.Not.Null);
```

---

## Test Organization

### Naming

- **Class:** `{Feature}Should` (preferred) or `{Feature}Tests`
- **Methods:** Describe expected behavior -- `CreateAudioSourceFromResolvedPromise`, `DisposeLoadedScene`
- **Body:** Split with `// Arrange`, `// Act`, `// Assert` comments

### Folder structure

```
Feature/
  Tests/
    {Feature}Should.cs
    {Feature}.Tests.asmref         -- references DCL.EditMode.Tests
  Tests/EditMode/
    {Feature}.EditMode.Tests.asmref
  Tests/PlayMode/
    {Feature}.PlayMode.Tests.asmref
```

### PlayMode integration tests

For full integration tests, use `IntegrationTestsSuite.CreateStaticContainer()` to spin up the full dependency container. See `Explorer/Assets/DCL/Infrastructure/Global/Tests/PlayMode/IntegrationTestsSuite.cs`.

---

## Cross-References

- **code-standards** skill -- test naming conventions, AAA pattern, PR standards
- **ecs-system-and-component-design** skill -- system test example at the bottom, cleanup lifecycle patterns
