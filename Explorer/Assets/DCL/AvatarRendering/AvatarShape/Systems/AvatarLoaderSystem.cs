using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.Unity.ColorComponent;
using System;
using System.Collections.Generic;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution,
    DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.Components.EmotesResolution,
    DCL.AvatarRendering.Emotes.Components.GetEmotesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    /// <summary>
    ///     Start loading the avatar shape for the entity from <see cref="Profile" /> or <see cref="PBAvatarShape" /> components.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
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
            World.Add(entity, new AvatarShapeComponent(pbAvatarShape.Name, pbAvatarShape.Id, pbAvatarShape, wearablePromise, emotePromise, pbAvatarShape.SkinColor.ToUnityColor(), pbAvatarShape.HairColor.ToUnityColor(), pbAvatarShape.EyeColor.ToUnityColor()));
        }

        [Query]
        [None(typeof(AvatarShapeComponent), typeof(PBAvatarShape))]
        private void CreateAvatarShapeFromProfile(in Entity entity, in Profile profile, ref PartitionComponent partition)
        {
            WearablePromise wearablePromise = CreateWearablePromise(profile, partition);
            EmotePromise emotePromise = CreateEmotePromise(profile, partition);
            profile.IsDirty = false;
            World.Add(entity, new AvatarShapeComponent(profile.Name, profile.UserId, profile.Avatar.BodyShape, wearablePromise, emotePromise, profile.Avatar.SkinColor, profile.Avatar.HairColor, profile.Avatar.EyesColor));
        }

        [Query]
        [None(typeof(Profile))]
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
            avatarShapeComponent.IsDirty = true;
            pbAvatarShape.IsDirty = false;
        }

        [Query]
        [None(typeof(PBAvatarShape))]
        private void UpdateAvatarFromProfile(ref Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!profile.IsDirty)
                return;

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                avatarShapeComponent.WearablePromise.ForgetLoading(World);

            if (!avatarShapeComponent.EmotePromise.IsConsumed)
                avatarShapeComponent.EmotePromise.ForgetLoading(World);

            WearablePromise newPromise = CreateWearablePromise(profile, partition);
            avatarShapeComponent.WearablePromise = newPromise;
            avatarShapeComponent.EmotePromise = CreateEmotePromise(profile, partition);
            avatarShapeComponent.BodyShape = profile.Avatar.BodyShape;
            avatarShapeComponent.IsDirty = true;
            profile.IsDirty = false;
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

        private EmotePromise CreateEmotePromise(PBAvatarShape pbAvatarShape, PartitionComponent partition)
        {
            var urns = new List<URN>();

            foreach (URN emote in pbAvatarShape.Emotes)
            {
                if (emote.IsNullOrEmpty()) continue;
                urns.Add(emote);
            }

            var intention = new GetEmotesByPointersIntention(urns, pbAvatarShape);
            return EmotePromise.Create(World, intention, partition);
        }

        private EmotePromise CreateEmotePromise(Profile profile, PartitionComponent partition)
        {
            var urns = new List<URN>();

            foreach (URN emote in profile.Avatar.Emotes)
            {
                if (emote.IsNullOrEmpty()) continue;
                urns.Add(emote);
            }

            var intention = new GetEmotesByPointersIntention(urns, profile.Avatar.BodyShape);
            return EmotePromise.Create(World, intention, partition);
        }
    }
}
