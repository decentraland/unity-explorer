using DCL.Friends.UI.FriendPanel;
using DCL.Notifications.NotificationsMenu;
using DCL.UI.Buttons;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.UI.Skybox;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Sidebar
{
    public class SidebarView : ViewBase, IView
    {
        [field: Header("Notifications")]
        [field: SerializeField] public Button notificationsButton { get; private set; } = null!;
        [field: SerializeField] public NotificationsMenuView NotificationsMenuView { get; private set; } = null!;
        [field: SerializeField] internal GameObject backpackNotificationIndicator { get; private set; } = null!;


        [field: Header("Profile")]
        [field: SerializeField] public ProfileWidgetView ProfileWidget { get; private set; } = null!;
        [field: SerializeField] internal GameObject profileMenu { get; private set; } = null!;
        [field: SerializeField] public ProfileMenuView ProfileMenuView { get; private set; } = null!;

        [field: Header("Explore Panel Shortcuts")]
        [field: SerializeField] public Button InWorldCameraButton { get; private set; } = null!;
        [field: SerializeField] internal Button communitiesButton { get; private set; } = null!;
        [field: SerializeField] internal Button mapButton { get; private set; } = null!;
        [field: SerializeField] internal Button backpackButton { get; private set; } = null!;
        [field: SerializeField] internal Button cameraReelButton { get; private set; } = null!;
        [field: SerializeField] internal Button settingsButton { get; private set; } = null!;

        [field: Header("Friends")]
        [field: SerializeField] public PersistentFriendPanelOpenerView PersistentFriendsPanelOpener { get; private set; } = null!;
        [field: SerializeField] public NotificationIndicatorView FriendRequestNotificationIndicator { get; private set; } = null!;

        [field: Header("Skybox")]
        [field: SerializeField] internal Button skyboxButton { get; private set; } = null!;
        [field: SerializeField] public SkyboxMenuView SkyboxMenuView { get; private set; } = null!;

        [field: Header("Smart Wearables")]
        [field: SerializeField] internal HoverableButton SmartWearablesButton { get; private set; }
        [field: SerializeField] public SmartWearablesSideBarTooltipView SmartWearablesTooltipView { get; private set; }

        [field: Header("Sidebar Settings")]
        [field: SerializeField] internal Button sidebarSettingsButton { get; private set; } = null!;
        [field: SerializeField] internal ElementWithCloseArea sidebarSettingsWidget { get; private set; } = null!;
        [field: SerializeField] internal Toggle autoHideToggle { get; private set; } = null!;

        [field: Header("Emotes")]
        [field: SerializeField] internal Button emotesWheelButton { get; private set; } = null!;

        [field: Header("Friends")]
        [field: SerializeField] internal Button friendsButton { get; private set; } = null!;

        [field: Header("Help")]
        [field: SerializeField] internal Button helpButton { get; private set; } = null!;

        [field: Header("Controls")]
        [field: SerializeField] internal Button controlsButton { get; private set; } = null!;

        [field: Header("Chat")]
        [field: SerializeField] public Button unreadMessagesButton { get; private set; } = null!;
        [field: SerializeField] internal NumericBadgeUIElement chatUnreadMessagesNumber { get; private set; } = null!;

        [field: Header("Marketplace Credits")]
        [field: SerializeField] public HoverableAndSelectableButtonWithAnimator marketplaceCreditsButton { get; private set; } = null!;
        [field: SerializeField] public Animator marketplaceCreditsButtonAnimator { get; private set; } = null!;
        [field: SerializeField] public GameObject marketplaceCreditsButtonAlertMark { get; private set; } = null!;

        public delegate void BlockStatusChangedDelegate(bool status);
        public delegate void AutohideStatusChangedDelegate(bool status);

        public event BlockStatusChangedDelegate? BlockStatusChanged;
        public event AutohideStatusChangedDelegate? AutohideStatusChanged;

        public void BlockSidebar()
        {
            BlockStatusChanged?.Invoke(true);
        }

        public void UnblockSidebar()
        {
            BlockStatusChanged?.Invoke(false);
        }

        public void SetAutoHideSidebarStatus(bool value)
        {
            AutohideStatusChanged?.Invoke(value);
        }
    }
}
