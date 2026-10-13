using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Helpers;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Components.Intentions;
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
using System.Collections.Generic;
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
            UpdateAvatarFromSceneWearablesQuery(World);

            CreateAvatarShapeFromSDKComponentQuery(World);
            CreateMainPlayerAvatarShapeFromProfileQuery(World);
            CreateAvatarShapeFromProfileQuery(World);
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
            WearablePromise wearablePromise = CreateWearablePromise(profile, partition, ConsumeSceneWearablesOverride(entity));
            var avatarShape = new AvatarShapeComponent(profile.Name, profile.UserId, profile.Avatar.BodyShape, wearablePromise, profile.Avatar.SkinColor, profile.Avatar.HairColor, profile.Avatar.EyesColor);
            World.Add(entity, avatarShape, new AvatarHighlightComponent());
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(AvatarShapeComponent), typeof(PBAvatarShape))]
        private void CreateMainPlayerAvatarShapeFromProfile(in Entity entity, in Profile profile, ref PartitionComponent partition)
        {
            WearablePromise wearablePromise = CreateWearablePromise(profile, partition, ConsumeSceneWearablesOverride(entity));

            var avatarShapeComponent = new AvatarShapeComponent(profile.Name, profile.UserId, profile.Avatar.BodyShape, wearablePromise, profile.Avatar.SkinColor, profile.Avatar.HairColor, profile.Avatar.EyesColor);

            // No lazy load for main player. Get all emotes, so it can play them accordingly without undesired delays
            LoadAllEmotes(profile, partition);

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

            ApplyProfileToAvatarShape(profile, ConsumeSceneWearablesOverride(entity), ref avatarShapeComponent, ref partition);
            profile.IsDirty = false;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void UpdateMainPlayerAvatarFromProfile(in Entity entity, ref Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!profile.IsDirty)
                return;

            ApplyProfileToAvatarShape(profile, ConsumeSceneWearablesOverride(entity), ref avatarShapeComponent, ref partition);

            // No lazy load for main player. Get all emotes, so it can play them accordingly without undesired delays
            LoadAllEmotes(profile, partition);
            profile.IsDirty = false;
        }

        [Query]
        [None(typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void UpdateAvatarFromSceneWearables(ref SceneWearablesOverride sceneWearables, in Profile profile, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            if (!sceneWearables.IsDirty)
                return;

            sceneWearables.IsDirty = false;
            ApplyProfileToAvatarShape(profile, sceneWearables.Urns, ref avatarShapeComponent, ref partition);
        }

        private void ApplyProfileToAvatarShape(Profile profile, IReadOnlyList<URN>? sceneWearables, ref AvatarShapeComponent avatarShapeComponent, ref PartitionComponent partition)
        {
            avatarShapeComponent.WearablePromise.ForgetLoading(World);

            WearablePromise newPromise = CreateWearablePromise(profile, partition, sceneWearables);
            avatarShapeComponent.ID = profile.UserId;
            avatarShapeComponent.Name = profile.Name;
            avatarShapeComponent.WearablePromise = newPromise;
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

        private WearablePromise CreateWearablePromise(Profile profile, PartitionComponent partition, IReadOnlyList<URN>? sceneWearables = null)
        {
            // profile.Avatar.Wearables should be shortened, but since GetWearablesByPointers already retrieves shortened-urns,
            // there is not need to convert
            GetWearablesByPointersIntention intention = WearableComponentsUtils.CreateGetWearablesByPointersIntention(profile.Avatar.BodyShape, profile.Avatar.Wearables, profile.Avatar.ForceRender);

            if (sceneWearables != null)
                for (var i = 0; i < sceneWearables.Count; i++)
                    intention.Pointers.Add(sceneWearables[i]);

            return WearablePromise.Create(World, intention, partition);
        }

        private IReadOnlyList<URN>? ConsumeSceneWearablesOverride(Entity entity)
        {
            ref SceneWearablesOverride sceneWearables = ref World.TryGetRef<SceneWearablesOverride>(entity, out bool exists);

            if (!exists) return null;

            sceneWearables.IsDirty = false;
            return sceneWearables.Urns;
        }

        private void LoadAllEmotes(Profile profile, PartitionComponent partition)
        {
            World.Create(CreateEmotePromise(profile, partition));
        }

        private EmotePromise CreateEmotePromise(Profile profile, PartitionComponent partition) =>
            EmotePromise.Create(World, EmoteComponentsUtils.CreateGetEmotesByPointersIntention(profile.Avatar.BodyShape, profile.Avatar.Emotes), partition);

    }
}
