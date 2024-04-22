using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.ColorComponent;

namespace DCL.Multiplayer.SDK.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerComponentsHandlerSystem))]
    [LogCategory(ReportCategory.PLAYER_AVATAR_BASE)]
    public partial class WriteSDKAvatarBaseSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<PBAvatarBase> componentPool;

        public WriteSDKAvatarBaseSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<PBAvatarBase> componentPool) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.componentPool = componentPool;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateAvatarBaseQuery(World);
            CreateAvatarBaseQuery(World);
        }

        [Query]
        [None(typeof(PBAvatarBase), typeof(DeleteEntityIntention))]
        private void CreateAvatarBase(in Entity entity, ref PlayerSDKDataComponent playerSDKDataComponent)
        {
            PBAvatarBase pbComponent = componentPool.Get();
            pbComponent.Name = playerSDKDataComponent.Name;
            pbComponent.BodyShapeUrn = playerSDKDataComponent.BodyShapeURN;
            pbComponent.SkinColor = playerSDKDataComponent.SkinColor.ToColor3();
            pbComponent.EyesColor = playerSDKDataComponent.EyesColor.ToColor3();
            pbComponent.HairColor = playerSDKDataComponent.HairColor.ToColor3();

            ecsToCRDTWriter.PutMessage<PBAvatarBase, PBAvatarBase>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.Name = pbComponent.Name;
                dispatchedPBComponent.BodyShapeUrn = pbComponent.BodyShapeUrn;
                dispatchedPBComponent.SkinColor = pbComponent.SkinColor;
                dispatchedPBComponent.EyesColor = pbComponent.EyesColor;
                dispatchedPBComponent.HairColor = pbComponent.HairColor;
            }, playerSDKDataComponent.CRDTEntity, pbComponent);

            World.Add(entity, pbComponent, playerSDKDataComponent.CRDTEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarBase(in Entity entity, ref PlayerSDKDataComponent playerSDKDataComponent, ref PBAvatarBase pbComponent)
        {
            if (!playerSDKDataComponent.IsDirty) return;

            pbComponent.Name = playerSDKDataComponent.Name;
            pbComponent.BodyShapeUrn = playerSDKDataComponent.BodyShapeURN;
            pbComponent.SkinColor = playerSDKDataComponent.SkinColor.ToColor3();
            pbComponent.EyesColor = playerSDKDataComponent.EyesColor.ToColor3();
            pbComponent.HairColor = playerSDKDataComponent.HairColor.ToColor3();

            ecsToCRDTWriter.PutMessage<PBAvatarBase, PBAvatarBase>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.Name = pbComponent.Name;
                dispatchedPBComponent.BodyShapeUrn = pbComponent.BodyShapeUrn;
                dispatchedPBComponent.SkinColor = pbComponent.SkinColor;
                dispatchedPBComponent.EyesColor = pbComponent.EyesColor;
                dispatchedPBComponent.HairColor = pbComponent.HairColor;
            }, playerSDKDataComponent.CRDTEntity, pbComponent);

            World.Set(entity, pbComponent);
        }

        [Query]
        [All(typeof(PBAvatarBase))]
        [None(typeof(PlayerSDKDataComponent), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref CRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarBase>(crdtEntity);
            World.Add(entity, new DeleteEntityIntention());
            World.Remove<PBAvatarBase, CRDTEntity>(entity);
        }
    }
}
