using DCL.UI;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Shop
{
    public class ShopCategoryRowView : MonoBehaviour
    {
        [field: SerializeField] public ButtonWithSelectableStateView Button { get; private set; } = null!;
        [field: SerializeField] public Image Icon { get; private set; } = null!;
        [field: SerializeField] public RectTransform? Chevron { get; private set; }
        [field: SerializeField] public LayoutElement Indent { get; private set; } = null!;
        [field: SerializeField] public float IndentPerDepth { get; private set; } = 16f;

        public Action<ShopCategoryRowView>? Clicked;

        public ShopCategoryTree.Node? Node { get; private set; }

        private void Awake() =>
            Button.Button.onClick.AddListener(() => Clicked?.Invoke(this));

        public void Bind(ShopCategoryTree.Node node, int depth, bool selected, bool expanded, Sprite? icon)
        {
            Node = node;
            Button.Text.text = node.Label;
            Button.SetSelected(selected);
            Icon.enabled = icon != null;

            if (icon != null)
                Icon.sprite = icon;

            Indent.minWidth = depth * IndentPerDepth;

            if (Chevron == null)
                return;

            Chevron.gameObject.SetActive(node.HasChildren);
            Chevron.localRotation = Quaternion.Euler(0f, 0f, expanded ? 180f : 0f);
        }
    }
}
