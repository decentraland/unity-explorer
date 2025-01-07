using DCL.UI.GenericContextMenu.Controls.Configs;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuSeparatorView : GenericContextMenuComponent
    {
        [field: SerializeField] public LayoutElement LayoutElementComponent { get; private set; }

        public override void Configure(ContextMenuControlSettings settings, object initialValue)
        {
            SeparatorContextMenuControlSettings separatorSettings = settings as SeparatorContextMenuControlSettings;
            LayoutElementComponent.preferredHeight = separatorSettings!.Height;
            LayoutElementComponent.minHeight = separatorSettings.Height;
            HorizontalLayoutComponent.padding.left = separatorSettings.LeftPadding;
            HorizontalLayoutComponent.padding.right = separatorSettings.RightPadding;
            RectTransformComponent.sizeDelta = new Vector2(RectTransformComponent.sizeDelta.x, separatorSettings.Height);
        }

        public override void UnregisterListeners() { }

        public override void RegisterListener(Delegate listener) { }

        public override void RegisterCloseListener(Action listener) { }
    }
}
