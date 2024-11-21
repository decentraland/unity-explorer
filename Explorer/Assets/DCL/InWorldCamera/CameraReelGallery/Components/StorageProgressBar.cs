using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReelGallery.Components
{
    public class StorageProgressBar : MonoBehaviour
    {
        private const float MIN_VALUE = 0f;
        private const float MAX_VALUE = 100f;

        private string labelString = "Storage {0}/{1} photos taken";

        [Range(MIN_VALUE, MAX_VALUE)] public float valuePercentage;

        [Space]
        [Header("Configuration")]
        public float? MaxRealValue;
        public float? MinRealValue;

        [Space]
        [Header("Internal references")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform foreground;
        [SerializeField] private TMP_Text label;

        public void SetPercentageValue(float value)
        {
            if (!MaxRealValue.HasValue || !MinRealValue.HasValue)
                throw new Exception("MaxRealValue and MinRealValue must be set before setting a value");

            this.valuePercentage = Mathf.Clamp(value, MinRealValue.Value, MaxRealValue.Value);
            UpdateGraphics();
        }

        public void SetLabelString(string labelStr) =>
            this.labelString = labelStr;

        private void OnValidate()
        {
            if (background is null || foreground is null || label is null) return;

            UpdateGraphics();
        }

        private void UpdateGraphics()
        {
            string numerator = MaxRealValue.HasValue && MinRealValue.HasValue ? Mathf.RoundToInt(Mathf.Lerp(MinRealValue.Value, MaxRealValue.Value, valuePercentage / MAX_VALUE)).ToString() : "-";
            string denominator = MaxRealValue.HasValue ? MaxRealValue.Value.ToString(CultureInfo.InvariantCulture) : "-";

            label.SetText(string.Format(labelString, numerator, denominator));

            if (!MaxRealValue.HasValue || !MinRealValue.HasValue)
                return;

            foreground.sizeDelta = new Vector2(valuePercentage * (background.rect.width / 100f), foreground.sizeDelta.y);
        }
    }
}
