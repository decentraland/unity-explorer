using DG.Tweening;
using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.EventSystems;
using Debug = UnityEngine.Debug;

namespace DCL.CharacterPreview
{
    public readonly struct CharacterPreviewCameraController : IDisposable
    {
        private const float FULL_SCREEN_DRAG_DEGREES = 360f;     // Degrees rotated for a full screen drag
        private const float MIN_ROTATION_TWEEN_DURATION = 0.01f; // Seconds for smallest rotation input

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
                    characterPreviewAvatarContainer.SetCamera(cameraSettings.cameraPositions[i]);
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

            characterPreviewAvatarContainer.TweenFovTo(newFieldOfView, cameraSettings.fieldOfViewInertiaDuration, cameraSettings.fieldOfViewInertiaCurve);
        }

        private void OnDrag(PointerEventData pointerEventData)
        {
            if (pointerEventData.button == PointerEventData.InputButton.Middle) return;

            switch (pointerEventData.button)
            {
                case PointerEventData.InputButton.Right:
                {
                    if (!cameraSettings.dragEnabled) return;

                    characterPreviewAvatarContainer.StopCameraTween();

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

                    // Normalize delta so it is resolution-independent
                    float normalizedDelta = pointerEventData.delta.x / Screen.width;
                    float rotationDelta = normalizedDelta * FULL_SCREEN_DRAG_DEGREES * cameraSettings.rotationSpeed;

                    float currentValue = characterPreviewAvatarContainer.freeLookCamera.m_XAxis.Value;
                    float targetValue = currentValue + rotationDelta;

                    float minDuration = MIN_ROTATION_TWEEN_DURATION;
                    float maxDuration = cameraSettings.rotationInertiaDuration;

                    float speedFactor = Mathf.Clamp01(Mathf.Abs(normalizedDelta));
                    float duration = Mathf.Lerp(minDuration, maxDuration, speedFactor);

                    characterPreviewAvatarContainer.TweenXAxisTo(targetValue, duration, cameraSettings.rotationInertiaCurve);

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
