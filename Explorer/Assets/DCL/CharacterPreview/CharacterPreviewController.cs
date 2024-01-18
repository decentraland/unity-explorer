using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Optimization.Pools;
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
        private readonly IComponentPoolsRegistry poolsRegistry;

        public CharacterPreviewController(World world, CharacterPreviewContainer container, CharacterPreviewInputEventBus inputEventBus, IComponentPoolsRegistry poolsRegistry)
        {
            globalWorld = world;
            characterPreviewContainer = container;
            characterPreviewInputEventBus = inputEventBus;
            this.poolsRegistry = poolsRegistry;

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
            characterPreviewInputEventBus.OnChangePreviewFocusEvent += OnChangePreviewCategory;
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

        private void OnChangePreviewCategory(AvatarSlotCategoryEnum categoryEnum)
        {
            switch (categoryEnum)
            {
                case AvatarSlotCategoryEnum.Top:
                    characterPreviewContainer.cameraTarget.position = characterPreviewContainer.topPositionTransform.position;
                    break;
                case AvatarSlotCategoryEnum.Bottom:
                    characterPreviewContainer.cameraTarget.position = characterPreviewContainer.bottomPositionTransform.position;
                    break;
                case AvatarSlotCategoryEnum.Shoes:
                    characterPreviewContainer.cameraTarget.position = characterPreviewContainer.shoesPositionTransform.position;
                    break;
                case AvatarSlotCategoryEnum.Head:
                    characterPreviewContainer.cameraTarget.position = characterPreviewContainer.headPositionTransform.position;
                    break;
                case AvatarSlotCategoryEnum.Body:
                    characterPreviewContainer.cameraTarget.position = characterPreviewContainer.defaultPositionTransform.position;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(categoryEnum), categoryEnum, null);
            }
        }


        private void OnScroll(PointerEventData pointerEventData)
        {
            //All these magic numbers will disappear :)
            var transform1 = characterPreviewContainer.cameraTarget.transform;
               var position = transform1.position;
               position.z -= pointerEventData.scrollDelta.y * Time.deltaTime * 4;
               if (position.z < -7) position.z = -7;
               transform1.position = position;
        }

        private void OnDrag(PointerEventData pointerEventData)
        {
            //All these magic numbers will disappear :)
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
            ref AvatarShapeComponent avatarShape = ref globalWorld.Get<AvatarShapeComponent>(characterPreviewEntity);
            if (!avatarShape.WearablePromise.IsConsumed)
                avatarShape.WearablePromise.ForgetLoading(globalWorld);
            characterPreviewContainer.gameObject.SetActive(false);
        }

        public void Show()
        {
            characterPreviewContainer.gameObject.SetActive(true);
        }

        public void Dispose()
        {
            // Add DeleteEntityIntention to release resources of the instantiated avatar
            globalWorld.Add(characterPreviewEntity, new DeleteEntityIntention());
            poolsRegistry.GetPool(typeof(CharacterPreviewContainer)).Release(characterPreviewContainer);
            characterPreviewInputEventBus.OnDraggingEvent -= OnDrag;
            characterPreviewInputEventBus.OnScrollEvent -= OnScroll;
            characterPreviewInputEventBus.OnChangePreviewFocusEvent -= OnChangePreviewCategory;
        }
    }
}
