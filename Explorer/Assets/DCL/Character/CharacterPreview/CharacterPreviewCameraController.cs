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

        // public void ResetZoom()
        // {
        //     // Find the default camera position (usually the 'Body' category) and apply its FOV.
        //     // This assumes the first entry is the default, which is a safe assumption for a full-body view.
        //     if (cameraSettings.cameraPositions.Length > 0)
        //     {
        //         characterPreviewAvatarContainer.TargetFOV = cameraSettings.cameraPositions[0].cameraFieldOfView;
        //
        //         // Also reset the vertical panning to the default position
        //         Vector3 position = characterPreviewAvatarContainer.cameraTarget.localPosition;
        //         position.y = cameraSettings.cameraPositions[0].verticalPosition.y;
        //         characterPreviewAvatarContainer.cameraTarget.localPosition = position;
        //     }
        // }

        public void ResetZoom()
        {
            if (cameraSettings.cameraPositions.Length == 0) return;

            var cfg = cameraSettings.cameraPositions[0];

            // Kill any rotational carry-over; optional but avoids “slip”.
            characterPreviewAvatarContainer.AngularVelocity = 0f;
            characterPreviewAvatarContainer.RotationInertia = 0f;

            // Force both the target and the current lens FOV
            characterPreviewAvatarContainer.TargetFOV = cfg.cameraFieldOfView;
            var lens = characterPreviewAvatarContainer.freeLookCamera.m_Lens;
            lens.FieldOfView = cfg.cameraFieldOfView;
            characterPreviewAvatarContainer.freeLookCamera.m_Lens = lens;

            // Recenter vertical pan to default
            var pos = characterPreviewAvatarContainer.cameraTarget.localPosition;
            pos.y = cfg.verticalPosition.y;
            characterPreviewAvatarContainer.cameraTarget.localPosition = pos;
        }

        public async UniTask ResetZoomAndWaitAsync(
            CancellationToken ct = default,
            float epsilonFov = 0.05f,
            float epsilonY = 0.001f)
        {
            if (cameraSettings.cameraPositions.Length == 0) return;

            var cfg = cameraSettings.cameraPositions[0];

            // Request reset (do NOT force lens immediately — let the smoother do its thing)
            characterPreviewAvatarContainer.TargetFOV = cfg.cameraFieldOfView;

            // Snap the vertical pan (match your current UX; change to easing if you prefer)
            var pos = characterPreviewAvatarContainer.cameraTarget.localPosition;
            pos.y = cfg.verticalPosition.y;
            characterPreviewAvatarContainer.cameraTarget.localPosition = pos;

            var cam = characterPreviewAvatarContainer;
            // Wait until lens FOV matches target within tolerance and Y is where we set it
            await UniTask.WaitUntil(() =>
            {
                float fovNow = cam.freeLookCamera.m_Lens.FieldOfView;
                float yNow = cam.cameraTarget.localPosition.y;
                return Mathf.Abs(fovNow - cfg.cameraFieldOfView) <= epsilonFov
                       && Mathf.Abs(yNow - cfg.verticalPosition.y) <= epsilonY;
            }, cancellationToken: ct);
        }
    }
}
