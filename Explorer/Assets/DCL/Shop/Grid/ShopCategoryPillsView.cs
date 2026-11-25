using DCL.Backpack.Gifting.Styling;
using DCL.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Shop
{
    /// <summary>
    ///     The wearable category pills above the grid: "ALL" plus one pill per entry of <see cref="ShopCategoryTree.WEARABLE_CHIPS" />,
    ///     exactly one selected at a time.
    /// </summary>
    public class ShopCategoryPillsView : MonoBehaviour
    {
        /// <summary>The "ALL" pill authored in the prefab; the category pills are clones of it.</summary>
        [field: SerializeField] private ButtonWithSelectableStateView AllPill { get; set; } = null!;
        [field: SerializeField] private float LabelRightPadding { get; set; } = 16f;

        private readonly List<ButtonWithSelectableStateView> pills = new ();
        private readonly List<string?> pillKeys = new ();

        public event Action<string?>? Selected;

        /// <summary>Creates the pills once; the "ALL" pill carries a null key.</summary>
        public void Initialize(IWearableStylingCatalog styling)
        {
            if (pills.Count > 0)
                return;

            AddPill(AllPill, null);

            foreach (ShopCategoryTree.Node node in ShopCategoryTree.WEARABLE_CHIPS)
            {
                ButtonWithSelectableStateView pill = Instantiate(AllPill, AllPill.transform.parent);
                pill.name = node.Label;
                pill.Text.text = node.Label.ToUpperInvariant();
                pill.IconImage.sprite = styling.GetCategoryIcon(node.IconCategory);
                AddPill(pill, node.Key);
            }

            SetSelected(null);
        }

        public void SetSelected(string? key)
        {
            for (var i = 0; i < pills.Count; i++)
                pills[i].SetSelected(pillKeys[i] == key);
        }

        /// <summary>Lets the row run past the right edge of its parent by the given width.</summary>
        public void SetRightOverflow(float width)
        {
            var row = (RectTransform)transform;
            row.offsetMax = new Vector2(width, row.offsetMax.y);
        }

        private void AddPill(ButtonWithSelectableStateView pill, string? key)
        {
            var pillTransform = (RectTransform)pill.transform;
            // Measured from the string: preferredWidth is still the unmeasured 0 on a label whose text was never set from code
            float width = pill.Text.rectTransform.anchoredPosition.x + pill.Text.GetPreferredValues(pill.Text.text).x + LabelRightPadding;
            pillTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            pill.Button.onClick.AddListener(() => OnPillClicked(pill, key));
            pills.Add(pill);
            pillKeys.Add(key);
        }

        private void OnPillClicked(ButtonWithSelectableStateView pill, string? key)
        {
            if (pill.Selected)
                return;

            SetSelected(key);
            Selected?.Invoke(key);
        }
    }
}
