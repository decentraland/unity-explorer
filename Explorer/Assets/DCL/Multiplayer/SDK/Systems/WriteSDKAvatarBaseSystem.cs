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
using ECS.LifeCycle.Components;
using ECS.Unity.ColorComponent;
using ECS.Unity.Groups;

namespace DCL.Multiplayer.SDK.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
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
            ecsToCRDTWriter.PutMessage<PBAvatarBase, PlayerSDKDataComponent>(static (pbAvatarBase, playerSDKDataComponent) =>
            {
                pbAvatarBase.Name = playerSDKDataComponent.Name;
                pbAvatarBase.BodyShapeUrn = playerSDKDataComponent.BodyShapeURN;
                pbAvatarBase.SkinColor = playerSDKDataComponent.SkinColor.ToColor3();
                pbAvatarBase.EyesColor = playerSDKDataComponent.EyesColor.ToColor3();
                pbAvatarBase.HairColor = playerSDKDataComponent.HairColor.ToColor3();
            }, playerSDKDataComponent.CRDTEntity, playerSDKDataComponent);

            PBAvatarBase pbComponent = componentPool.Get();
            pbComponent.Name = playerSDKDataComponent.Name;
            pbComponent.BodyShapeUrn = playerSDKDataComponent.BodyShapeURN;

            pbComponent.SkinColor = playerSDKDataComponent.SkinColor.ToColor3();
            pbComponent.EyesColor = playerSDKDataComponent.EyesColor.ToColor3();
            pbComponent.HairColor = playerSDKDataComponent.HairColor.ToColor3();
            World.Add(entity, pbComponent, playerSDKDataComponent.CRDTEntity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarBase(in Entity entity, ref PlayerSDKDataComponent playerSDKDataComponent, ref PBAvatarBase pbComponent)
        {
            if (!playerSDKDataComponent.IsDirty) return;

            ecsToCRDTWriter.PutMessage<PBAvatarBase, PlayerSDKDataComponent>(static (pbAvatarBase, playerSDKDataComponent) =>
            {
                pbAvatarBase.Name = playerSDKDataComponent.Name;
                pbAvatarBase.BodyShapeUrn = playerSDKDataComponent.BodyShapeURN;
                pbAvatarBase.SkinColor = playerSDKDataComponent.SkinColor.ToColor3();
                pbAvatarBase.EyesColor = playerSDKDataComponent.EyesColor.ToColor3();
                pbAvatarBase.HairColor = playerSDKDataComponent.HairColor.ToColor3();
            }, playerSDKDataComponent.CRDTEntity, playerSDKDataComponent);

            pbComponent.Name = playerSDKDataComponent.Name;
            pbComponent.BodyShapeUrn = playerSDKDataComponent.BodyShapeURN;
            pbComponent.SkinColor = playerSDKDataComponent.SkinColor.ToColor3();
            pbComponent.EyesColor = playerSDKDataComponent.EyesColor.ToColor3();
            pbComponent.HairColor = playerSDKDataComponent.HairColor.ToColor3();
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
