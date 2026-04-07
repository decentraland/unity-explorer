---
name: testing-infrastructure
description: "Testing patterns -- UnitySystemTestBase, ECS test utilities, mocking, EditMode vs PlayMode. Use when writing tests for ECS systems, controllers, or async code, or choosing test types."
user-invocable: false
---

# Testing Infrastructure

## Sources

- `docs/standards.md` (testing section) -- NUnit, NSubstitute, coverage expectations, performance tests
- `docs/development-guide.md` (system testing section) -- `UnitySystemTestBase<T>`, `[InternalsVisibleTo]`

---

## UnitySystemTestBase<TSystem>

Located at `Explorer/Assets/DCL/Infrastructure/ECS/TestSuite/UnitySystemTestBase.cs`. Provides world lifecycle for ECS system tests.

**Key API:**
- `protected TSystem? system` -- assign in `[SetUp]`
- `protected World world` -- lazily created with a default `SceneShortInfo(Vector2Int.zero, "TEST")` entity
- `AddTransformToEntity(entity, isDirty, world)`, `AddMaterialToEntity(entity, isDirty, world)`, `AddUITransformToEntity(entity, isDirty)` -- convenience helpers delegating to `EcsTestsUtils`

**Key behaviors:**
- World is lazily created on first access with a default scene info entity
- `[TearDown]` disposes system and world automatically; override `OnTearDown()` for custom cleanup
- Assign `system` in `[SetUp]` by calling the system constructor directly

---

## Test Utilities

### EcsTestsUtils (static helpers in TestSuite)

Located at `Explorer/Assets/DCL/Infrastructure/ECS/TestSuite/EcsTestsUtils.cs`:

| Method | Purpose |
|--------|---------|
| `AddTransformToEntity(world, entity, isDirty)` | Creates a GameObject, adds `TransformComponent` + `SDKTransform` |
| `AddMaterialToEntity(world, entity)` | Creates a GameObject, adds `MaterialComponent` with default URP Lit material |
| `SetUpFeaturesRegistry(params string[] flags)` | Initializes `FeatureFlagsConfiguration` and `FeaturesRegistry` for tests |
| `TearDownFeaturesRegistry()` | Resets feature flags singletons |

### ECSTestUtils (mocking helpers in Tests/Editor)

Located at `Explorer/Assets/DCL/Tests/Editor/ECSTestUtils.cs`. Use `ECSTestUtils.SceneDataSub()` whenever a system requires `ISceneData` -- it maps any content path to a `URLAddress`.

> **Note:** `EcsTestsUtils` = static helpers in TestSuite; `ECSTestUtils` = mocking helpers in Tests/Editor. Different classes despite near-identical names.

---

## EditMode vs PlayMode Tests

| Type | Use when | Requires |
|------|----------|----------|
| **EditMode** | Pure logic, ECS systems, serialization, utilities -- no scene or coroutines needed | Editor only |
| **PlayMode** | Integration tests needing MonoBehaviours, scene loading, `UniTask.Yield`, or full containers | Runtime player loop |

### Assembly Structure

- `DCL.EditMode.Tests` -- `Explorer/Assets/DCL/Tests/Editor/DCL.EditMode.Tests.asmdef` (platform: Editor only)
- `DCL.PlayMode.Tests` -- `Explorer/Assets/DCL/Tests/PlayMode/DCL.PlayMode.Tests.asmdef` (all platforms)

Both require `UNITY_INCLUDE_TESTS` define constraint and reference `nunit.framework.dll` and `NSubstitute.dll`.

Feature-local test folders reference the root assembly via `.asmref` files. Naming convention: `{Feature}.Tests.asmref` for EditMode (most common), `{Feature}.EditMode.Tests.asmref` or `{Feature}.PlayMode.Tests.asmref` when both types exist.

### [InternalsVisibleTo] Pattern

System constructors are `internal`. Expose them to tests via an `AssemblyInfo.cs` in the production assembly:

```csharp
[assembly: InternalsVisibleTo("DCL.EditMode.Tests")]
[assembly: InternalsVisibleTo("DCL.PlayMode.Tests")]
```

---

## Common Mocks

| Interface | Typical setup |
|-----------|---------------|
| `ISceneData` | `ECSTestUtils.SceneDataSub()` (maps content paths to URLs) |
| `ISceneStateProvider` | `sceneStateProvider.IsCurrent.Returns(true)` |
| `IPerformanceBudget` | `budget.TrySpendBudget().Returns(true)` |
| `IComponentPoolsRegistry` | Mock pool chain: `poolsRegistry.GetReferenceTypePool<T>().Returns(pool)` |
| `IECSToCRDTWriter` | `Substitute.For<IECSToCRDTWriter>()` then verify with `.Received()` |
| `IFinalizeWorldSystem` | `Substitute.For<IFinalizeWorldSystem>()` then verify `FinalizeComponents` called |
| `IRealmData` | `Substitute.For<IRealmData>()` |

---

## System Test Patterns

- **Lifecycle testing:** Test creation, update, dirty-flag re-processing, cancellation, and cleanup
- **Multi-frame simulation:** Call `system.Update(0)` repeatedly to simulate multiple frames; set `IsDirty = true` between calls
- **DeleteEntityIntention:** Add `DeleteEntityIntention` to trigger entity destruction cleanup, verify disposal calls via `.Received()`
- **IFinalizeWorldSystem:** Mock the interface, wire into `ECSWorldFacade`, call `Dispose()`, verify `FinalizeComponents` called

---

## Async Test Patterns

- Use `async Task` (not `async void`) with `[Test]`
- Simulate `AssetPromise` resolution by adding `StreamableLoadingResult<T>` to the promise entity, then call `system.Update(0)` to consume
- For `UniTask` failures: use `AsyncLoadProcessReport.Create`, set exception/cancelled, then `await WaitUntilFinishedAsync()`

---

## Test Organization

### Naming

- **Class:** `{Feature}Should` (preferred) or `{Feature}Tests`
- **Methods:** Describe expected behavior -- `CreateAudioSourceFromResolvedPromise`, `DisposeLoadedScene`
- **Body:** Split with `// Arrange`, `// Act`, `// Assert` comments

### Folder Structure

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

For full integration tests, use `IntegrationTestsSuite.CreateStaticContainer()` to spin up the full dependency container.

---

## Detailed Reference

For detailed code examples, see [reference.md](reference.md).

---

## Cross-References

- **code-standards** skill -- test naming conventions, AAA pattern, PR standards
- **ecs-system-and-component-design** skill -- system test example at the bottom, cleanup lifecycle patterns
