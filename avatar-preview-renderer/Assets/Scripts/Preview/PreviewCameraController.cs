using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Preview
{
    public class PreviewCameraController : MonoBehaviour
    {
        [SerializeField] private float minFOV = 10f;
        [SerializeField] private float maxFOV = 30f;
        [SerializeField] private float wheelZoomSensitivity = 0.5f;

        // Distance from the marketplace/builder cameras to the subject they frame. Used to convert a
        // drag into world units so the subject tracks the cursor exactly; the cameras sit at a local
        // (0, 1, 7) with the subject at the preview root's origin.
        [SerializeField] private float panSubjectDistance = 7f;
        [SerializeField] private float maxPanOffset = 2f;

        [SerializeField] private float lerpSpeed = 1f;

        [SerializeField] private CinemachineCamera authProfileCamera;
        [SerializeField] private CinemachineCamera marketplaceWearableCamera;
        [SerializeField] private CinemachineCamera marketplaceAvatarCamera;
        [SerializeField] private CinemachineCamera builderCamera;
        [SerializeField] private CinemachineCamera jesusCamera;

        private float _targetFOV;
        private float _initialFOV;

        private Vector3 _avatarCameraInitialPos;
        private Vector3 _wearableCameraInitialPos;
        private Vector3 _builderCameraInitialPos;

        private Vector2 _panOffset;

        private void Awake()
        {
            _targetFOV = _initialFOV = marketplaceAvatarCamera.Lens.FieldOfView;

            // Same three cameras the FOV zoom drives - authProfile and jesus are deliberately left alone
            _avatarCameraInitialPos = marketplaceAvatarCamera.transform.localPosition;
            _wearableCameraInitialPos = marketplaceWearableCamera.transform.localPosition;
            _builderCameraInitialPos = builderCamera.transform.localPosition;

            // We prioritize this one because we want to have a cut to any other camera after this for the first time
            authProfileCamera.Prioritize();
        }

        public void SetMode(PreviewMode mode)
        {
            // Reset FOV and pan when switching modes
            marketplaceAvatarCamera.Lens.FieldOfView = marketplaceWearableCamera.Lens.FieldOfView =
                builderCamera.Lens.FieldOfView = _targetFOV = _initialFOV;

            _panOffset = Vector2.zero;
            ApplyPan();

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

        private void ApplyPan()
        {
            ApplyPan(marketplaceAvatarCamera, _avatarCameraInitialPos);
            ApplyPan(marketplaceWearableCamera, _wearableCameraInitialPos);
            ApplyPan(builderCamera, _builderCameraInitialPos);
        }

        private void ApplyPan(CinemachineCamera cam, Vector3 initialLocalPos)
        {
            var camTransform = cam.transform;

            // localRotation maps the camera's own right/up axes into parent space, so the offset stays
            // screen-aligned however the camera happens to be oriented.
            camTransform.localPosition = initialLocalPos +
                                         camTransform.localRotation *
                                         new Vector3(_panOffset.x, _panOffset.y, 0f);
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

        public void ZoomByWheelDelta(float delta)
        {
            _targetFOV = Mathf.Clamp(_targetFOV + delta * wheelZoomSensitivity, minFOV, maxFOV);
        }

        /// <summary>
        /// Pans the camera by a drag, expressed as a fraction of the panel height (see
        /// <see cref="PreviewUIPresenter"/>). Applied immediately and unsmoothed so the subject stays
        /// glued to the cursor. <paramref name="deltaTime"/> is intentionally unused: the offset
        /// tracks distance dragged, not elapsed time.
        /// </summary>
        public void Pan(Vector2 normalizedDelta, float deltaTime)
        {
            // Multiplying by the world height visible at the subject makes the drag 1:1 - drag a
            // quarter of the viewport and the subject moves a quarter of the viewport. Inverted
            // because moving the camera left is what slides the subject right.
            // NOTE: uses the FOV, so it is meaningless under projection=orthographic, where framing
            // comes from the orthographic size instead. Nothing in production sends that today.
            // The live FOV, not _targetFOV - mid-zoom they differ, and 1:1 has to match what is on screen.
            var fov = marketplaceAvatarCamera.Lens.FieldOfView;
            var worldHeightAtSubject = 2f * panSubjectDistance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            _panOffset += new Vector2(-normalizedDelta.x, normalizedDelta.y) * worldHeightAtSubject;
            _panOffset = Vector2.ClampMagnitude(_panOffset, maxPanOffset);

            ApplyPan();
        }
    }
}