using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.ColorComponent;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateBefore(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class WriteSDKAvatarBaseSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        public WriteSDKAvatarBaseSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateAvatarBaseQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarBase(PlayerSceneCRDTEntity playerCRDTEntity, ProfileSDKSubProduct profile)
        {
            if (!profile.IsDirty) return;

            ecsToCRDTWriter.PutMessage<PBAvatarBase, ProfileSDKSubProduct>(static (pbComponent, profile) =>
            {
                pbComponent.Name = profile.Name;
                ProfileSDKSubProduct.AvatarSubProduct avatar = profile.Avatar;
                pbComponent.BodyShapeUrn = avatar.BodyShape;
                pbComponent.SkinColor = avatar.SkinColor.ToColor3();
                pbComponent.EyesColor = avatar.EyesColor.ToColor3();
                pbComponent.HairColor = avatar.HairColor.ToColor3();
            }, playerCRDTEntity.CRDTEntity, profile);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(PlayerSceneCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarBase>(playerCRDTEntity.CRDTEntity);
        }
    }
}
