using DCL.ChatArea;
using DCL.Friends.UI.FriendPanel;
using DCL.Friends.UI.PushNotifications;
using DCL.MarketplaceCredits;
using DCL.Minimap;
using DCL.UI.Controls;
using DCL.UI.Sidebar;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.MainUI
{
    public class MainUIView : ViewBase, IView
    {
        [field: SerializeField] public ChatMainSharedAreaView ChatMainView { get; private set; } = null!;
        [field: SerializeField] public FriendsPanelView FriendsPanelViewView { get; private set; } = null!;
        [field: SerializeField] public MinimapView MinimapView { get; private set; } = null!;
        [field: SerializeField] public FriendPushNotificationView FriendPushNotificationView { get; private set; } = null!;
        [field: SerializeField] public MarketplaceCreditsMenuView MarketplaceCreditsMenuView { get; private set; } = null!;
        [field: SerializeField] public SidebarView SidebarView { get; private set; } = null!;
        [field: SerializeField] public ControlsPanelView ControlsPanelView { get; private set; } = null!;
        [field: SerializeField] public WarningNotificationView WarningNotification { get; private set; } = null!;
        [field: SerializeField] public WarningNotificationView PerformanceModeOnToast { get; private set; } = null!;
        [field: SerializeField] public WarningNotificationView EndOfSceneToast { get; private set; } = null!;
        [field: SerializeField] public Button EndOfSceneOpenPlacesButton { get; private set; } = null!;
        [field: SerializeField] internal PointerDetectionArea pointerDetectionArea { get; private set; } = null!;
        [field: SerializeField] internal LayoutElement sidebarLayoutElement { get; private set; } = null!;
        [field: SerializeField] internal GameObject sidebarDetectionArea { get; private set; } = null!;
    }
}
