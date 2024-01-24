using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.Pools;
using DCL.Profiles;
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
        private readonly CharacterPreviewContainer characterPreviewContainer;
        private readonly CharacterPreviewCameraController cameraController;
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly Entity playerEntity;

        public CharacterPreviewController(World world, CharacterPreviewContainer container, CharacterPreviewInputEventBus inputEventBus, IComponentPoolsRegistry poolsRegistry, Entity playerEntity)
        {
            globalWorld = world;
            characterPreviewContainer = container;
            cameraController = new CharacterPreviewCameraController(inputEventBus, characterPreviewContainer);
            this.poolsRegistry = poolsRegistry;
            this.playerEntity = playerEntity;

            // See the logic of AvatarInstantiatorSystem
            // We should provide the following components:
            // TransformComponent
            // AvatarShapeComponent (isDirty = true)

            // Create and store AvatarShapeComponent straight-away but don't mark it as dirty so it is not grabbed by the system
            // TODO add meaningful ID and Name

            characterPreviewEntity = world.Create(
                new TransformComponent(container.avatarParent),
                new AvatarShapeComponent("CharacterPreview", "CharacterPreview"));
        }

        public void UpdateAvatar(CharacterPreviewModel model)
        {
            if (globalWorld == null || model.Wearables == null || model.Wearables.Count <= 0)
            {
                return;
            }
            ref AvatarShapeComponent avatarShape = ref globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity);

            avatarShape.SkinColor = model.SkinColor;
            avatarShape.HairColor = model.HairColor;
            avatarShape.BodyShape = BodyShape.FromStringSafe(model.BodyShape);

            if (!avatarShape.WearablePromise.IsConsumed)
                avatarShape.WearablePromise.ForgetLoading(globalWorld);

            avatarShape.WearablePromise = AssetPromise<IWearable[], GetWearablesByPointersIntention>.Create(
                globalWorld,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(avatarShape.BodyShape, model.Wearables, globalWorld.Get<Profile>(playerEntity).Avatar.ForceRender),
                PartitionComponent.TOP_PRIORITY
            );

            avatarShape.IsDirty = true;
        }

        public void Hide()
        {
            //ref AvatarShapeComponent avatarShape = ref globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity);
            //if (!avatarShape.WearablePromise.IsConsumed)
             //   avatarShape.WearablePromise.ForgetLoading(globalWorld);
             if(characterPreviewContainer != null) characterPreviewContainer.gameObject.SetActive(false);
        }

        public void Show()
        {
            if(characterPreviewContainer != null) characterPreviewContainer.gameObject.SetActive(true);
        }

        public void Dispose()
        {
            // Add DeleteEntityIntention to release resources of the instantiated avatar
            globalWorld.Add(characterPreviewEntity, new DeleteEntityIntention());
            poolsRegistry.GetPool(typeof(CharacterPreviewContainer)).Release(characterPreviewContainer);
            cameraController.Dispose();
        }
    }
}
