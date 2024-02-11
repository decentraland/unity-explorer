using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.Components;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.Unity.Transforms.Components;
using System;

namespace DCL.CharacterPreview
{
    public readonly struct CharacterPreviewController : IDisposable
    {
        private readonly World globalWorld;
        private readonly Entity characterPreviewEntity;
        private readonly CharacterPreviewAvatarContainer characterPreviewAvatarContainer;
        private readonly CharacterPreviewCameraController cameraController;
        private readonly IComponentPool<CharacterPreviewAvatarContainer> characterPreviewContainerPool;

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
                new AvatarShapeComponent("CharacterPreview", "CharacterPreview"));
        }

        public void UpdateAvatar(CharacterPreviewAvatarModel avatarModel)
        {
            if (globalWorld == null || avatarModel.Wearables == null || avatarModel.Wearables.Count <= 0) return;

            ref AvatarShapeComponent avatarShape = ref globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity);

            avatarShape.SkinColor = avatarModel.SkinColor;
            avatarShape.HairColor = avatarModel.HairColor;
            avatarShape.BodyShape = BodyShape.FromStringSafe(avatarModel.BodyShape);

            avatarShape.WearablePromise.ForgetLoading(globalWorld);
            avatarShape.WearablePromise = AssetPromise<WearablesResolution, GetWearablesByPointersIntention>.Create(
                globalWorld,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(avatarShape.BodyShape, avatarModel.Wearables, avatarModel.ForceRenderCategories),
                PartitionComponent.TOP_PRIORITY
            );

            avatarShape.IsDirty = true;
        }

        public void Hide()
        {
            if (globalWorld != null)
            {
                ref AvatarShapeComponent avatarShape = ref globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity);
                if (!avatarShape.WearablePromise.IsConsumed) avatarShape.WearablePromise.ForgetLoading(globalWorld);
            }

            if (characterPreviewAvatarContainer != null) characterPreviewAvatarContainer.gameObject.SetActive(false);
        }

        public void Show()
        {
            if (characterPreviewAvatarContainer != null) { characterPreviewAvatarContainer.gameObject.SetActive(true); }
        }

        public void Dispose()
        {
            globalWorld.Add(characterPreviewEntity, new DeleteEntityIntention());
            characterPreviewContainerPool.Release(characterPreviewAvatarContainer);
            cameraController.Dispose();
        }
    }
}
