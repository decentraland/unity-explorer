using DCL.UI.GenericContextMenu.Controls.Configs;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuSeparatorView : GenericContextMenuComponent
    {
        [field: SerializeField] public LayoutElement LayoutElementComponent { get; private set; }

        public override void Configure(ContextMenuControlSettings settings)
        {
            SeparatorContextMenuControlSettings separatorSettings = settings as SeparatorContextMenuControlSettings;
            LayoutElementComponent.preferredHeight = separatorSettings!.Height;
            LayoutElementComponent.minHeight = separatorSettings.Height;
            RectTransformComponent.sizeDelta = new Vector2(RectTransformComponent.sizeDelta.x, separatorSettings.Height);
        }

        public override void UnregisterListeners(){}
    }
}
