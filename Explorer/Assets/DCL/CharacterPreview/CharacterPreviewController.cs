using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.Unity.Transforms.Components;
using System;
using System.Collections.Generic;

namespace DCL.CharacterPreview
{
    public readonly struct CharacterPreviewController : IDisposable
    {
        private readonly CharacterPreviewCameraController cameraController;
        private readonly CharacterPreviewContainer characterPreviewContainer;
        private readonly IComponentPool<CharacterPreviewContainer> characterPreviewContainerPool;
        private readonly Entity characterPreviewEntity;
        private readonly World globalWorld;

        public CharacterPreviewController(World world, CharacterPreviewContainer container, CharacterPreviewInputEventBus inputEventBus, IComponentPool<CharacterPreviewContainer> characterPreviewContainerPool)
        {
            globalWorld = world;
            characterPreviewContainer = container;
            cameraController = new CharacterPreviewCameraController(inputEventBus, characterPreviewContainer);
            this.characterPreviewContainerPool = characterPreviewContainerPool;

            // TODO add meaningful ID and Name

            characterPreviewEntity = world.Create(
                new TransformComponent(container.avatarParent),
                new AvatarShapeComponent("CharacterPreview", "CharacterPreview"));
        }

        public void Dispose()
        {
            globalWorld.Add(characterPreviewEntity, new DeleteEntityIntention());
            characterPreviewContainerPool.Release(characterPreviewContainer);
            cameraController.Dispose();
        }

        public void UpdateAvatar(CharacterPreviewModel model)
        {
            if (globalWorld == null || model.Wearables == null || model.Wearables.Count <= 0) return;

            ref AvatarShapeComponent avatarShape = ref globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity);

            avatarShape.SkinColor = model.SkinColor;
            avatarShape.HairColor = model.HairColor;
            avatarShape.BodyShape = BodyShape.FromStringSafe(model.BodyShape);

            avatarShape.WearablePromise.ForgetLoading(globalWorld);

            avatarShape.WearablePromise = AssetPromise<WearablesResolution, GetWearablesByPointersIntention>.Create(
                globalWorld,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(avatarShape.BodyShape, model.Wearables, model.ForceRender),
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

            if (characterPreviewContainer != null) characterPreviewContainer.gameObject.SetActive(false);
        }

        public void Show()
        {
            if (characterPreviewContainer != null) characterPreviewContainer.gameObject.SetActive(true);
        }
    }
}
