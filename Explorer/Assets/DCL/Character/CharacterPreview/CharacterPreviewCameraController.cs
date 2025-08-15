﻿using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.CharacterPreview
{
    public readonly struct CharacterPreviewCameraController : IDisposable
    {
        private readonly CharacterPreviewInputEventBus characterPreviewInputEventBus;
        private readonly CharacterPreviewAvatarContainer characterPreviewAvatarContainer;
        private readonly CharacterPreviewCameraSettings cameraSettings;

        public CharacterPreviewCameraController(CharacterPreviewInputEventBus characterPreviewInputEventBus, CharacterPreviewAvatarContainer characterPreviewAvatarContainer, CharacterPreviewCameraSettings cameraSettings)
        {
            this.characterPreviewInputEventBus = characterPreviewInputEventBus;
            this.characterPreviewAvatarContainer = characterPreviewAvatarContainer;
            this.cameraSettings = cameraSettings;

            characterPreviewInputEventBus.OnDraggingEvent += OnDrag;
            characterPreviewInputEventBus.OnScrollEvent += OnScroll;
            characterPreviewInputEventBus.OnChangePreviewFocusEvent += OnChangePreviewCategory;

            OnChangePreviewCategory(AvatarWearableCategoryEnum.Body);
        }

        private void OnChangePreviewCategory(AvatarWearableCategoryEnum categoryEnum)
        {
            int positions = cameraSettings.cameraPositions.Length;

            for (var i = 0; i < positions; i++)
            {
                if (cameraSettings.cameraPositions[i].wearableCategoryEnum == categoryEnum)
                {
                    characterPreviewAvatarContainer.SetCameraPosition(cameraSettings.cameraPositions[i]);
                    break;
                }
            }
        }

        private void OnScroll(PointerEventData pointerEventData)
        {
            if (!cameraSettings.scrollEnabled) return;

            float newFieldOfView = characterPreviewAvatarContainer.freeLookCamera.m_Lens.FieldOfView;
            float originalFieldOfView = newFieldOfView;

            newFieldOfView -= pointerEventData.scrollDelta.y * Time.deltaTime * cameraSettings.scrollModifier;

            if (newFieldOfView < cameraSettings.fieldOfViewLimits.y) newFieldOfView = cameraSettings.fieldOfViewLimits.y;
            else if (newFieldOfView > cameraSettings.fieldOfViewLimits.x) newFieldOfView = cameraSettings.fieldOfViewLimits.x;

            if (newFieldOfView > originalFieldOfView && newFieldOfView > cameraSettings.fieldOfViewThresholdForReCentering)
            {
                Vector3 position = characterPreviewAvatarContainer.cameraTarget.localPosition;
                float t = Mathf.InverseLerp(cameraSettings.fieldOfViewLimits.y, cameraSettings.fieldOfViewLimits.x, newFieldOfView);
                float smoothedT = Mathf.SmoothStep(0f, 1f, t);

                float vertPos = Mathf.SmoothStep(position.y,
                    cameraSettings.cameraPositions[0].verticalPosition.y,
                    smoothedT
                );

                position.y = vertPos;

                characterPreviewAvatarContainer.cameraTarget.localPosition = position;
            }

            characterPreviewAvatarContainer.freeLookCamera.m_Lens.FieldOfView = newFieldOfView;
            characterPreviewAvatarContainer.StopCameraTween();
        }

        private void OnDrag(PointerEventData pointerEventData)
        {
            if (pointerEventData.button == PointerEventData.InputButton.Middle) return;

            characterPreviewAvatarContainer.StopCameraTween();

            switch (pointerEventData.button)
            {
                case PointerEventData.InputButton.Right:
                {
                    if (!cameraSettings.dragEnabled) return;

                    if (characterPreviewAvatarContainer.freeLookCamera.m_Lens.FieldOfView < cameraSettings.fieldOfViewThresholdForPanning)
                    {
                        Vector3 position = characterPreviewAvatarContainer.cameraTarget.localPosition;
                        float dragModifier = Time.deltaTime * cameraSettings.dragMovementModifier;

                        position.y -= pointerEventData.delta.y * dragModifier;

                        if (position.y < cameraSettings.minVerticalOffset) position.y = cameraSettings.minVerticalOffset;
                        else if (position.y > cameraSettings.maxVerticalOffset) position.y = cameraSettings.maxVerticalOffset;

                        characterPreviewAvatarContainer.cameraTarget.localPosition = position;
                    }

                    break;
                }
                case PointerEventData.InputButton.Left:
                {
                    if (!cameraSettings.rotationEnabled) return;

                    Vector3 rotation = characterPreviewAvatarContainer.rotationTarget.rotation.eulerAngles;
                    float rotationModifier = Time.deltaTime * cameraSettings.rotationModifier;

                    rotation.y -= pointerEventData.delta.x * rotationModifier;
                    var quaternion = Quaternion.Euler(rotation);

                    characterPreviewAvatarContainer.rotationTarget.rotation = quaternion;
                    break;
                }
            }
        }

        public void Dispose()
        {
            characterPreviewInputEventBus.OnDraggingEvent -= OnDrag;
            characterPreviewInputEventBus.OnScrollEvent -= OnScroll;
            characterPreviewInputEventBus.OnChangePreviewFocusEvent -= OnChangePreviewCategory;
            characterPreviewAvatarContainer.Dispose();
        }
    }
}
