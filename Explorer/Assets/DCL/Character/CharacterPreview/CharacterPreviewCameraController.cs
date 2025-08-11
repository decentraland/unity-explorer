using DG.Tweening;
using System;
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

            characterPreviewAvatarContainer.TweenFovTo(newFieldOfView, cameraSettings.fieldOfViewEaseDurationSeconds, cameraSettings.fieldOfViewEase);
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

                    float rotationDelta = pointerEventData.delta.x * cameraSettings.degreesPerPixel;

                    // TODO remove after tests
                    Debug.Log($"rotationDelta: {rotationDelta}");

                    float currentValue = characterPreviewAvatarContainer.freeLookCamera.m_XAxis.Value;
                    float targetValue = currentValue + rotationDelta;

                    float minDuration = 0.01f;
                    float maxDuration = cameraSettings.rotationEaseMaxDurationSeconds;
                    // Max rotation delta capped with this formula so that over a certain speed it just slows down in maxDuration seconds
                    float maxDeltaForDuration = Screen.width / 30 * cameraSettings.degreesPerPixel;

                    float t = Mathf.Clamp01(Mathf.Abs(rotationDelta) / maxDeltaForDuration);
                    float duration = Mathf.Lerp(minDuration, maxDuration, t);

                    characterPreviewAvatarContainer.TweenXAxisTo(targetValue, duration, cameraSettings.rotationEase);

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
