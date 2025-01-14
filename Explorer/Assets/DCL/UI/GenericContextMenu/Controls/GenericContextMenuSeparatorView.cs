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
            LayoutElementComponent.preferredHeight = settings!.height;
            LayoutElementComponent.minHeight = settings.height;
            HorizontalLayoutComponent.padding.left = settings.leftPadding;
            HorizontalLayoutComponent.padding.right = settings.rightPadding;
            RectTransformComponent.sizeDelta = new Vector2(RectTransformComponent.sizeDelta.x, settings.height);
        }

        public override void UnregisterListeners() { }

        public override void RegisterListener(Delegate listener) { }

        public override void RegisterCloseListener(Action listener) { }
    }
}
