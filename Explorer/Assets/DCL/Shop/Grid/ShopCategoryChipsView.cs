using DCL.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Shop
{
    /// <summary>
    ///     The wearable category row above the grid: "All" plus one chip per entry of <see cref="ShopCategoryTree.WEARABLE_CHIPS" />.
    /// </summary>
    public class ShopCategoryChipsView : MonoBehaviour
    {
        [field: SerializeField] public ButtonWithSelectableStateView ChipPrefab { get; private set; } = null!;
        [field: SerializeField] public Transform ChipsContainer { get; private set; } = null!;
        [field: SerializeField] public string AllLabel { get; private set; } = "All";

        private readonly List<ButtonWithSelectableStateView> chips = new ();
        private readonly List<string?> chipKeys = new ();

        public event Action<string?>? Selected;

        /// <summary>Builds the row once; the icon resolver maps a chip's category to its sprite, null leaves the icon hidden.</summary>
        public void Initialize(Func<string?, Sprite?> iconResolver)
        {
            if (chips.Count > 0)
                return;

            AddChip(null, AllLabel, null, iconResolver);

            foreach (ShopCategoryTree.Node node in ShopCategoryTree.WEARABLE_CHIPS)
                AddChip(node.Key, node.Label, node.IconCategory, iconResolver);

            SetSelected(null);
        }

        public void SetSelected(string? key)
        {
            for (var i = 0; i < chips.Count; i++)
                chips[i].SetSelected(chipKeys[i] == key);
        }

        private void AddChip(string? key, string label, string? iconCategory, Func<string?, Sprite?> iconResolver)
        {
            ButtonWithSelectableStateView chip = Instantiate(ChipPrefab, ChipsContainer);
            chip.gameObject.SetActive(true);
            chip.Text.text = label;

            Sprite? icon = iconCategory == null ? null : iconResolver(iconCategory);
            chip.IconImage.gameObject.SetActive(icon != null);

            if (icon != null)
                chip.IconImage.sprite = icon;

            chip.Button.onClick.AddListener(() =>
            {
                SetSelected(key);
                Selected?.Invoke(key);
            });

            chips.Add(chip);
            chipKeys.Add(key);
        }
    }
}
