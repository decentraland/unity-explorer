using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.CharacterPreview
{
    public readonly struct CharacterPreviewCameraController : IDisposable
    {
        private const float SCROLL_DAMPENING_EXP = 0.7f;
        private const float MAX_ANGULAR_VELOCITY = 900f;

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

        public void Dispose()
        {
            characterPreviewInputEventBus.OnDraggingEvent -= OnDrag;
            characterPreviewInputEventBus.OnScrollEvent -= OnScroll;
            characterPreviewInputEventBus.OnChangePreviewFocusEvent -= OnChangePreviewCategory;
            characterPreviewAvatarContainer.Dispose();
        }

        public void ResetAvatarMovement() =>
            characterPreviewAvatarContainer.ResetAvatarMovement();

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

            CalculateFOV(pointerEventData);
        }

        private void OnDrag(PointerEventData pointerEventData)
        {
            if (pointerEventData.button == PointerEventData.InputButton.Middle) return;

            switch (pointerEventData.button)
            {
                case PointerEventData.InputButton.Right:
                {
                    if (!cameraSettings.dragEnabled) return;

                    CalculateCameraTargetPosition(pointerEventData);

                    break;
                }
                case PointerEventData.InputButton.Left:
                {
                    if (!cameraSettings.rotationEnabled) return;

                    CalculateAngularVelocity(pointerEventData);

                    break;
                }
            }
        }

        private void CalculateFOV(PointerEventData pointerEventData)
        {
            float currentFieldOfView = characterPreviewAvatarContainer.freeLookCamera.m_Lens.FieldOfView;
            float originalFieldOfView = currentFieldOfView;

            float scrollDelta = pointerEventData.scrollDelta.y * cameraSettings.scrollModifier;
            float scrollMagnitude = Mathf.Abs(scrollDelta);
            float scaledScrollDelta = Mathf.Sign(scrollDelta) * Mathf.Pow(scrollMagnitude, SCROLL_DAMPENING_EXP);

            float newFieldOfView = currentFieldOfView - scaledScrollDelta;

            // Clamp to limits
            if (newFieldOfView < cameraSettings.fieldOfViewLimits.y) newFieldOfView = cameraSettings.fieldOfViewLimits.y;
            else if (newFieldOfView > cameraSettings.fieldOfViewLimits.x) newFieldOfView = cameraSettings.fieldOfViewLimits.x;

            // Handle camera position adjustment for recentering
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

            characterPreviewAvatarContainer.TargetFOV = newFieldOfView;
        }

        private void CalculateCameraTargetPosition(PointerEventData pointerEventData)
        {
            if (characterPreviewAvatarContainer.freeLookCamera.m_Lens.FieldOfView < cameraSettings.fieldOfViewThresholdForPanning)
            {
                Vector3 position = characterPreviewAvatarContainer.cameraTarget.localPosition;
                float dragModifier = Time.deltaTime * cameraSettings.dragMovementModifier;

                position.y -= pointerEventData.delta.y * dragModifier;

                if (position.y < cameraSettings.minVerticalOffset) position.y = cameraSettings.minVerticalOffset;
                else if (position.y > cameraSettings.maxVerticalOffset) position.y = cameraSettings.maxVerticalOffset;

                characterPreviewAvatarContainer.cameraTarget.localPosition = position;
            }
        }

        private void CalculateAngularVelocity(PointerEventData pointerEventData)
        {
            characterPreviewAvatarContainer.RotationModifier = cameraSettings.rotationModifier;
            characterPreviewAvatarContainer.RotationInertia = cameraSettings.rotationInertia;

            characterPreviewAvatarContainer.IsDragging = true;
            characterPreviewAvatarContainer.LastDragTime = Time.time;

            float angularVelocity = characterPreviewAvatarContainer.AngularVelocity;
            float targetVelocity = -pointerEventData.delta.x / Time.deltaTime;

            if (cameraSettings.rotationInertia <= 0f)
            {
                // No inertia, instant response
                angularVelocity = targetVelocity;
            }
            else
            {
                // Acceleration, higher inertia = slower acceleration
                float accelerationRate = (1f / cameraSettings.rotationInertia) * Time.deltaTime;
                angularVelocity = Mathf.Lerp(angularVelocity, targetVelocity, accelerationRate);
            }

            characterPreviewAvatarContainer.AngularVelocity = Mathf.Clamp(angularVelocity, -MAX_ANGULAR_VELOCITY, MAX_ANGULAR_VELOCITY);
        }
    }
}
