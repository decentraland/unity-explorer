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

        [Range(MIN_VALUE, MAX_VALUE), SerializeField] private float valuePercentage;

        [Space]
        [Header("Configuration")]
        [SerializeField] private float maxRealValue;
        [SerializeField] private float minRealValue;

        [Space]
        [Header("Internal references")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform foreground;
        [SerializeField] private TMP_Text label;

        public void SetPercentageValue(float value, float min, float max)
        {
            this.minRealValue = min;
            this.maxRealValue = max;
            this.valuePercentage = Mathf.Clamp(value, minRealValue, maxRealValue);
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
            bool isFractionAllZero = minRealValue == 0 && maxRealValue == 0;
            string numerator = !isFractionAllZero ? Mathf.RoundToInt(Mathf.Lerp(minRealValue, maxRealValue, valuePercentage / MAX_VALUE)).ToString() : "-";
            string denominator = !isFractionAllZero ? maxRealValue.ToString(CultureInfo.InvariantCulture) : "-";

            label.SetText(string.Format(labelString, numerator, denominator));

            foreground.sizeDelta = new Vector2(valuePercentage * (background.rect.width / 100f), foreground.sizeDelta.y);
        }
    }
}
