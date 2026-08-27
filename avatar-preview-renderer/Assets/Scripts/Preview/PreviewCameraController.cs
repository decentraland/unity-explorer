using System;
using Unity.Cinemachine;
using UnityEngine;
using Utils;

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

        // Multiplies the FOV the auto-fit arrives at. 1 puts the subject's edges exactly on the frame
        // edges; above 1 pulls back for breathing room, below 1 pushes in and lets it overflow a little.
        // Re-applied as soon as either value changes, so dragging these in play mode reframes live.
        [SerializeField, Range(0.5f, 1.5f)] private float avatarFitMargin = 1.05f;
        [SerializeField, Range(0.5f, 1.5f)] private float wearableFitMargin = 1.05f;

        [SerializeField] private CinemachineCamera authProfileCamera;
        [SerializeField] private CinemachineCamera marketplaceWearableCamera;
        [SerializeField] private CinemachineCamera marketplaceAvatarCamera;
        [SerializeField] private CinemachineCamera builderCamera;
        [SerializeField] private CinemachineCamera jesusCamera;

        private float _initialFOV;

        // One per zoomable camera. The avatar and item-alone views frame different subjects, so they
        // cannot share a single zoom and pan the way they used to.
        private CameraFraming _avatarFraming;
        private CameraFraming _wearableFraming;
        private CameraFraming _builderFraming;

        private CameraFraming _active;

        private float _lastAvatarFitMargin;
        private float _lastWearableFitMargin;
        private float _lastAspect;

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

            _lastAvatarFitMargin = avatarFitMargin;
            _lastWearableFitMargin = wearableFitMargin;
            _lastAspect = Aspect;

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

        /// <summary>
        /// Records what a view frames and zooms it in to suit. Only ever writes the zoom and the pan -
        /// the two things the wheel and the right-drag write - so the result is a framing the user could
        /// have reached by hand, and can leave the same way.
        /// </summary>
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

            // Measured from where the camera sits with no pan applied, so a refit never compounds on top
            // of the offset a previous one left behind.
            var unpannedPosition = parent != null
                ? parent.TransformPoint(framing.InitialLocalPosition)
                : framing.InitialLocalPosition;

            var rotation = camTransform.rotation;
            var toSubject = framing.SubjectCenter - unpannedPosition;

            // Depth along the view axis rather than straight-line distance: the cameras look slightly
            // down, and it is the depth in front of the lens that the frustum grows with.
            var distance = Mathf.Max(Vector3.Dot(toSubject, rotation * Vector3.forward), 0.01f);

            // The vertical FOV that just contains the subject's height, and the one that just contains
            // its width once the aspect has turned the horizontal half-angle into a vertical one. The
            // larger of the two is the only one that contains both.
            var forHeight = 2f * Mathf.Atan(framing.SubjectHeight * 0.5f / distance) * Mathf.Rad2Deg;
            var forWidth = 2f * Mathf.Atan(framing.SubjectRadius / distance / Aspect) * Mathf.Rad2Deg;

            var fittedFOV = Mathf.Max(forHeight, forWidth);
            framing.TargetFOV = Mathf.Clamp(fittedFOV * MarginFor(framing), minFOV, maxFOV);

            // Zooming in tightens around the view axis, so the subject also has to be brought onto it.
            // Its offset in the camera's own basis is exactly the pan that does that.
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

        /// <summary>
        /// Re-runs the fit when something it was computed from has moved underneath it: either margin
        /// slider, or the viewport aspect, which the Shop changes whenever the page is resized.
        /// </summary>
        private void RefitIfFramingInputsChanged()
        {
            var aspect = Aspect;

            if (Mathf.Approximately(avatarFitMargin, _lastAvatarFitMargin)
                && Mathf.Approximately(wearableFitMargin, _lastWearableFitMargin)
                && Mathf.Approximately(aspect, _lastAspect))
                return;

            _lastAvatarFitMargin = avatarFitMargin;
            _lastWearableFitMargin = wearableFitMargin;
            _lastAspect = aspect;

            // Only the views that were actually fitted - resetting the others would throw away a zoom
            // the user set by hand.
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

            // localRotation maps the camera's own right/up axes into parent space, so the offset stays
            // screen-aligned however the camera happens to be oriented.
            camTransform.localPosition = framing.InitialLocalPosition +
                                         camTransform.localRotation *
                                         new Vector3(framing.PanOffset.x, framing.PanOffset.y, 0f);
        }

        public void ShowMarketplaceWearable(bool showWearable)
        {
            _active = showWearable ? _wearableFraming : _avatarFraming;

            // Switching view is a fresh look at a different subject, so it starts from the framing the
            // fit chose rather than from whatever zoom and pan were left on it last time.
            Refit(_active);

            _active.Camera.Prioritize();
        }

        public void ZoomByWheelDelta(float delta)
        {
            _active.TargetFOV = Mathf.Clamp(_active.TargetFOV + delta * wheelZoomSensitivity,
                minFOV, maxFOV);
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
            // The live FOV, not TargetFOV - mid-zoom they differ, and 1:1 has to match what is on screen.
            var fov = _active.Camera.Lens.FieldOfView;
            var worldHeightAtSubject = 2f * panSubjectDistance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            _active.PanOffset += new Vector2(-normalizedDelta.x, normalizedDelta.y) * worldHeightAtSubject;
            _active.PanOffset = Vector2.ClampMagnitude(_active.PanOffset, maxPanOffset);

            ApplyPan(_active);
        }

        /// <summary>
        /// One camera's framing. The subject it was fitted to is kept so the fit can be re-run when the
        /// margin sliders or the viewport aspect move underneath it, without the caller having to hand
        /// the bounds back in. A wheel zoom overwrites <see cref="TargetFOV"/> directly.
        /// </summary>
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
