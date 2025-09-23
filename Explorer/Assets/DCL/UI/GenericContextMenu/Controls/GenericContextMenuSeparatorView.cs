using DCL.UI.Controls.Configs;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Controls
{
    public class GenericContextMenuSeparatorView : GenericContextMenuComponentBase
    {
        [field: SerializeField] public LayoutElement LayoutElementComponent { get; private set; }

        public void Configure(SeparatorContextMenuControlSettings settings)
        {
            LayoutElementComponent.preferredHeight = settings!.height;
            LayoutElementComponent.minHeight = settings.height;
            HorizontalLayoutComponent.padding.left = settings.leftPadding;
            HorizontalLayoutComponent.padding.right = settings.rightPadding;
            RectTransformComponent.sizeDelta = new Vector2(RectTransformComponent.sizeDelta.x, settings.height);
        }

        public override void UnregisterListeners() { }

        public override void RegisterCloseListener(Action listener) { }
    }
}
