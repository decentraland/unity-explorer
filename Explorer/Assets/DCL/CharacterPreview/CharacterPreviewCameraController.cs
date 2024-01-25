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
            characterPreviewInputEventBus.OnPointerUpEvent += OnPointerUp;
        }

        private void OnPointerUp(PointerEventData pointerEventData)
        {
            characterPreviewContainer.SetCursor(CharacterPreviewCameraMovementType.Default);
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
            if (newFieldOfView < characterPreviewContainer.depthLimits.y) newFieldOfView = characterPreviewContainer.depthLimits.y;
            else if (newFieldOfView > characterPreviewContainer.depthLimits.x) newFieldOfView = characterPreviewContainer.depthLimits.x;

            characterPreviewContainer.freeLookCamera.m_Lens.FieldOfView = newFieldOfView;
            characterPreviewContainer.StopCameraTween();
        }

        private void OnDrag(PointerEventData pointerEventData)
        {
            characterPreviewContainer.StopCameraTween();
            switch (pointerEventData.button)
            {
                case PointerEventData.InputButton.Right:
                {
                    characterPreviewContainer.SetCursor(CharacterPreviewCameraMovementType.Pan);
                    var position = characterPreviewContainer.cameraTarget.localPosition;
                    float dragModifier = Time.deltaTime * characterPreviewContainer.dragMovementModifier;

                    position.y -= pointerEventData.delta.y * dragModifier;
                    if (position.y < characterPreviewContainer.minVerticalOffset) position.y = characterPreviewContainer.minVerticalOffset;
                    else if (position.y > characterPreviewContainer.maxVerticalOffset) position.y = characterPreviewContainer.maxVerticalOffset;

                    characterPreviewContainer.cameraTarget.localPosition = position;
                    break;
                }
                case PointerEventData.InputButton.Left:
                {
                    characterPreviewContainer.SetCursor(CharacterPreviewCameraMovementType.Rotate);
                    var rotation = characterPreviewContainer.rotationTarget.rotation.eulerAngles;
                    float rotationModifier = Time.deltaTime * characterPreviewContainer.rotationModifier;

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
            characterPreviewInputEventBus.OnPointerUpEvent -= OnPointerUp;
            characterPreviewContainer.Dispose();
        }
    }
}
