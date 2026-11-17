using DCL.UI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DCL.Shop
{
    /// <summary>
    ///     The wearable category dropdown above the grid: "All" plus one option per entry of <see cref="ShopCategoryTree.WEARABLE_CHIPS" />.
    /// </summary>
    public class ShopCategoryDropdownView : MonoBehaviour
    {
        [field: SerializeField] public DropdownView Dropdown { get; private set; } = null!;
        [field: SerializeField] public string AllLabel { get; private set; } = "All";

        private readonly List<string?> optionKeys = new ();
        private readonly List<TMP_Dropdown.OptionData> options = new ();

        public event Action<string?>? Selected;

        /// <summary>Fills the options once; the "All" option carries a null key.</summary>
        public void Initialize()
        {
            if (optionKeys.Count > 0)
                return;

            AddOption(null, AllLabel);

            foreach (ShopCategoryTree.Node node in ShopCategoryTree.WEARABLE_CHIPS)
                AddOption(node.Key, node.Label);

            TMP_Dropdown dropdown = Dropdown.Dropdown;
            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.onValueChanged.AddListener(index => Selected?.Invoke(optionKeys[index]));
            SetSelected(null);
        }

        public void SetSelected(string? key)
        {
            int index = Math.Max(0, optionKeys.IndexOf(key));
            Dropdown.Dropdown.SetValueWithoutNotify(index);
        }

        private void AddOption(string? key, string label)
        {
            optionKeys.Add(key);
            options.Add(new TMP_Dropdown.OptionData(label));
        }
    }
}
