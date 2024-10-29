using TMPro;
using UnityEngine;

namespace DCL.InWorldCamera.CameraReel.Components
{
    public class StorageProgressBar : MonoBehaviour
    {
        private const float MIN_VALUE = 0f;
        private const float MAX_VALUE = 100f;

        [Range(MIN_VALUE, MAX_VALUE)] public float valuePercentage;

        [Space]
        [Header("Configuration")]
        public float MaxRealValue = 100f;
        public float MinRealValue = 0f;

        [Space]
        [Header("Internal references")]
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform foreground;
        [SerializeField] private TMP_Text label;

        public void SetPercentageValue(float value)
        {
            this.valuePercentage = Mathf.Clamp(value, MinRealValue, MaxRealValue);
            UpdateGraphics();
        }

        private void OnValidate()
        {
            if (background is null || foreground is null) return;

            UpdateGraphics();
        }

        private void UpdateGraphics()
        {
            foreground.sizeDelta = new Vector2(valuePercentage * (background.rect.width / 100f), foreground.sizeDelta.y);
            label.SetText("Storage {0}/{1} photos taken", Mathf.RoundToInt(Mathf.Lerp(MinRealValue, MaxRealValue, valuePercentage / MAX_VALUE)), MaxRealValue);
        }
    }
}
