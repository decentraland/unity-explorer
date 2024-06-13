using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Profiles;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.Unity.ColorComponent;
using System;
using UnityEngine;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution,
    DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    /// <summary>
    ///     Start loading the avatar shape for the entity from <see cref="Profile" /> or <see cref="PBAvatarShape" /> components.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarLoaderSystem : BaseUnityLoopSystem
    {
        internal AvatarLoaderSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            CreateAvatarShapeFromSDKComponentQuery(World);
            UpdateAvatarFromSDKComponentQuery(World);

            CreateAvatarShapeFromProfileQuery(World);
            UpdateAvatarFromProfileQuery(World);
        }

        [Query]
        [None(typeof(AvatarShapeComponent), typeof(Profile))]
        private void CreateAvatarShapeFromSDKComponent(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partition)
        {
            WearablePromise wearablePromise = CreateWearablePromise(pbAvatarShape, partition);
            EmotePromise emotePromise = CreateEmotePromise(pbAvatarShape, partition);
            pbAvatarShape.IsDirty = false;

            World.Add(entity, new AvatarShapeComponent(pbAvatarShape.Name, pbAvatarShape.Id, pbAvatarShape, wearablePromise, emotePromise,
                pbAvatarShape.GetSkinColor().ToUnityColor(),
                pbAvatarShape.GetHairColor().ToUnityColor(),
                pbAvatarShape.GetEyeColor().ToUnityColor()));
        }

        [Query]
        [None(typeof(AvatarShapeComponent), typeof(PBAvatarShape))]
        private void CreateAvatarShapeFromProfile(in Entity entity, in Profile profile, ref PartitionComponent partition)
        {
            WearablePromise wearablePromise = CreateWearablePromise(profile, partition);
            EmotePromise emotePromise = CreateEmotePromise(profile, partition);
            World.Add(entity, new AvatarShapeComponent(profile.Name, profile.UserId, profile.Avatar.BodyShape, wearablePromise, emotePromise, profile.Avatar.SkinColor, profile.Avatar.HairColor, profile.Avatar.EyesColor));
        }

        [Query]
        [None(typeof(Profile), typeof(DeleteEntityIntention))]
        private void UpdateAvatarFromSDKComponent(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!pbAvatarShape.IsDirty)
                return;

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                avatarShapeComponent.WearablePromise.ForgetLoading(World);

            if (!avatarShapeComponent.EmotePromise.IsConsumed)
                avatarShapeComponent.EmotePromise.ForgetLoading(World);

            WearablePromise newPromise = CreateWearablePromise(pbAvatarShape, partition);
            avatarShapeComponent.WearablePromise = newPromise;
            avatarShapeComponent.EmotePromise = CreateEmotePromise(pbAvatarShape, partition);
            avatarShapeComponent.BodyShape = pbAvatarShape;
            avatarShapeComponent.HairColor = pbAvatarShape.GetHairColor().ToUnityColor();
            avatarShapeComponent.SkinColor = pbAvatarShape.GetSkinColor().ToUnityColor();
            avatarShapeComponent.EyesColor = pbAvatarShape.GetEyeColor().ToUnityColor();
            avatarShapeComponent.IsDirty = true;
            pbAvatarShape.IsDirty = false;
        }

        [Query]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void UpdateAvatarFromProfile(ref Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!profile.IsDirty)
                return;

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                avatarShapeComponent.WearablePromise.ForgetLoading(World);

            if (!avatarShapeComponent.EmotePromise.IsConsumed)
                avatarShapeComponent.EmotePromise.ForgetLoading(World);

            WearablePromise newPromise = CreateWearablePromise(profile, partition);
            avatarShapeComponent.ID = profile.UserId;
            avatarShapeComponent.Name = profile.Name;
            avatarShapeComponent.WearablePromise = newPromise;
            avatarShapeComponent.EmotePromise = CreateEmotePromise(profile, partition);
            avatarShapeComponent.BodyShape = profile.Avatar.BodyShape;
            avatarShapeComponent.HairColor = profile.Avatar.HairColor;
            avatarShapeComponent.SkinColor = profile.Avatar.SkinColor;
            avatarShapeComponent.EyesColor = profile.Avatar.EyesColor;
            avatarShapeComponent.IsDirty = true;
        }

        private WearablePromise CreateWearablePromise(PBAvatarShape pbAvatarShape, PartitionComponent partition) =>
            WearablePromise.Create(World,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(pbAvatarShape, pbAvatarShape.Wearables, Array.Empty<string>()),
                partition);

        private WearablePromise CreateWearablePromise(Profile profile, PartitionComponent partition) =>
            // profile.Avatar.Wearables should be shortened, but since GetWearablesByPointers already retrieves shortened-urns,
            // there is not need to convert
            WearablePromise.Create(World,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(profile.Avatar.BodyShape, profile.Avatar.Wearables, profile.Avatar.ForceRender),
                partition);

        private EmotePromise CreateEmotePromise(PBAvatarShape pbAvatarShape, PartitionComponent partition) =>
            EmotePromise.Create(World, EmoteComponentsUtils.CreateGetEmotesByPointersIntention(pbAvatarShape, pbAvatarShape.Emotes), partition);

        private EmotePromise CreateEmotePromise(Profile profile, PartitionComponent partition) =>
            EmotePromise.Create(World, EmoteComponentsUtils.CreateGetEmotesByPointersIntention(profile.Avatar.BodyShape, profile.Avatar.Emotes), partition);
    }
}
