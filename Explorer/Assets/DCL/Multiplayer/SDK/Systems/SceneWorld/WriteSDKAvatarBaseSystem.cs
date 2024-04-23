using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.LifeCycle.Systems;
using ECS.Unity.ColorComponent;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedPostRenderingSystemGroup))]
    [UpdateBefore(typeof(ResetDirtyFlagSystem<PlayerProfileDataComponent>))]
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
        private void CreateAvatarBase(in Entity entity, ref PlayerProfileDataComponent playerProfileDataComponent)
        {
            PBAvatarBase pbComponent = componentPool.Get();
            pbComponent.Name = playerProfileDataComponent.Name;
            pbComponent.BodyShapeUrn = playerProfileDataComponent.BodyShapeURN;
            pbComponent.SkinColor = playerProfileDataComponent.SkinColor.ToColor3();
            pbComponent.EyesColor = playerProfileDataComponent.EyesColor.ToColor3();
            pbComponent.HairColor = playerProfileDataComponent.HairColor.ToColor3();

            ecsToCRDTWriter.PutMessage<PBAvatarBase, PBAvatarBase>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.Name = pbComponent.Name;
                dispatchedPBComponent.BodyShapeUrn = pbComponent.BodyShapeUrn;
                dispatchedPBComponent.SkinColor = pbComponent.SkinColor;
                dispatchedPBComponent.EyesColor = pbComponent.EyesColor;
                dispatchedPBComponent.HairColor = pbComponent.HairColor;
            }, playerProfileDataComponent.CRDTEntity, pbComponent);

            World.Add(entity, pbComponent, playerProfileDataComponent.CRDTEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarBase(in Entity entity, ref PlayerProfileDataComponent playerProfileDataComponent, ref PBAvatarBase pbComponent)
        {
            if (!playerProfileDataComponent.IsDirty) return;

            pbComponent.Name = playerProfileDataComponent.Name;
            pbComponent.BodyShapeUrn = playerProfileDataComponent.BodyShapeURN;
            pbComponent.SkinColor = playerProfileDataComponent.SkinColor.ToColor3();
            pbComponent.EyesColor = playerProfileDataComponent.EyesColor.ToColor3();
            pbComponent.HairColor = playerProfileDataComponent.HairColor.ToColor3();

            ecsToCRDTWriter.PutMessage<PBAvatarBase, PBAvatarBase>(static (dispatchedPBComponent, pbComponent) =>
            {
                dispatchedPBComponent.Name = pbComponent.Name;
                dispatchedPBComponent.BodyShapeUrn = pbComponent.BodyShapeUrn;
                dispatchedPBComponent.SkinColor = pbComponent.SkinColor;
                dispatchedPBComponent.EyesColor = pbComponent.EyesColor;
                dispatchedPBComponent.HairColor = pbComponent.HairColor;
            }, playerProfileDataComponent.CRDTEntity, pbComponent);

            World.Set(entity, pbComponent);
        }

        [Query]
        [All(typeof(PBAvatarBase))]
        [None(typeof(PlayerProfileDataComponent), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref CRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarBase>(crdtEntity);
            World.Remove<PBAvatarBase, CRDTEntity>(entity);
        }
    }
}
