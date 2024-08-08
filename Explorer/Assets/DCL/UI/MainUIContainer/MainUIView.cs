using DCL.Chat;
using DCL.Minimap;
using DCL.UI.ConnectionStatusPanel;
using DCL.UI.Sidebar;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.MainUI
{
    public class MainUIView : ViewBase, IView
    {
        [field: SerializeField] public ChatView ChatView { get; private set; }
        [field: SerializeField] public MinimapView MinimapView { get; private set; }
        [field: SerializeField] public ConnectionStatusPanelView ConnectionStatusPanelView { get; private set; }
        [field: SerializeField] public SidebarView SidebarView { get; private set; }
        [field: SerializeField] internal PointerDetectionArea pointerDetectionArea { get; private set; }
        [field: SerializeField] internal LayoutElement sidebarLayoutElement { get; private set; }
    }
}
