using DCL.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.InWorldCamera.UI
{
    public class InWorldCameraEffectsView : MonoBehaviour
    {
        [Header("Basic Color Adjustments")]
        [SerializeField, Range(-5f, 5f)] private float postExposure;
        [SerializeField, Range(-100f, 100f)] private float contrast;
        [SerializeField, Range(-100f, 100f)] private float saturation;
        [SerializeField, Range(-100f, 100f)] private float hueShift;

        [Header("Color Filter")]
        [SerializeField, Range(0, 360f)] private float filterHue;
        [SerializeField, Range(0, 100f)] private float filterSaturation;
        [SerializeField, Range(0, 100f)] private float filterValue = 100f;
        [field: SerializeField] [field: Range(0, 100f)] public float FilterIntensity { get; private set; }

        private Color filterColor = Color.white;

        [Header("Depth of Field")]
        [SerializeField] private bool enableDOF;
        [SerializeField, Range(0, 150f)] private float focusDistance = 10f;
        [SerializeField, Range(1, 300f)] private float focalLength = 50f;
        [SerializeField, Range(1, 32f)] private float aperture = 5.6f;

        [FormerlySerializedAs("enableAutofocus")]
        [Header("Autofocus")]
        [SerializeField] private bool enableAutoFocus;
        [SerializeField] private LayerMask autofocusLayers = -1;  // All layers by default
        [SerializeField, Range(1, 60f)] private float autofocusUpdateRate = 4;    // Updates per second
        [SerializeField, Range(0.1f, 20f)] private float autofocusBlendSpeed = 5f;  // How smooth the focus transition is
        [SerializeField, Range(0.1f, 100f)] private float autofocusMaxDistance = 50f;

        private DefaultCameraEffects defaults;
        private float targetFocusDistance;

        public ReactiveProperty<float> PostExposure { get; private set; }
        public ReactiveProperty<float> Contrast { get; private set; }
        public ReactiveProperty<float> Saturation { get; private set; }
        public ReactiveProperty<float> HueShift { get; private set; }
        public ReactiveProperty<Color> FilterColor { get; private set; }

        public ReactiveProperty<bool> EnabledDof { get; private set; }
        public ReactiveProperty<float> FocusDistance { get; private set; }
        public ReactiveProperty<float> FocalLength { get; private set; }
        public ReactiveProperty<float> Aperture { get; private set; }

        public ReactiveProperty<bool> EnableAutoFocus { get; private set; }

        private void Awake()
        {
            defaults = new DefaultCameraEffects
            {
                PostExposure = 0f,
                Contrast = 0f,
                Saturation = 0f,
                HueShift = 0f,
                FilterColor = Color.white,

                EnabledDof = false,
                FocusDistance = 10f,
                FocalLength = 50f,
                Aperture = 5.6f,
            };

            PostExposure = new ReactiveProperty<float>(postExposure);
            Contrast = new ReactiveProperty<float>(contrast);
            Saturation = new ReactiveProperty<float>(saturation);
            HueShift = new ReactiveProperty<float>(hueShift);
            FilterColor = new ReactiveProperty<Color>(filterColor);

            EnabledDof = new ReactiveProperty<bool>(enableDOF);
            FocusDistance = new ReactiveProperty<float>(focusDistance);
            FocalLength = new ReactiveProperty<float>(focalLength);
            Aperture = new ReactiveProperty<float>(aperture);

            EnableAutoFocus = new ReactiveProperty<bool>(false);
        }

        private void Update()
        {
            PostExposure.Value = postExposure;
            Contrast.Value = contrast;
            Saturation.Value = saturation;
            HueShift.Value = hueShift;

            filterColor = Color.HSVToRGB(filterHue / 360f, filterSaturation / 100f, filterValue / 100f); // ranges are 0-1 in RGB
            FilterColor.Value = filterColor;

            EnabledDof.Value = enableDOF;
            if (enableDOF)
            {
                EnableAutoFocus.Value = enableAutoFocus;
                FocusDistance.Value = focusDistance;
                FocalLength.Value = focalLength;
                Aperture.Value = aperture;
            }
        }

        private void OnDestroy()
        {
            PostExposure.Dispose();
            Contrast.Dispose();
            Saturation.Dispose();
            HueShift.Dispose();
            FilterColor.Dispose();

            EnabledDof.Dispose();
            FocusDistance.Dispose();
            FocalLength.Dispose();
            Aperture.Dispose();

            EnableAutoFocus.Dispose();
        }

        public void Show()
        {
            enabled = true;
        }

        public void Hide()
        {
            enabled = false;

            postExposure = defaults.PostExposure;
            contrast = defaults.Contrast;
            saturation = defaults.Saturation;
            hueShift = defaults.HueShift;
            filterColor = defaults.FilterColor;

            enableDOF = defaults.EnabledDof;

            focusDistance = defaults.FocusDistance;
            focalLength = defaults.FocalLength;
            aperture = defaults.Aperture;

            enableAutoFocus = false;
        }

        public void SetAutoFocus(float distance, float targetFocusDistance)
        {
            focusDistance = distance;
            this.targetFocusDistance = targetFocusDistance;
        }

        private void OnDrawGizmosSelected()
        {
            if (!enableAutoFocus || !enableDOF || !Camera.main) return;

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // Draw the autofocus sampling area
            Vector3 center = Vector3.forward * targetFocusDistance;
            var autofocusAreaSize = 0.1f;
            float size = autofocusAreaSize * targetFocusDistance * 2f; // Scale area with distance
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, new Vector3(size, size, 0.1f));

            // Draw focus distance
            Gizmos.DrawLine(Vector3.zero, center);

            // Draw the focus plane
            Vector3 up = Vector3.up * size;
            Vector3 right = Vector3.right * size;
            Gizmos.DrawLine(center - up, center + up);
            Gizmos.DrawLine(center - right, center + right);

            Gizmos.matrix = oldMatrix;
        }

        private struct DefaultCameraEffects
        {
            public float PostExposure;
            public float Contrast;
            public float Saturation;
            public float HueShift;
            public Color FilterColor;

            public bool EnabledDof;
            public float FocusDistance;
            public float FocalLength;
            public float Aperture;
        }
    }
}
