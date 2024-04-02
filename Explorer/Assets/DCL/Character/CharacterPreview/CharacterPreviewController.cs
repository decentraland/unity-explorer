using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Emotes.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using System;
using System.Collections.Generic;
using EmotePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.Components.EmotesResolution,
    DCL.AvatarRendering.Emotes.Components.GetEmotesByPointersIntention>;

namespace DCL.CharacterPreview
{
    public readonly struct CharacterPreviewController : IDisposable
    {
        private readonly CharacterPreviewCameraController cameraController;
        private readonly CharacterPreviewAvatarContainer characterPreviewAvatarContainer;
        private readonly IComponentPool<CharacterPreviewAvatarContainer> characterPreviewContainerPool;
        private readonly Entity characterPreviewEntity;
        private readonly World globalWorld;

        public CharacterPreviewController(World world, CharacterPreviewAvatarContainer avatarContainer,
            CharacterPreviewInputEventBus inputEventBus, IComponentPool<CharacterPreviewAvatarContainer> characterPreviewContainerPool,
            CharacterPreviewCameraSettings cameraSettings)
        {
            globalWorld = world;
            characterPreviewAvatarContainer = avatarContainer;
            cameraController = new CharacterPreviewCameraController(inputEventBus, characterPreviewAvatarContainer, cameraSettings);
            this.characterPreviewContainerPool = characterPreviewContainerPool;

            characterPreviewEntity = world.Create(
                new CharacterTransform(avatarContainer.avatarParent),
                new AvatarShapeComponent("CharacterPreview", "CharacterPreview"),
                new CharacterEmoteComponent());
        }

        public void Dispose()
        {
            // World can be already destroyed but for some reason `IsAlive` returns true
            if (globalWorld.Capacity > 0)
            {
                ref AvatarShapeComponent avatarShape = ref globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity);
                if (!avatarShape.WearablePromise.IsConsumed) avatarShape.WearablePromise.ForgetLoading(globalWorld);
                globalWorld.Add(characterPreviewEntity, new DeleteEntityIntention());
            }

            characterPreviewContainerPool.Release(characterPreviewAvatarContainer);
            cameraController.Dispose();
        }

        public void UpdateAvatar(CharacterPreviewAvatarModel avatarModel)
        {
            if (avatarModel.Wearables == null || avatarModel.Wearables.Count <= 0) return;

            ref AvatarShapeComponent avatarShape = ref globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity);

            avatarShape.SkinColor = avatarModel.SkinColor;
            avatarShape.HairColor = avatarModel.HairColor;
            avatarShape.EyesColor = avatarModel.EyesColor;
            avatarShape.BodyShape = BodyShape.FromStringSafe(avatarModel.BodyShape);

            avatarShape.WearablePromise.ForgetLoading(globalWorld);

            avatarShape.WearablePromise = AssetPromise<WearablesResolution, GetWearablesByPointersIntention>.Create(
                globalWorld,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(avatarShape.BodyShape, avatarModel.Wearables, avatarModel.ForceRenderCategories),
                PartitionComponent.TOP_PRIORITY
            );

            avatarShape.EmotePromise = EmotePromise.Create(globalWorld,
                new GetEmotesByPointersIntention((IReadOnlyCollection<URN>?)avatarModel.Emotes ?? Array.Empty<URN>(), avatarShape.BodyShape),
                PartitionComponent.TOP_PRIORITY);

            avatarShape.IsDirty = true;
        }

        public void PlayEmote(string emoteId)
        {
            globalWorld.Add(characterPreviewEntity, new CharacterEmoteIntent { EmoteId = emoteId });
        }
    }
}
