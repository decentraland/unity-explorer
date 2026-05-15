# ECS System & Component Design -- Detailed Reference

## BillboardSystem -- Clean System Example

From `BillboardSystem.cs`:

```csharp
[UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
[UpdateAfter(typeof(UpdateTransformSystem))]
public partial class BillboardSystem : BaseUnityLoopSystem
{
    private const float MINIMUM_DISTANCE_TO_ROTATE_SQR = 0.25f * 0.25f;
    private readonly IExposedCameraData exposedCameraData;

    // Internal constructor with shared dependencies only (see CLAUDE.md section 1)
    internal BillboardSystem(World world, IExposedCameraData exposedCameraData) : base(world)
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

---

## Full Cleanup Lifecycle -- CleanUpAudioSourceSystem

From `CleanUpAudioSourceSystem.cs` -- shows all three cleanup triggers + `IForEach<T>` struct pattern:

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

    // IForEach<T> struct -- allocation-free callback for InlineQuery
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

---

## AvatarLoaderSystemShould -- System Test Example

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
