using System;
using Unity.Cinemachine;
using UnityEngine;
using Utils;

namespace Preview
{
    public class PreviewCameraController : MonoBehaviour
    {
        // Floor for the fit's view-axis depth, so a subject sitting on the lens cannot divide by zero.
        private const float MIN_FRUSTUM_DEPTH = 0.01f;

        [SerializeField] private float minFOV = 10f;
        [SerializeField] private float maxFOV = 30f;
        [SerializeField] private float wheelZoomSensitivity = 0.5f;

        // Converts a drag into world units so the subject tracks the cursor exactly.
        [SerializeField] private float panSubjectDistance = 7f;
        [SerializeField] private float maxPanOffset = 2f;

        [SerializeField] private float lerpSpeed = 1f;

        // Multiplies the fitted FOV: 1 puts the subject's edges on the frame edges, above 1 pulls back.
        [SerializeField, Range(0.5f, 1.5f)] private float avatarFitMargin = 1.05f;
        [SerializeField, Range(0.5f, 1.5f)] private float wearableFitMargin = 1.05f;

        [SerializeField] private CinemachineCamera authProfileCamera;
        [SerializeField] private CinemachineCamera marketplaceWearableCamera;
        [SerializeField] private CinemachineCamera marketplaceAvatarCamera;
        [SerializeField] private CinemachineCamera builderCamera;
        [SerializeField] private CinemachineCamera jesusCamera;

        private float _initialFOV;

        // One per zoomable camera - the avatar and item views frame different subjects.
        private CameraFraming _avatarFraming;
        private CameraFraming _wearableFraming;
        private CameraFraming _builderFraming;

        private CameraFraming _active;

        private float _lastAspect;

#if UNITY_EDITOR
        private float _lastAvatarFitMargin;
        private float _lastWearableFitMargin;
#endif

        // The canvas is the whole screen in a WebGL build, so this is the rendering camera's aspect.
        private static float Aspect => (float)Screen.width / Mathf.Max(1, Screen.height);

        private void Awake()
        {
            _initialFOV = marketplaceAvatarCamera.Lens.FieldOfView;

            // Same three cameras the FOV zoom drives - authProfile and jesus are deliberately left alone
            _avatarFraming = new CameraFraming(marketplaceAvatarCamera, _initialFOV);
            _wearableFraming = new CameraFraming(marketplaceWearableCamera, _initialFOV);
            _builderFraming = new CameraFraming(builderCamera, _initialFOV);
            _active = _avatarFraming;

            _lastAspect = Aspect;

#if UNITY_EDITOR
            _lastAvatarFitMargin = avatarFitMargin;
            _lastWearableFitMargin = wearableFitMargin;
#endif

            // We prioritize this one because we want to have a cut to any other camera after this for the first time
            authProfileCamera.Prioritize();
        }

