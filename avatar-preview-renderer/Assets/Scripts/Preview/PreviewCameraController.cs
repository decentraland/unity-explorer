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

        // The zoomed lens stays inside these whatever the fit and the zoom factor ask for.
        [SerializeField] private float minFieldOfView = 2f;
        [SerializeField] private float maxFieldOfView = 60f;

        // Zoom factor per unit of wheel delta; UI Toolkit reports about three units per notch.
        [SerializeField] private float wheelZoomStep = 1.03f;

        // The renderer's own wheel range around the fitted view, used when the page passes no zoom
        // option: in up to twice the fit, out to two thirds of it.
        [SerializeField] private float defaultZoomIn = 2f;
        [SerializeField] private float defaultZoomOut = 1.5f;

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
        private float _initialOrthoSize;

        // One per zoomable camera - the avatar and item views frame different subjects.
        private CameraFraming _avatarFraming;
        private CameraFraming _wearableFraming;
        private CameraFraming _builderFraming;

        private CameraFraming _active;

        private float _lastAspect;
        private PreviewMode? _lastMode;

        private ZoomRange _zoomRange;
        private Vector2 _startOffset;
        private bool _zoomLocked;
        private bool _orthographic;

#if UNITY_EDITOR
        private float _lastAvatarFitMargin;
        private float _lastWearableFitMargin;
