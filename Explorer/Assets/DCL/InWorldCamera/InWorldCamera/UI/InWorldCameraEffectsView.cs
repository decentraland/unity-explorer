using DCL.Utilities;
using UnityEngine;

namespace DCL.InWorldCamera.UI
{
    public class InWorldCameraEffectsView : MonoBehaviour
    {
        private struct DefaultCameraEffects
        {
            public float PostExposure;
            public float Contrast;
            public float Saturation;
            public float HueShift;
            public Color FilterColor;
        }

        [field: Header("Basic Color Adjustments")]
        [field: SerializeField, Range(-5f, 5f)] private float postExposure;
        [field: SerializeField, Range(-100f, 100f)] private float contrast;
        [field: SerializeField, Range(-100f, 100f)] private float saturation;
        [field: SerializeField, Range(-100f, 100f)] private float hueShift;

        [Header("Color Filter")]
        [field: SerializeField, Range(0, 360f)] private float filterHue;
        [field: SerializeField, Range(0, 360f)] private float filterSaturation;
        [field: SerializeField, Range(0, 100f)] private float filterValue = 100f;
        [field: SerializeField, Range(0, 360f)] public float FilterIntensity { get; private set; }

        private Color filterColor = Color.white;

        public ReactiveProperty<float> PostExposure { get; private set; }
        public ReactiveProperty<float> Contrast { get; private set;}
        public ReactiveProperty<float> Saturation { get; private set; }
        public ReactiveProperty<float> HueShift { get; private set; }
        public ReactiveProperty<Color> FilterColor { get; private set; }

        [Header("Depth of Field")]
        [field: SerializeField] public bool EnableDOF = false;
        [field: SerializeField, Range(0, 50f)] public float FocusDistance = 10f;
        [field: SerializeField, Range(1, 300f)] public float FocalLength = 50f;  // Changed range to be more camera-like
        [field: SerializeField, Range(1, 32f)] public float Aperture = 5.6f;     // F-stops like in real cameras

        // [Header("Autofocus")]
        // [field: SerializeField] public bool EnableAutofocus = false;
        // [field: SerializeField] public LayerMask AutofocusLayers = -1;  // All layers by default
        // [field: SerializeField, Range(1, 60f)] public float AutofocusUpdateRate = 4;    // Updates per second
        // [field: SerializeField, Range(0.1f, 20f)] public float AutofocusBlendSpeed = 5f;  // How smooth the focus transition is
        // [field: SerializeField, Range(0.1f, 100f)] public float AutofocusMaxDistance = 50f;
        // [field: SerializeField, Range(0.01f, 0.3f)] public float AutofocusAreaSize = 0.1f;  // Size of focus sampling area

        private DefaultCameraEffects defaults;

        private void Awake()
        {
            defaults = new DefaultCameraEffects
            {
                PostExposure = 0f,
                Contrast = 0f,
                Saturation = 0f,
                HueShift = 0f,
                FilterColor = Color.white
            };

            PostExposure = new ReactiveProperty<float>(postExposure);
            Contrast = new ReactiveProperty<float>(contrast);
            Saturation = new ReactiveProperty<float>(saturation);
            HueShift = new ReactiveProperty<float>(hueShift);
            FilterColor = new ReactiveProperty<Color>(filterColor);
        }

        private void Update()
        {
            PostExposure.Value = postExposure;
            Contrast.Value = contrast;
            Saturation.Value = saturation;
            HueShift.Value = hueShift;

            filterColor = Color.HSVToRGB(filterHue / 360f, filterSaturation / 100f, filterValue / 100f); // ranges are 0-1 in RGB
            FilterColor.Value = filterColor;
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
        }
    }
}
