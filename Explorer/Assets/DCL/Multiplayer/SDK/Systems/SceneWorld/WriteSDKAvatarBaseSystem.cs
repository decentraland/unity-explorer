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

        public override void Initialize()
        {
            UpdateAvatarBaseQuery(World, true);
            UpdateAvatarBaseWithTalkingQuery(World, true);
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateAvatarBaseQuery(World, false);
            UpdateAvatarBaseWithTalkingQuery(World, false);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(SDKTalking))]
        private void UpdateAvatarBase([Data] bool force, PlayerSceneCRDTEntity playerCRDTEntity, SDKProfile profile)
        {
            if (!force && !profile.IsDirty) return;

            ecsToCRDTWriter.PutMessage<PBAvatarBase, SDKProfile>(static (pbComponent, profile) => FillAvatarBase(pbComponent, profile), playerCRDTEntity.CRDTEntity, profile);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateAvatarBaseWithTalking([Data] bool force, PlayerSceneCRDTEntity playerCRDTEntity, SDKProfile profile, in SDKTalking talking)
        {
            if (!force && !profile.IsDirty && !talking.IsDirty) return;

            ecsToCRDTWriter.PutMessage<PBAvatarBase, (SDKProfile profile, bool talking)>(static (pbComponent, data) =>
            {
                FillAvatarBase(pbComponent, data.profile);
                pbComponent.Talking = data.talking;
            }, playerCRDTEntity.CRDTEntity, (profile, talking.IsTalking));
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(PlayerSceneCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarBase>(playerCRDTEntity.CRDTEntity);
        }

        private static void FillAvatarBase(PBAvatarBase pbComponent, SDKProfile profile)
        {
            pbComponent.Name = profile.Name;
            SDKProfile.SDKAvatar avatar = profile.Avatar;
            pbComponent.BodyShapeUrn = avatar.BodyShape;
            pbComponent.SkinColor = avatar.SkinColor.ToColor3();
            pbComponent.EyesColor = avatar.EyesColor.ToColor3();
            pbComponent.HairColor = avatar.HairColor.ToColor3();
        }
    }
}
