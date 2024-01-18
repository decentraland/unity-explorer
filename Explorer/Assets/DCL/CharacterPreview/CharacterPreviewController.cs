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
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.CharacterPreview
{
    public readonly struct CharacterPreviewController : IDisposable
    {
        private readonly World globalWorld;
        private readonly Entity characterPreviewEntity;
        private readonly CharacterPreviewContainer characterPreviewContainer;
        private readonly CharacterPreviewInputEventBus characterPreviewInputEventBus;

        public CharacterPreviewController(World world, CharacterPreviewContainer container, CharacterPreviewInputEventBus inputEventBus)
        {
            globalWorld = world;
            characterPreviewContainer = container;
            characterPreviewInputEventBus = inputEventBus;

            // See the logic of AvatarInstantiatorSystem
            // We should provide the following components:
            // TransformComponent
            // AvatarShapeComponent (isDirty = true)

            // Create and store AvatarShapeComponent straight-away but don't mark it as dirty so it is not grabbed by the system
            // TODO add meaningful ID and Name

            characterPreviewEntity = world.Create(
                new TransformComponent(container.avatarParent),
                new AvatarShapeComponent("CharacterPreview", "CharacterPreview"));

            characterPreviewInputEventBus.OnDraggingEvent += OnDrag;
            characterPreviewInputEventBus.OnScrollEvent += OnScroll;
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

        private void OnScroll(PointerEventData pointerEventData)
        {
               var transform1 = characterPreviewContainer.cameraTarget.transform;
               //this should be in a system probably?
               var position = transform1.position;
               position.z -= pointerEventData.scrollDelta.y * Time.deltaTime * 4;
               if (position.z < -7) position.z = -7;
               transform1.position = position;
        }

        public void OnDrag(PointerEventData pointerEventData)
        {
            switch (pointerEventData.button)
            {
                case PointerEventData.InputButton.Right:
                {
                    var position = characterPreviewContainer.cameraTarget.position;
                    position.x += pointerEventData.delta.x * Time.deltaTime * .2f;
                    position.y -= pointerEventData.delta.y * Time.deltaTime * .2f; //Input in Y is inverted

                    //Apply boundaries for limiting panning after this step
                    characterPreviewContainer.cameraTarget.position = position;
                    break;
                }
                case PointerEventData.InputButton.Left:
                {
                    var rotation = characterPreviewContainer.rotationTarget.rotation.eulerAngles;
                    rotation.y += pointerEventData.delta.x * Time.deltaTime * 8;
                    rotation.x += pointerEventData.delta.y * Time.deltaTime * 10;
                    var quaternion = new Quaternion
                    {
                        eulerAngles = rotation,
                    };

                    characterPreviewContainer.rotationTarget.rotation = quaternion;
                    break;
                }
            }
        }

        public void Hide()
        {

        }

        public void Dispose()
        {
            // Add DeleteEntityIntention to release resources of the instantiated avatar
            globalWorld.Add(characterPreviewEntity, new DeleteEntityIntention());
            characterPreviewInputEventBus.OnDraggingEvent -= OnDrag;
            characterPreviewInputEventBus.OnScrollEvent -= OnScroll;

        }
    }
}
