using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Profiles;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.Unity.ColorComponent;
using System;
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
            UpdateMainPlayerAvatarFromSDKComponentQuery(World);
            UpdateAvatarFromSDKComponentQuery(World);
            CreateMainPlayerAvatarShapeFromProfileQuery(World);
            CreateAvatarShapeFromProfileQuery(World);
            UpdateMainPlayerAvatarFromProfileQuery(World);
            UpdateAvatarFromProfileQuery(World);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(AvatarShapeComponent), typeof(Profile))]
        private void CreateAvatarShapeFromSDKComponent(in Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partition)
        {
            pbAvatarShape.IsDirty = false;

            WearablePromise wearablePromise = CreateWearablePromise(pbAvatarShape, partition);

            World.Add(entity, new AvatarShapeComponent(pbAvatarShape.Name, pbAvatarShape.Id, pbAvatarShape, wearablePromise,
                pbAvatarShape.GetSkinColor().ToUnityColor(),
                pbAvatarShape.GetHairColor().ToUnityColor(),
                pbAvatarShape.GetEyeColor().ToUnityColor()));
        }

        [Query]
        [None(typeof(AvatarShapeComponent), typeof(PBAvatarShape), typeof(PlayerComponent))]
        private void CreateAvatarShapeFromProfile(in Entity entity, in Profile profile, ref PartitionComponent partition)
        {
            WearablePromise wearablePromise = CreateWearablePromise(profile, partition);
            World.Add(entity, new AvatarShapeComponent(profile.Name, profile.UserId, profile.Avatar.BodyShape, wearablePromise, profile.Avatar.SkinColor, profile.Avatar.HairColor, profile.Avatar.EyesColor));
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(AvatarShapeComponent), typeof(PBAvatarShape))]
        private void CreateMainPlayerAvatarShapeFromProfile(in Entity entity, in Profile profile, ref PartitionComponent partition)
        {
            WearablePromise wearablePromise = CreateWearablePromise(profile, partition);

            var avatarShapeComponent = new AvatarShapeComponent(profile.Name, profile.UserId, profile.Avatar.BodyShape, wearablePromise, profile.Avatar.SkinColor, profile.Avatar.HairColor, profile.Avatar.EyesColor);
            // No lazy load for main player. Get all emotes, so it can play them accordingly without undesired delays
            avatarShapeComponent.EmotePromise = CreateEmotePromise(profile, partition);

            World.Add(entity, avatarShapeComponent);
        }

        [Query]
        [None(typeof(Profile), typeof(DeleteEntityIntention))]
        private void UpdateAvatarFromSDKComponent(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!pbAvatarShape.IsDirty)
                return;

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                avatarShapeComponent.WearablePromise.ForgetLoading(World);

            WearablePromise newPromise = CreateWearablePromise(pbAvatarShape, partition);
            avatarShapeComponent.WearablePromise = newPromise;

            avatarShapeComponent.BodyShape = pbAvatarShape;
            avatarShapeComponent.HairColor = pbAvatarShape.GetHairColor().ToUnityColor();
            avatarShapeComponent.SkinColor = pbAvatarShape.GetSkinColor().ToUnityColor();
            avatarShapeComponent.EyesColor = pbAvatarShape.GetEyeColor().ToUnityColor();
            avatarShapeComponent.IsDirty = true;
            pbAvatarShape.IsDirty = false;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(Profile), typeof(DeleteEntityIntention))]
        private void UpdateMainPlayerAvatarFromSDKComponent(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!pbAvatarShape.IsDirty) return;

            UpdateAvatarFromSDKComponent(ref pbAvatarShape, ref avatarShapeComponent, ref partition);

            if (avatarShapeComponent.EmotePromise is { IsConsumed: false })
                avatarShapeComponent.EmotePromise.Value.ForgetLoading(World);

            // No lazy load for main player. Get all emotes, so it can play them accordingly without undesired delays
            avatarShapeComponent.EmotePromise = CreateEmotePromise(pbAvatarShape, partition);
        }

        [Query]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void UpdateAvatarFromProfile(ref Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!profile.IsDirty)
                return;

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                avatarShapeComponent.WearablePromise.ForgetLoading(World);

            WearablePromise newPromise = CreateWearablePromise(profile, partition);
            avatarShapeComponent.ID = profile.UserId;
            avatarShapeComponent.Name = profile.Name;
            avatarShapeComponent.WearablePromise = newPromise;
            avatarShapeComponent.BodyShape = profile.Avatar.BodyShape;
            avatarShapeComponent.HairColor = profile.Avatar.HairColor;
            avatarShapeComponent.SkinColor = profile.Avatar.SkinColor;
            avatarShapeComponent.EyesColor = profile.Avatar.EyesColor;
            avatarShapeComponent.IsDirty = true;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void UpdateMainPlayerAvatarFromProfile(ref Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            UpdateAvatarFromProfile(ref profile, ref avatarShapeComponent, ref partition);

            if (!profile.IsDirty) return;

            if (avatarShapeComponent.EmotePromise is { IsConsumed: false })
                avatarShapeComponent.EmotePromise.Value.ForgetLoading(World);

            // No lazy load for main player. Get all emotes, so it can play them accordingly without undesired delays
            avatarShapeComponent.EmotePromise = CreateEmotePromise(profile, partition);
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
