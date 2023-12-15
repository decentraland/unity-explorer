using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Profiles;
using Decentraland.Common;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.Unity.ColorComponent;
using UnityEngine;
using Entity = Arch.Core.Entity;
using Promise = ECS.StreamableLoading.Common.AssetPromise<
    DCL.AvatarRendering.Wearables.Components.IWearable[],
    DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarLoaderSystem : BaseUnityLoopSystem
    {
        internal AvatarLoaderSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateAvatarByProfileQuery(World);
            LoadNewAvatarQuery(World);
            UpdateAvatarQuery(World);
        }

        // TODO: remove PBAvatarShape as middleware, instead use Profile directly
        [Query]
        [None(typeof(AvatarShapeComponent))]
        private void LoadNewAvatar(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partition)
        {
            Promise wearablePromise = CreateWearablePromise(pbAvatarShape, partition);
            pbAvatarShape.IsDirty = false;
            World.Add(entity, new AvatarShapeComponent(pbAvatarShape.Name, pbAvatarShape.Id, pbAvatarShape, wearablePromise, pbAvatarShape.SkinColor.ToUnityColor(), pbAvatarShape.HairColor.ToUnityColor()));
        }

        // TODO: remove PBAvatarShape as middleware, instead use Profile directly
        [Query]
        private void UpdateAvatar(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!pbAvatarShape.IsDirty)
                return;

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                avatarShapeComponent.WearablePromise.ForgetLoading(World);

            Promise newPromise = CreateWearablePromise(pbAvatarShape, partition);
            avatarShapeComponent.WearablePromise = newPromise;

            avatarShapeComponent.BodyShape = pbAvatarShape;
            avatarShapeComponent.IsDirty = true;
            pbAvatarShape.IsDirty = false;
        }

        // TODO: remove PBAvatarShape as middleware, instead use Profile directly
        [Query]
        private void UpdateAvatarByProfile(in Entity entity, ref Profile profile, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partition)
        {
            Color avatarSkinColor = profile.Avatar.SkinColor;
            Color avatarHairColor = profile.Avatar.HairColor;

            World.Set(entity, new PBAvatarShape
            {
                Id = profile.UserId,
                BodyShape = profile.Avatar.BodyShape,
                Wearables = { profile.Avatar.SharedWearables },
                Name = profile.Name,
                SkinColor = new Color3
                {
                    R = avatarSkinColor.r,
                    B = avatarSkinColor.b,
                    G = avatarSkinColor.g,
                },
                HairColor = new Color3
                {
                    R = avatarHairColor.r,
                    B = avatarHairColor.b,
                    G = avatarHairColor.g,
                },
                IsDirty = true,
            });

            World.Remove<Profile>(entity);
        }

        private Promise CreateWearablePromise(PBAvatarShape pbAvatarShape, PartitionComponent partition) =>
            Promise.Create(World,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(pbAvatarShape, pbAvatarShape.Wearables),
                partition);
    }
}
