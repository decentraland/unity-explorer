using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.Unity.ColorComponent;
using System;
using Entity = Arch.Core.Entity;
using Promise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution,
    DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape.Systems
{
    /// <summary>
    ///     Start loading the avatar shape for the entity from <see cref="Profile" /> or <see cref="PBAvatarShape" /> components.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarLoaderSystem : BaseUnityLoopSystem
    {
        internal AvatarLoaderSystem(World world) : base(world) { }

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
            Promise wearablePromise = CreateWearablePromise(pbAvatarShape, partition);
            pbAvatarShape.IsDirty = false;
            World.Add(entity, new AvatarShapeComponent(pbAvatarShape.Name, pbAvatarShape.Id, pbAvatarShape, wearablePromise, pbAvatarShape.SkinColor.ToUnityColor(), pbAvatarShape.HairColor.ToUnityColor(), pbAvatarShape.EyeColor.ToUnityColor()));
        }

        [Query]
        [None(typeof(AvatarShapeComponent), typeof(PBAvatarShape))]
        private void CreateAvatarShapeFromProfile(in Entity entity, in Profile profile, ref PartitionComponent partition)
        {
            Promise wearablePromise = CreateWearablePromise(profile, partition);
            profile.IsDirty = false;
            World.Add(entity, new AvatarShapeComponent(profile.Name, profile.UserId, profile.Avatar.BodyShape, wearablePromise, profile.Avatar.SkinColor, profile.Avatar.HairColor, profile.Avatar.EyesColor));
        }

        [Query]
        [None(typeof(Profile))]
        private void UpdateAvatarFromSDKComponent(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
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

        [Query]
        [None(typeof(PBAvatarShape))]
        private void UpdateAvatarFromProfile(ref Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!profile.IsDirty)
                return;

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                avatarShapeComponent.WearablePromise.ForgetLoading(World);

            Promise newPromise = CreateWearablePromise(profile, partition);
            avatarShapeComponent.WearablePromise = newPromise;

            avatarShapeComponent.BodyShape = profile.Avatar.BodyShape;
            avatarShapeComponent.IsDirty = true;
            profile.IsDirty = false;
        }

        private Promise CreateWearablePromise(PBAvatarShape pbAvatarShape, PartitionComponent partition) =>
            Promise.Create(World,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(pbAvatarShape, pbAvatarShape.Wearables, Array.Empty<string>()),
                partition);

        private Promise CreateWearablePromise(Profile profile, PartitionComponent partition) =>

            // profile.Avatar.Wearables should be shortened, but since GetWearablesByPointers already retrieves shortened-urns,
            // there is not need to convert
            Promise.Create(World,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(profile.Avatar.BodyShape, profile.Avatar.Wearables, profile.Avatar.ForceRender),
                partition);
    }
}
