using DCL.UI.GenericContextMenu.Controls.Configs;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuSeparatorView : MonoBehaviour, IGenericContextMenuComponent
    {
        [field: SerializeField] public LayoutElement LayoutElementComponent { get; private set; }
        [field: SerializeField] public RectTransform RectTransformComponent { get; private set; }

        public void Configure(ContextMenuControlSettings settings)
        {
            SeparatorContextMenuControlSettings separatorSettings = settings as SeparatorContextMenuControlSettings;
            LayoutElementComponent.preferredHeight = separatorSettings!.Height;
            LayoutElementComponent.minHeight = separatorSettings.Height;
            RectTransformComponent.sizeDelta = new Vector2(RectTransformComponent.sizeDelta.x, separatorSettings.Height);
        }

        public void UnregisterListeners(){}
    }
}
