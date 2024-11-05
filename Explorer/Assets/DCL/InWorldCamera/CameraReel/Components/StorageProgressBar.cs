using System;
using TMPro;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class StorageProgressBar : MonoBehaviour
    {
        private const float MIN_VALUE = 0f;
        private const float MAX_VALUE = 100f;

        private const string LABEL_PLACEHOLDER = "Storage {0}/{1} photos taken";

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

        private void OnValidate()
        {
            if (background is null || foreground is null || label is null) return;

            UpdateGraphics();
        }

        private void UpdateGraphics()
        {
            if (!MaxRealValue.HasValue || !MinRealValue.HasValue)
            {
                label.SetText("Storage -/- photos taken");
                return;
            }

            foreground.sizeDelta = new Vector2(valuePercentage * (background.rect.width / 100f), foreground.sizeDelta.y);
            label.SetText(LABEL_PLACEHOLDER, Mathf.RoundToInt(Mathf.Lerp(MinRealValue.Value, MaxRealValue.Value, valuePercentage / MAX_VALUE)), MaxRealValue.Value);
        }
    }
}
