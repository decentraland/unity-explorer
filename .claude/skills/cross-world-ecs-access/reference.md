# Cross-World ECS Access — Detailed Reference

## InputModifierPlugin — Plugin Injection Example

```csharp
public class InputModifierPlugin : IDCLWorldPlugin
{
    private readonly Arch.Core.World world;       // global world
    private readonly Entity playerEntity;          // global player entity

    public InputModifierPlugin(Arch.Core.World world, Entity playerEntity,
        ISceneRestrictionBusController sceneRestrictionBusController) { ... }

    public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
        in ECSWorldInstanceSharedDependencies sharedDependencies,
        in PersistentEntities persistentEntities, ...)
    {
        var system = InputModifierHandlerSystem.InjectToWorld(
            ref builder, world, playerEntity, sharedDependencies.SceneStateProvider, ...);
        sceneIsCurrentListeners.Add(system);
        finalizeWorldSystems.Add(system);
    }
}
```

---

## Pattern A: Direct Global World Access — InputModifierHandlerSystem

```csharp
public partial class InputModifierHandlerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
{
    private readonly World globalWorld;
    private readonly Entity playerEntity;

    internal InputModifierHandlerSystem(World world, World globalWorld, Entity playerEntity, ...) : base(world)
    {
        this.globalWorld = globalWorld;
        this.playerEntity = playerEntity;
    }

    [Query]
    private void ApplyModifiers([Data] bool skipDirtyCheck, Entity entity,
        in PBInputModifier pbInputModifier, in CRDTEntity crdtEntity)
    {
        // Read scene-world SDK component, write to global-world component
        ref var inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
        inputModifier.DisableAll = pbInputModifier.Standard.DisableAll;
        // ...
    }
}
```

---

## Pattern B: CRDT Bridge — WriteSDKAvatarBaseSystem

```csharp
public partial class WriteSDKAvatarBaseSystem : BaseUnityLoopSystem
{
    private readonly IECSToCRDTWriter ecsToCRDTWriter;

    [Query]
    [None(typeof(DeleteEntityIntention))]
    private void UpdateAvatarBase(PlayerSceneCRDTEntity playerCRDTEntity, SDKProfile profile)
    {
        if (!profile.IsDirty) return;
        ecsToCRDTWriter.PutMessage<PBAvatarBase, SDKProfile>(static (pb, profile) =>
        {
            pb.Name = profile.Name;
            pb.BodyShapeUrn = profile.Avatar.BodyShape;
        }, playerCRDTEntity.CRDTEntity, profile);
    }
}
```

---

## SingleInstanceEntity and WorldExtensions

```csharp
// From WorldExtensions.cs
public static SingleInstanceEntity CacheCamera(this World world) =>
    new (in QUERY, world);

public static ref CameraComponent GetCameraComponent(this in SingleInstanceEntity instance, World world) =>
    ref world.Get<CameraComponent>(instance);
```

---

## Global-to-Scene Propagation — PlayerTransformPropagationSystem

```csharp
[UpdateInGroup(typeof(PreRenderingSystemGroup))]
public partial class PlayerTransformPropagationSystem : BaseUnityLoopSystem
{
    [Query]
    [None(typeof(DeleteEntityIntention))]
    private void PropagateTransformToScene(in CharacterTransform characterTransform,
        in PlayerCRDTEntity playerCRDTEntity)
    {
        if (!characterTransform.Transform.hasChanged || !playerCRDTEntity.AssignedToScene) return;
        if (playerCRDTEntity.CRDTEntity.Id == SpecialEntitiesID.PLAYER_ENTITY) return;

        World sceneEcsWorld = playerCRDTEntity.SceneFacade!.EcsExecutor.World;

        if (!sceneEcsWorld.TryGet<SDKTransform>(playerCRDTEntity.SceneWorldEntity, out SDKTransform? sdkTransform))
            sceneEcsWorld.Add(playerCRDTEntity.SceneWorldEntity, sdkTransform = sdkTransformPool.Get());

        sdkTransform!.Position.Value = characterTransform.Transform.position;
        sdkTransform.Rotation.Value = characterTransform.Transform.rotation;
        sdkTransform.IsDirty = true;
    }
}
```

---

## ISceneIsCurrentListener Full Lifecycle

```csharp
public partial class InputModifierHandlerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
{
    protected override void Update(float t)
    {
        if (!sceneStateProvider.IsCurrent) return;   // guard every frame
        ApplyModifiersQuery(World, false);
        HandleComponentRemovalQuery(World);
    }

    public void OnSceneIsCurrentChanged(bool value)
    {
        if (value)
            ApplyModifiersQuery(World, true);        // re-apply on becoming current
        else
            ResetModifiers();                        // reset global state on leaving
    }

    private void ResetModifiers()
    {
        ref InputModifierComponent inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
        inputModifier.RemoveAllModifiers();
    }
}
```

Register the system in the plugin:

```csharp
sceneIsCurrentListeners.Add(system);
```
