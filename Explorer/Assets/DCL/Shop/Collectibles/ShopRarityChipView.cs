using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Shop
{
    public class ShopRarityChipView : MonoBehaviour
    {
        [field: SerializeField] public string RarityId { get; private set; } = string.Empty;
        [field: SerializeField] public Toggle Toggle { get; private set; } = null!;
        [field: SerializeField] public Image Swatch { get; private set; } = null!;
        [field: SerializeField] public GameObject? Check { get; private set; }
        [field: SerializeField] public TMP_Text Label { get; private set; } = null!;

        public Action<ShopRarityChipView, bool>? Toggled;

        private void Awake() =>
            Toggle.onValueChanged.AddListener(isOn =>
            {
                Check?.SetActive(isOn);
                Toggled?.Invoke(this, isOn);
            });

        public void SetColor(Color color) =>
            Swatch.color = color;

        public void SetSelectedSilently(bool isOn)
        {
            Toggle.SetIsOnWithoutNotify(isOn);
            Check?.SetActive(isOn);
        }
    }
}