        public void SetMode(PreviewMode mode)
        {
            // Reset zoom, pan and any fit when switching modes
            ResetFraming(_avatarFraming);
            ResetFraming(_wearableFraming);
            ResetFraming(_builderFraming);

            _active = mode == PreviewMode.Builder ? _builderFraming : _avatarFraming;

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

        /// <summary>
        /// Frames the avatar view on everything the avatar is currently wearing.
        /// </summary>
        public void FitAvatarView(Transform subject) => Fit(_avatarFraming, subject);

        /// <summary>
        /// Frames the item-alone view on the single item it shows.
        /// </summary>
        public void FitWearableView(Transform subject) => Fit(_wearableFraming, subject);

        // Writes only zoom and pan - the two things the wheel and right-drag write - so the user can
        // leave the framing the same way they could have reached it.
        private void Fit(CameraFraming framing, Transform subject)
        {
            framing.HasSubject = GameObjectUtils.TryMeasureYawInvariant(subject, out var center,
                out var radius, out var height);

            framing.SubjectCenter = center;
            framing.SubjectRadius = radius;
            framing.SubjectHeight = height;

            Refit(framing);
        }

        private void Refit(CameraFraming framing)
        {
            if (!framing.HasSubject)
            {
                ResetFraming(framing);
                return;
            }

            var camTransform = framing.Camera.transform;
            var parent = camTransform.parent;

            // From the unpanned position, so a refit never compounds on the last one's offset.
            var unpannedPosition = parent != null
                ? parent.TransformPoint(framing.InitialLocalPosition)
                : framing.InitialLocalPosition;

            var rotation = camTransform.rotation;
            var toSubject = framing.SubjectCenter - unpannedPosition;

            // Depth along the view axis, not straight-line distance: the cameras look slightly down and
            // the frustum grows with depth in front of the lens.
            var distance = Mathf.Max(Vector3.Dot(toSubject, rotation * Vector3.forward), MIN_FRUSTUM_DEPTH);

            // The larger of "contains the height" and "contains the width" is the only one containing both.
            var forHeight = 2f * Mathf.Atan(framing.SubjectHeight * 0.5f / distance) * Mathf.Rad2Deg;
            var forWidth = 2f * Mathf.Atan(framing.SubjectRadius / distance / Aspect) * Mathf.Rad2Deg;

            var fittedFOV = Mathf.Max(forHeight, forWidth);
            framing.TargetFOV = Mathf.Clamp(fittedFOV * MarginFor(framing), minFOV, maxFOV);

            // Zooming tightens around the view axis, so the subject has to be brought onto it too.
            var inCameraSpace = Quaternion.Inverse(rotation) * toSubject;

            framing.PanOffset = Vector2.ClampMagnitude(
                new Vector2(inCameraSpace.x, inCameraSpace.y), maxPanOffset);

            ApplyPan(framing);
        }

        private void ResetFraming(CameraFraming framing)
        {
            framing.HasSubject = false;
            framing.TargetFOV = _initialFOV;
            framing.Camera.Lens.FieldOfView = _initialFOV;
            framing.PanOffset = Vector2.zero;

            ApplyPan(framing);
        }

        private float MarginFor(CameraFraming framing) =>
            framing == _wearableFraming ? wearableFitMargin : avatarFitMargin;

        private void Update()
        {
            RefitIfFramingInputsChanged();

            LerpToTargetFOV(_avatarFraming);
            LerpToTargetFOV(_wearableFraming);
            LerpToTargetFOV(_builderFraming);
        }

        // Re-runs the fit when the viewport aspect moves underneath it, which the Shop does on resize.
        // Margins are watched in the Editor only - Inspector dials cannot move in a player build.
        private void RefitIfFramingInputsChanged()
        {
            var aspect = Aspect;
            var changed = !Mathf.Approximately(aspect, _lastAspect);
            _lastAspect = aspect;

#if UNITY_EDITOR
            if (!Mathf.Approximately(avatarFitMargin, _lastAvatarFitMargin)
                || !Mathf.Approximately(wearableFitMargin, _lastWearableFitMargin))
            {
                _lastAvatarFitMargin = avatarFitMargin;
                _lastWearableFitMargin = wearableFitMargin;
                changed = true;
            }
#endif

            if (!changed) return;

            // Fitted views only - resetting the others would throw away a hand-set zoom.
            if (_avatarFraming.HasSubject) Refit(_avatarFraming);
            if (_wearableFraming.HasSubject) Refit(_wearableFraming);
        }

        private void LerpToTargetFOV(CameraFraming framing)
        {
            framing.Camera.Lens.FieldOfView = Mathf.Lerp(framing.Camera.Lens.FieldOfView,
                framing.TargetFOV, Time.deltaTime * lerpSpeed);
        }

        private static void ApplyPan(CameraFraming framing)
        {
            var camTransform = framing.Camera.transform;

            // localRotation keeps the offset screen-aligned however the camera is oriented.
            camTransform.localPosition = framing.InitialLocalPosition +
                                         camTransform.localRotation *
                                         new Vector3(framing.PanOffset.x, framing.PanOffset.y, 0f);
        }

        public void ShowMarketplaceWearable(bool showWearable)
        {
            _active = showWearable ? _wearableFraming : _avatarFraming;

            // A different subject starts from the fit's framing, not last time's zoom and pan.
            Refit(_active);

            _active.Camera.Prioritize();
        }

        public void ZoomByWheelDelta(float delta)
        {
            _active.TargetFOV = Mathf.Clamp(_active.TargetFOV + delta * wheelZoomSensitivity,
                minFOV, maxFOV);
        }

        /// <summary>
        /// Pans the camera by a drag, as a fraction of the panel height (see
        /// <see cref="PreviewUIPresenter"/>). Unsmoothed, so the subject stays glued to the cursor.
        /// <paramref name="deltaTime"/> is unused: the offset tracks distance dragged, not time.
        /// </summary>
        public void Pan(Vector2 normalizedDelta, float deltaTime)
        {
            // World height at the subject makes the drag 1:1, and inverts because moving the camera left
            // slides the subject right. The LIVE FOV, not TargetFOV - mid-zoom they differ, and 1:1 has
            // to match what is on screen. Perspective only.
            var fov = _active.Camera.Lens.FieldOfView;
            var worldHeightAtSubject = 2f * panSubjectDistance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            _active.PanOffset += new Vector2(-normalizedDelta.x, normalizedDelta.y) * worldHeightAtSubject;
            _active.PanOffset = Vector2.ClampMagnitude(_active.PanOffset, maxPanOffset);

            ApplyPan(_active);
        }

        // The fitted subject is kept so the fit can re-run on an aspect change without the caller
        // handing the bounds back in.
        private class CameraFraming
        {
            public readonly CinemachineCamera Camera;
            public readonly Vector3 InitialLocalPosition;

            public float TargetFOV;
            public Vector2 PanOffset;

            public bool HasSubject;
            public Vector3 SubjectCenter;
            public float SubjectRadius;
            public float SubjectHeight;

            public CameraFraming(CinemachineCamera camera, float initialFOV)
            {
                Camera = camera;
                InitialLocalPosition = camera.transform.localPosition;
                TargetFOV = initialFOV;
            }
        }
    }
}
