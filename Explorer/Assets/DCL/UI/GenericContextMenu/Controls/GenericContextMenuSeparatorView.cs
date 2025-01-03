using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu.Controls
{
    public class GenericContextMenuSeparatorView : MonoBehaviour, IGenericContextMenuComponent
    {
        [field: SerializeField] public LayoutElement LayoutElementComponent { get; private set; }

        public void Configure(ContextMenuControlSettings settings)
        {
            SeparatorContextMenuControlSettings separatorSettings = settings as SeparatorContextMenuControlSettings;
            LayoutElementComponent.preferredHeight = separatorSettings!.Height;
        }

        public void UnregisterListeners(){}
    }
}
