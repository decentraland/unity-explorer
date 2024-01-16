using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
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

        public CharacterPreviewController(World world, CharacterPreviewContainer container)
        {
            globalWorld = world;

            // See the logic of AvatarInstantiatorSystem
            // We should provide the following components:
            // TransformComponent
            // AvatarShapeComponent (isDirty = true)

            // Create and store AvatarShapeComponent straight-away but don't mark it as dirty so it is not grabbed by the system
            // TODO add meaningful ID and Name


            characterPreviewEntity = world.Create(
                new TransformComponent(container.parent),
                new AvatarShapeComponent("CharacterPreview", "CharacterPreview"));
        }

        public void UpdateAvatar(CharacterPreviewModel model)
        {
            if (globalWorld == null)
            {
                return;
            }
            ref AvatarShapeComponent avatarShape = ref globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity);

            avatarShape.SkinColor = model.SkinColor;
            avatarShape.HairColor = model.HairColor;
            avatarShape.BodyShape = model.BodyShape;

            if (!avatarShape.WearablePromise.IsConsumed)
                avatarShape.WearablePromise.ForgetLoading(globalWorld);

            // Create a promise for wearables to ensure the corresponding Asset Bundles are loaded
            avatarShape.WearablePromise = AssetPromise<IWearable[], GetWearablesByPointersIntention>.Create(
                globalWorld,
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(avatarShape.BodyShape, model.Wearables),
                PartitionComponent.TOP_PRIORITY
            );

            // Mark it as dirty
            avatarShape.IsDirty = true;
        }

        public void Dispose()
        {
            // Add DeleteEntityIntention to release resources of the instantiated avatar
            globalWorld.Add(characterPreviewEntity, new DeleteEntityIntention());
        }
    }
}
