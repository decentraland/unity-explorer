using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Profiles;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.ColorComponent;
using System;
using System.Collections.Generic;
using UnityEngine;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution,
    DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Start loading the avatar shape for the entity from <see cref="Profile" /> or <see cref="PBAvatarShape" /> components.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class AvatarLoaderSystem : BaseUnityLoopSystem
    {
        internal AvatarLoaderSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateAvatarFromSDKComponentQuery(World);
            UpdateMainPlayerAvatarFromProfileQuery(World);
            UpdateAvatarFromProfileQuery(World);
            UpdateMainPlayerAvatarFromSceneOverrideQuery(World);
            UpdateAvatarFromSceneOverrideQuery(World);

            CreateAvatarShapeFromSDKComponentQuery(World);
            CreateMainPlayerAvatarShapeFromProfileQuery(World);
            CreateAvatarShapeFromProfileQuery(World);

            ConsumeProfileWearableDTOsQuery(World);
        }

        [Query]
        [None(typeof(AvatarShapeComponent), typeof(Profile))]
        private void CreateAvatarShapeFromSDKComponent(Entity entity, ref PBAvatarShape pbAvatarShape, ref PartitionComponent partition)
        {
            WearablePromise wearablePromise = CreateWearablePromise(pbAvatarShape, partition);

            var avatarShape = new AvatarShapeComponent(pbAvatarShape.Name, pbAvatarShape.Id, pbAvatarShape, wearablePromise,
                pbAvatarShape.GetSkinColor().ToUnityColor(),
                pbAvatarShape.GetHairColor().ToUnityColor(),
                pbAvatarShape.GetEyeColor().ToUnityColor(),
                pbAvatarShape is { HasShowOnlyWearables: true, ShowOnlyWearables: true });

            avatarShape.CaptureWearablesSnapshot(pbAvatarShape.Wearables);

            World.Add(entity, avatarShape, new AvatarHighlightComponent());
        }

        [Query]
        [None(typeof(AvatarShapeComponent), typeof(PBAvatarShape), typeof(PlayerComponent))]
        private void CreateAvatarShapeFromProfile(Entity entity, in Profile profile, ref PartitionComponent partition)
        {
            SceneAvatarShapeOverride? sceneOverride = ConsumeSceneAvatarShapeOverride(entity);
            AvatarShapeComponent avatarShape = CreateAvatarShape(profile, in sceneOverride, partition);
            World.Add(entity, avatarShape, new AvatarHighlightComponent());
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(AvatarShapeComponent), typeof(PBAvatarShape))]
        private void CreateMainPlayerAvatarShapeFromProfile(in Entity entity, in Profile profile, ref PartitionComponent partition)
        {
            SceneAvatarShapeOverride? sceneOverride = ConsumeSceneAvatarShapeOverride(entity);
            AvatarShapeComponent avatarShapeComponent = CreateAvatarShape(profile, in sceneOverride, partition);

            // No lazy load for main player. Get all emotes, so it can play them accordingly without undesired delays
            LoadAllEmotes(avatarShapeComponent.BodyShape, profile, partition);

            World.Add(entity, avatarShapeComponent);
        }

        [Query]
        [None(typeof(Profile), typeof(DeleteEntityIntention))]
        private void UpdateAvatarFromSDKComponent(ref PBAvatarShape pbAvatarShape, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!pbAvatarShape.IsDirty)
                return;

            // Always update cosmetic fields
            // Identity fields are always cheap to track and never affect rendering.
            avatarShapeComponent.ID = pbAvatarShape.Id;
            avatarShapeComponent.Name = pbAvatarShape.Name;

            if (avatarShapeComponent.HasStructuralChange(pbAvatarShape))
            {
                avatarShapeComponent.WearablePromise.ForgetLoading(World);

                avatarShapeComponent.WearablePromise = CreateWearablePromise(pbAvatarShape, partition);
                avatarShapeComponent.BodyShape = pbAvatarShape;
                avatarShapeComponent.ShowOnlyWearables = pbAvatarShape is { HasShowOnlyWearables: true, ShowOnlyWearables: true };
                avatarShapeComponent.HairColor = pbAvatarShape.GetHairColor().ToUnityColor();
                avatarShapeComponent.SkinColor = pbAvatarShape.GetSkinColor().ToUnityColor();
                avatarShapeComponent.EyesColor = pbAvatarShape.GetEyeColor().ToUnityColor();
                avatarShapeComponent.CaptureWearablesSnapshot(pbAvatarShape.Wearables);
                avatarShapeComponent.IsDirty = true;
            }
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void UpdateAvatarFromProfile(Entity entity, ref Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!profile.IsDirty)
                return;

            SceneAvatarShapeOverride? sceneOverride = ConsumeSceneAvatarShapeOverride(entity);
            ApplyProfileToAvatarShape(profile, in sceneOverride, ref avatarShapeComponent, ref partition);
            profile.IsDirty = false;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void UpdateMainPlayerAvatarFromProfile(in Entity entity, ref Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!profile.IsDirty)
                return;

            SceneAvatarShapeOverride? sceneOverride = ConsumeSceneAvatarShapeOverride(entity);
            ApplyProfileToAvatarShape(profile, in sceneOverride, ref avatarShapeComponent, ref partition);

            // No lazy load for main player. Get all emotes, so it can play them accordingly without undesired delays
            LoadAllEmotes(avatarShapeComponent.BodyShape, profile, partition);
            profile.IsDirty = false;
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void UpdateAvatarFromSceneOverride(ref SceneAvatarShapeOverride sceneOverride, in Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!sceneOverride.IsDirty)
                return;

            sceneOverride.IsDirty = false;
            SceneAvatarShapeOverride? appliedOverride = sceneOverride;
            ApplyProfileToAvatarShape(profile, in appliedOverride, ref avatarShapeComponent, ref partition);
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void UpdateMainPlayerAvatarFromSceneOverride(ref SceneAvatarShapeOverride sceneOverride, in Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!sceneOverride.IsDirty)
                return;

            sceneOverride.IsDirty = false;
            SceneAvatarShapeOverride? appliedOverride = sceneOverride;
            BodyShape previousBodyShape = avatarShapeComponent.BodyShape;
            ApplyProfileToAvatarShape(profile, in appliedOverride, ref avatarShapeComponent, ref partition);

            if (!avatarShapeComponent.BodyShape.Equals(previousBodyShape))
                LoadAllEmotes(avatarShapeComponent.BodyShape, profile, partition);
        }

        private AvatarShapeComponent CreateAvatarShape(Profile profile, in SceneAvatarShapeOverride? sceneOverride, PartitionComponent partition)
        {
            var avatarShape = new AvatarShapeComponent(profile.Name, profile.UserId, profile.Avatar.BodyShape, default(WearablePromise),
                profile.Avatar.SkinColor, profile.Avatar.HairColor, profile.Avatar.EyesColor);

            ApplyLook(profile, in sceneOverride, ref avatarShape);
            avatarShape.WearablePromise = CreateWearablePromise(profile, in sceneOverride, avatarShape.BodyShape, partition);
            return avatarShape;
        }

        private void ApplyProfileToAvatarShape(Profile profile, in SceneAvatarShapeOverride? sceneOverride, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            avatarShapeComponent.WearablePromise.ForgetLoading(World);

            avatarShapeComponent.ID = profile.UserId;
            avatarShapeComponent.Name = profile.Name;
            ApplyLook(profile, in sceneOverride, ref avatarShapeComponent);
            avatarShapeComponent.WearablePromise = CreateWearablePromise(profile, in sceneOverride, avatarShapeComponent.BodyShape, partition);
            avatarShapeComponent.IsDirty = true;
        }

        private static void ApplyLook(Profile profile, in SceneAvatarShapeOverride? sceneOverride, ref AvatarShapeComponent avatarShapeComponent)
        {
            bool showOnlyWearables = sceneOverride?.ShowOnlyWearables ?? false;

            avatarShapeComponent.BodyShape = sceneOverride?.BodyShape ?? profile.Avatar.BodyShape;
            avatarShapeComponent.ShowOnlyWearables = showOnlyWearables;
            avatarShapeComponent.SkinColor = HideWhenShowingOnlyWearables(sceneOverride?.SkinColor ?? profile.Avatar.SkinColor, showOnlyWearables);
            avatarShapeComponent.HairColor = HideWhenShowingOnlyWearables(sceneOverride?.HairColor ?? profile.Avatar.HairColor, showOnlyWearables);
            avatarShapeComponent.EyesColor = HideWhenShowingOnlyWearables(sceneOverride?.EyesColor ?? profile.Avatar.EyesColor, showOnlyWearables);
        }

        private static Color HideWhenShowingOnlyWearables(Color color, bool showOnlyWearables) =>
            showOnlyWearables ? new Color(color.r, color.g, color.b, 0f) : color;

        private WearablePromise CreateWearablePromise(PBAvatarShape pbAvatarShape, PartitionComponent partition) =>
            WearablePromise.Create(World,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(pbAvatarShape, pbAvatarShape.Wearables, Array.Empty<string>()),
                partition);

        [Query]
        [All(typeof(ProfileWearableDTOsRequest))]
        private void ConsumeProfileWearableDTOs(Entity entity, ref WearablePromise promise)
        {
            if (!promise.SafeTryConsume(World, GetReportData(), out StreamableLoadingResult<WearablesResolution> result)) return;

            if (result.Succeeded)
                result.Asset.Dispose();

            promise.LoadingIntention.Dispose();
            World.Destroy(entity);
        }

        private WearablePromise CreateWearablePromise(Profile profile, in SceneAvatarShapeOverride? sceneOverride, BodyShape bodyShape, PartitionComponent partition)
        {
            List<URN>? sceneWearables = sceneOverride?.Wearables;

            // profile.Avatar.Wearables should be shortened, but since GetWearablesByPointers already retrieves shortened-urns,
            // there is not need to convert
            if (sceneWearables is not { Count: > 0 })
                return WearablePromise.Create(World,
                    WearableComponentsUtils.CreateGetWearablesByPointersIntention(bodyShape, profile.Avatar.Wearables, profile.Avatar.ForceRender), partition);

            // Keeps the DTOs of the replaced profile wearables loaded
            World.Create(new ProfileWearableDTOsRequest(), WearablePromise.Create(World,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(profile.Avatar.BodyShape, profile.Avatar.Wearables, profile.Avatar.ForceRender, dtoOnly: true), partition));

            return WearablePromise.Create(World, WearableComponentsUtils.CreateGetWearablesByPointersIntention(bodyShape, sceneWearables, Array.Empty<string>()), partition);
        }

        private SceneAvatarShapeOverride? ConsumeSceneAvatarShapeOverride(Entity entity)
        {
            ref SceneAvatarShapeOverride sceneOverride = ref World.TryGetRef<SceneAvatarShapeOverride>(entity, out bool exists);

            if (!exists) return null;

            sceneOverride.IsDirty = false;
            return sceneOverride;
        }

        private void LoadAllEmotes(BodyShape bodyShape, Profile profile, PartitionComponent partition)
        {
            World.Create(EmotePromise.Create(World, EmoteComponentsUtils.CreateGetEmotesByPointersIntention(bodyShape, profile.Avatar.Emotes), partition));
        }

        private struct ProfileWearableDTOsRequest { }
    }
}
