using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Shop
{
    public class ShopFilterChipView : MonoBehaviour
    {
        [field: SerializeField] public TMP_Text Label { get; private set; } = null!;
        [field: SerializeField] public Button RemoveButton { get; private set; } = null!;

        public Action<ShopFilterChipView>? RemoveClicked;

        public string Key { get; private set; } = string.Empty;

        private void Awake() =>
            RemoveButton.onClick.AddListener(() => RemoveClicked?.Invoke(this));

        public void Bind(string key, string label)
        {
            Key = key;
            Label.text = label;
        }
    }
}
