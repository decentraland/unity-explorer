using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.CharacterPreview
{
    public readonly struct CharacterPreviewCameraController : IDisposable
    {
        private readonly CharacterPreviewInputEventBus characterPreviewInputEventBus;
        private readonly CharacterPreviewContainer characterPreviewContainer;

        public CharacterPreviewCameraController(CharacterPreviewInputEventBus characterPreviewInputEventBus, CharacterPreviewContainer characterPreviewContainer)
        {
            this.characterPreviewInputEventBus = characterPreviewInputEventBus;
            this.characterPreviewContainer = characterPreviewContainer;

            characterPreviewInputEventBus.OnDraggingEvent += OnDrag;
            characterPreviewInputEventBus.OnScrollEvent += OnScroll;
            characterPreviewInputEventBus.OnChangePreviewFocusEvent += OnChangePreviewCategory;
        }

        private void OnChangePreviewCategory(AvatarSlotCategoryEnum categoryEnum)
        {
            int positions = characterPreviewContainer.cameraPositions.Length;

            for (var i = 0; i < positions; i++)
            {
                if (characterPreviewContainer.cameraPositions[i].slotCategoryEnum == categoryEnum)
                {
                    characterPreviewContainer.SetCameraPosition(characterPreviewContainer.cameraPositions[i]);
                    break;
                }
            }
        }


        private void OnScroll(PointerEventData pointerEventData)
        {


            var newFieldOfView = characterPreviewContainer.freeLookCamera.m_Lens.FieldOfView;

            newFieldOfView -= pointerEventData.scrollDelta.y * Time.deltaTime * characterPreviewContainer.scrollModifier;
            //if (position.z < characterPreviewContainer.depthLimits.y) position.z = characterPreviewContainer.depthLimits.y;
            //else if (position.z > characterPreviewContainer.depthLimits.x) position.z = characterPreviewContainer.depthLimits.x;

            characterPreviewContainer.freeLookCamera.m_Lens.FieldOfView = newFieldOfView;
        }

        private void OnDrag(PointerEventData pointerEventData)
        {
            switch (pointerEventData.button)
            {
                case PointerEventData.InputButton.Right:
                {
                    var position = characterPreviewContainer.cameraTarget.localPosition;
                    float dragModifier = Time.deltaTime * characterPreviewContainer.dragMovementModifier;

                    //Disabled horizontal panning for now
                    //position.x += pointerEventData.delta.x * dragModifier;
                    //if (position.x < -characterPreviewContainer.maxHorizontalOffset) position.x = -characterPreviewContainer.maxHorizontalOffset;
                    //else if (position.x > characterPreviewContainer.maxHorizontalOffset) position.x = characterPreviewContainer.maxHorizontalOffset;

                    position.y -= pointerEventData.delta.y * dragModifier;
                    if (position.y < characterPreviewContainer.minVerticalOffset) position.y = characterPreviewContainer.minVerticalOffset;
                    else if (position.y > characterPreviewContainer.maxVerticalOffset) position.y = characterPreviewContainer.maxVerticalOffset;

                    characterPreviewContainer.cameraTarget.localPosition = position;
                    break;
                }
                case PointerEventData.InputButton.Left:
                {
                    var rotation = characterPreviewContainer.rotationTarget.rotation.eulerAngles;
                    float rotationModifier = Time.deltaTime * characterPreviewContainer.rotationModifier;
                    //Disabled vertical rotation
                    //rotation.x += pointerEventData.delta.y * rotationModifier;

                    rotation.y += pointerEventData.delta.x * rotationModifier;
                    var quaternion = new Quaternion
                    {
                        eulerAngles = rotation,
                    };

                    characterPreviewContainer.rotationTarget.rotation = quaternion;
                    break;
                }
            }
        }

        public void Dispose()
        {
            characterPreviewInputEventBus.OnDraggingEvent -= OnDrag;
            characterPreviewInputEventBus.OnScrollEvent -= OnScroll;
            characterPreviewInputEventBus.OnChangePreviewFocusEvent -= OnChangePreviewCategory;
        }
    }
}
