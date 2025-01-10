using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuSeparatorView : GenericContextMenuComponent<SeparatorContextMenuControlSettings>
    {
        [field: SerializeField] public LayoutElement LayoutElementComponent { get; private set; }

        public override void Configure(SeparatorContextMenuControlSettings settings, object initialValue)
        {
            LayoutElementComponent.preferredHeight = settings!.Height;
            LayoutElementComponent.minHeight = settings.Height;
            HorizontalLayoutComponent.padding.left = settings.LeftPadding;
            HorizontalLayoutComponent.padding.right = settings.RightPadding;
            RectTransformComponent.sizeDelta = new Vector2(RectTransformComponent.sizeDelta.x, settings.Height);
        }

        public override void UnregisterListeners() { }

        public override void RegisterListener(Delegate listener) { }

        public override void RegisterCloseListener(Action listener) { }
    }
}
