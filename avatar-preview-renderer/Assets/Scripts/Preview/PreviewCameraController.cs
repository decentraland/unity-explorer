using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Preview
{
    public class PreviewCameraController : MonoBehaviour
    {
        [SerializeField] private float minFOV = 10f;
        [SerializeField] private float maxFOV = 30f;
        [SerializeField] private float zoomStep = 5f;
        [SerializeField] private float wheelZoomSensitivity = 0.5f;
        [SerializeField] private float lerpSpeed = 1f;

        [SerializeField] private CinemachineCamera authProfileCamera;
        [SerializeField] private CinemachineCamera marketplaceWearableCamera;
        [SerializeField] private CinemachineCamera marketplaceAvatarCamera;
        [SerializeField] private CinemachineCamera builderCamera;
        [SerializeField] private CinemachineCamera jesusCamera;

        private float _targetFOV;
        private float _initialFOV;

        private void Awake()
        {
            _targetFOV = _initialFOV = marketplaceAvatarCamera.Lens.FieldOfView;
            
            // We prioritize this one because we want to have a cut to any other camera after this for the first time
            authProfileCamera.Prioritize();
        }

        public void SetMode(PreviewMode mode)
        {
            // Reset FOV when switching modes
            marketplaceAvatarCamera.Lens.FieldOfView = marketplaceWearableCamera.Lens.FieldOfView =
                builderCamera.Lens.FieldOfView = _targetFOV = _initialFOV;

            switch (mode)
            {
                // Marketplace goes to authProfile too since we want the first blend to be a cut
                case PreviewMode.Marketplace:
                case PreviewMode.Authentication:
                case PreviewMode.Profile:
                    authProfileCamera.Prioritize();
                    break;
                case PreviewMode.Jesus:
                    jesusCamera.Prioritize();
                    break;
                case PreviewMode.Builder:
                    builderCamera.Prioritize();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private void Update()
        {
            var fov = Mathf.Lerp(marketplaceAvatarCamera.Lens.FieldOfView, _targetFOV, Time.deltaTime * lerpSpeed);
            marketplaceAvatarCamera.Lens.FieldOfView = marketplaceWearableCamera.Lens.FieldOfView =
                builderCamera.Lens.FieldOfView = fov;
        }

        public void ShowMarketplaceWearable(bool showWearable)
        {
            if (showWearable)
            {
                marketplaceWearableCamera.Prioritize();
            }
            else
            {
                marketplaceAvatarCamera.Prioritize();
            }
        }

        public void ZoomIn()
        {
            _targetFOV = Mathf.Clamp(_targetFOV - zoomStep, minFOV, maxFOV);
        }

        public void ZoomOut()
        {
            _targetFOV = Mathf.Clamp(_targetFOV + zoomStep, minFOV, maxFOV);
        }

        public void ZoomByWheelDelta(float delta)
        {
            _targetFOV = Mathf.Clamp(_targetFOV + delta * wheelZoomSensitivity, minFOV, maxFOV);
        }
    }
}