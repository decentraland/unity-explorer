# Testing Infrastructure -- Detailed Reference

## UnitySystemTestBase Full API

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

---

## ECSTestUtils.SceneDataSub Code

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

---

## .asmref File Format

```json
{
    "reference": "GUID:da80994a355e49d5b84f91c0a84a721f"
}
```

---

## Lifecycle Testing Pattern

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

---

## Multi-Frame Simulation

```csharp
system.Update(0); // Frame 1: creates component
pbAudioSource.IsDirty = true;
system.Update(0); // Frame 2: processes dirty flag
```

---

## DeleteEntityIntention Testing

```csharp
Entity e = world.Create(scene, new DeleteEntityIntention(), new SceneDefinitionComponent());
system.Update(0f);
scene.Received(1).DisposeAsync();
```

---

## IFinalizeWorldSystem Testing

```csharp
var finalizeSystem = Substitute.For<IFinalizeWorldSystem>();
// ... wire into ECSWorldFacade ...
ecsWorldFacade.Dispose();
finalizeSystem.Received(1).FinalizeComponents(Arg.Any<Query>());
```

---

## Verification Example

```csharp
// Verify a mock was called
ecsToCRDTWriter.Received(1).PutMessage(...);

// Verify pool release count
pool.Received(100).Release(Arg.Is<object>(o => o.GetType() == typeof(TestComponent1)));
```

---

## Async Test -- UniTask Pattern

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

---

## AssetPromise Consumption in Tests

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
