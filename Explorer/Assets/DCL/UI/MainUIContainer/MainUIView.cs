using DCL.Chat;
using DCL.Friends.UI.FriendPanel;
using DCL.Friends.UI.PushNotifications;
using DCL.MarketplaceCredits;
using DCL.Minimap;
using DCL.UI.ConnectionStatusPanel;
using DCL.UI.Controls;
using DCL.UI.Sidebar;
using DCL.VoiceChat;
using DCL.VoiceChat.CommunityVoiceChat;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.MainUI
{
    public class MainUIView : ViewBase, IView
    {
        [field: SerializeField] public ChatView ChatView { get; private set; }
        [field: SerializeField] public VoiceChatView VoiceChatView { get; private set; }
        [field: SerializeField] public VoiceChatPanelResizeView VoiceChatPanelResizeView { get; private set; }
        [field: SerializeField] public CommunityVoiceChatTitlebarView CommunityVoiceChatView { get; private set; }
        [field: SerializeField] public SceneVoiceChatTitlebarView SceneVoiceChatTitlebarView { get; private set; }
        [field: SerializeField] public FriendsPanelView FriendsPanelViewView { get; private set; }
        [field: SerializeField] public MinimapView MinimapView { get; private set; }
        [field: SerializeField] public FriendPushNotificationView FriendPushNotificationView { get; private set; }
        [field: SerializeField] public MarketplaceCreditsMenuView MarketplaceCreditsMenuView { get; private set; }
        [field: SerializeField] public ConnectionStatusPanelView ConnectionStatusPanelView { get; private set; }
        [field: SerializeField] public SidebarView SidebarView { get; private set; }
        [field: SerializeField] public ControlsPanelView ControlsPanelView { get; private set; }
        [field: SerializeField] public WarningNotificationView WarningNotification { get; private set; }
        [field: SerializeField] internal PointerDetectionArea pointerDetectionArea { get; private set; }
        [field: SerializeField] internal LayoutElement sidebarLayoutElement { get; private set; }
        [field: SerializeField] internal GameObject sidebarDetectionArea { get; private set; }
    }
}