#endif

        // The canvas is the whole screen in a WebGL build, so this is the rendering camera's aspect.
        private static float Aspect => (float)Screen.width / Mathf.Max(1, Screen.height);

        private void Awake()
        {
            _initialFOV = marketplaceAvatarCamera.Lens.FieldOfView;
            _initialOrthoSize = marketplaceAvatarCamera.Lens.OrthographicSize;
            _zoomRange = ZoomRange.FromOptions(null, null, 50f, defaultZoomIn, defaultZoomOut);

            // Same three cameras the zoom drives - authProfile and jesus are deliberately left alone
            _avatarFraming = new CameraFraming(marketplaceAvatarCamera, _initialFOV, _initialOrthoSize);
            _wearableFraming = new CameraFraming(marketplaceWearableCamera, _initialFOV, _initialOrthoSize);
            _builderFraming = new CameraFraming(builderCamera, _initialFOV, _initialOrthoSize);
            _active = _avatarFraming;

            _lastAspect = Aspect;

#if UNITY_EDITOR
            _lastAvatarFitMargin = avatarFitMargin;
            _lastWearableFitMargin = wearableFitMargin;
#endif

            // We prioritize this one because we want to have a cut to any other camera after this for the first time
            authProfileCamera.Prioritize();
        }

        /// <summary>
        /// Takes the camera options: the zoom range, its lock, the starting offset and the projection.
        /// The current zoom and pan are only clamped, never replaced, so a reload within a mode keeps
        /// the creator's framing; <see cref="RestartZoom"/> is the explicit way back to the start.
        /// </summary>
        public void Configure(PreviewConfiguration config)
        {
            _zoomRange = ZoomRange.FromOptions(config.ZoomLevel, config.WheelZoom, config.WheelStart,
                defaultZoomIn, defaultZoomOut);
            _startOffset = config.Offset;
            _zoomLocked = config.LockRadius;
            _orthographic = config.Projection == "orthographic";

            _avatarFraming.Zoom = _zoomRange.Clamp(_avatarFraming.Zoom);
            _wearableFraming.Zoom = _zoomRange.Clamp(_wearableFraming.Zoom);
            _builderFraming.Zoom = _zoomRange.Clamp(_builderFraming.Zoom);
        }

        /// <summary>
        /// Puts every view back at the start of the zoom range, after the zoom options changed.
        /// </summary>
        public void RestartZoom()
        {
            _avatarFraming.Zoom = _zoomRange.Start;
            _wearableFraming.Zoom = _zoomRange.Start;
            _builderFraming.Zoom = _zoomRange.Start;
        }

        public void SetMode(PreviewMode mode)
        {
            // Only a change of mode starts over from the fit: a reload within a mode, which the editors
            // trigger on every item update, keeps the zoom and pan the creator set.
            if (_lastMode != mode)
            {
                _lastMode = mode;

                ResetFraming(_avatarFraming);
                ResetFraming(_wearableFraming);
                ResetFraming(_builderFraming);
                ResetZoomAndPan(_avatarFraming);
                ResetZoomAndPan(_wearableFraming);
                ResetZoomAndPan(_builderFraming);

                _active = mode == PreviewMode.Builder ? _builderFraming : _avatarFraming;
            }

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

        public void ShowMarketplaceWearable(bool showWearable)
        {
            var next = showWearable ? _wearableFraming : _avatarFraming;

            // A different subject starts from the fit's framing, not the other view's zoom and pan.
            // The same view again, as on a reload, keeps them.
            if (next != _active)
            {
                _active = next;
                ResetZoomAndPan(_active);
            }

            Refit(_active);
            _active.Camera.Prioritize();
        }

        public void ZoomByWheelDelta(float delta)
        {
            if (_zoomLocked) return;

            _active.Zoom = _zoomRange.Clamp(_active.Zoom * Mathf.Pow(wheelZoomStep, -delta));
        }

        /// <summary>
        /// A zoom button press from the page, in Babylon's unit: positive pulls closer.
        /// </summary>
        public void ZoomByDelta(float delta)
        {
            if (_zoomLocked) return;

            _active.Zoom = _zoomRange.StepByZoomDelta(_active.Zoom, delta);
        }

        /// <summary>
        /// Moves the orbit by <paramref name="radiusDelta"/> metres, Babylon's radius: positive pulls back.
        /// </summary>
        public void OrbitBy(float radiusDelta)
        {
            if (_zoomLocked) return;

            _active.Zoom = _zoomRange.StepByRadius(_active.Zoom, radiusDelta);
        }

        /// <summary>
        /// Puts the view at an absolute offset, across and up the screen in metres, replacing any
        /// right-drag pan. Unclamped: the page asked for exactly this.
        /// </summary>
        public void SetOffset(Vector2 offset)
        {
            _active.UserPan = offset;
            ApplyPan(_active);
        }

        /// <summary>
        /// Pans the camera by a drag, as a fraction of the panel height (see
        /// <see cref="PreviewUIPresenter"/>). Unsmoothed, so the subject stays glued to the cursor.
        /// <paramref name="deltaTime"/> is unused: the offset tracks distance dragged, not time.
        /// </summary>
        public void Pan(Vector2 normalizedDelta, float deltaTime)
        {
            // World height at the subject makes the drag 1:1, and inverts because moving the camera left
            // slides the subject right. The LIVE lens, not the target - mid-zoom they differ, and 1:1 has
            // to match what is on screen.
            var lens = _active.Camera.Lens;
            var worldHeightAtSubject = _orthographic
                ? 2f * lens.OrthographicSize
                : 2f * panSubjectDistance * Mathf.Tan(lens.FieldOfView * 0.5f * Mathf.Deg2Rad);

            _active.UserPan += new Vector2(-normalizedDelta.x, normalizedDelta.y) * worldHeightAtSubject;
            _active.UserPan = Vector2.ClampMagnitude(_active.UserPan, maxPanOffset);

            ApplyPan(_active);
        }

        private void Update()
        {
            RefitIfFramingInputsChanged();

            LerpLens(_avatarFraming);
            LerpLens(_wearableFraming);
            LerpLens(_builderFraming);
        }

        // Writes only the fit - the user's zoom and pan sit on top of it, so they survive a refit.
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
            var margin = MarginFor(framing);

            framing.FittedFOV = Mathf.Max(forHeight, forWidth) * margin;

            // An orthographic frame is half its height tall, and the width is brought in through the
            // aspect, the same choice as above.
            framing.FittedOrthoSize =
                Mathf.Max(framing.SubjectHeight * 0.5f, framing.SubjectRadius / Aspect) * margin;

            // Zooming tightens around the view axis, so the subject has to be brought onto it too.
            var inCameraSpace = Quaternion.Inverse(rotation) * toSubject;

            framing.FitOffset = Vector2.ClampMagnitude(
                new Vector2(inCameraSpace.x, inCameraSpace.y), maxPanOffset);

            ApplyPan(framing);
        }

        private void ResetFraming(CameraFraming framing)
        {
            framing.HasSubject = false;
            framing.FittedFOV = _initialFOV;
            framing.FittedOrthoSize = _initialOrthoSize;
            framing.FitOffset = Vector2.zero;

            framing.Camera.Lens.FieldOfView = TargetFieldOfView(framing);
            framing.Camera.Lens.OrthographicSize = TargetOrthoSize(framing);

            ApplyPan(framing);
        }

        private void ResetZoomAndPan(CameraFraming framing)
        {
            framing.Zoom = _zoomRange.Start;
            framing.UserPan = _startOffset;

            ApplyPan(framing);
        }

        private float MarginFor(CameraFraming framing) =>
            framing == _wearableFraming ? wearableFitMargin : avatarFitMargin;

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

        private void LerpLens(CameraFraming framing)
        {
            var t = Time.deltaTime * lerpSpeed;
            var lens = framing.Camera.Lens;

            framing.Camera.Lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, TargetFieldOfView(framing), t);
            framing.Camera.Lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, TargetOrthoSize(framing), t);
        }

        private float TargetFieldOfView(CameraFraming framing) =>
            Mathf.Clamp(framing.FittedFOV / framing.Zoom, minFieldOfView, maxFieldOfView);

        private static float TargetOrthoSize(CameraFraming framing) => framing.FittedOrthoSize / framing.Zoom;

        private static void ApplyPan(CameraFraming framing)
        {
            var camTransform = framing.Camera.transform;
            var offset = framing.FitOffset + framing.UserPan;

            // localRotation keeps the offset screen-aligned however the camera is oriented.
            camTransform.localPosition = framing.InitialLocalPosition +
                                         camTransform.localRotation * new Vector3(offset.x, offset.y, 0f);
        }

        // The fitted subject is kept so the fit can re-run on an aspect change without the caller
        // handing the bounds back in. The fit and the user's framing are separate, so one survives
        // a change of the other.
        private class CameraFraming
        {
            public readonly CinemachineCamera Camera;
            public readonly Vector3 InitialLocalPosition;

            public float FittedFOV;
            public float FittedOrthoSize;
            public Vector2 FitOffset;

            public float Zoom = 1f;
            public Vector2 UserPan;

            public bool HasSubject;
            public Vector3 SubjectCenter;
            public float SubjectRadius;
            public float SubjectHeight;

            public CameraFraming(CinemachineCamera camera, float initialFOV, float initialOrthoSize)
            {
                Camera = camera;
                InitialLocalPosition = camera.transform.localPosition;
                FittedFOV = initialFOV;
                FittedOrthoSize = initialOrthoSize;
            }
        }
    }
}
