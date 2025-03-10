﻿using DCL.EmotesWheel;
using DCL.Friends.UI;
using DCL.Friends.UI.FriendPanel;
using DCL.MarketplaceCredits;
using DCL.Notifications.NotificationsMenu;
using DCL.UI.Buttons;
using DCL.UI.Controls;
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
        [field: SerializeField] public HoverableAndSelectableButtonWithAnimator notificationsButton { get; private set; }
        [field: SerializeField] public NotificationsMenuView NotificationsMenuView { get; private set; }
        [field: SerializeField] internal GameObject backpackNotificationIndicator { get; private set; }


        [field: Header("Profile")]
        [field: SerializeField] public ProfileWidgetView ProfileWidget { get; private set; }
        [field: SerializeField] internal GameObject profileMenu { get; private set; }
        [field: SerializeField] public ProfileMenuView ProfileMenuView { get; private set; }

        [field: Header("Marketplace Credits")]
        [field: SerializeField] public HoverableAndSelectableButtonWithAnimator MarketplaceCreditsButton { get; private set; } = null!;
        [field: SerializeField] public MarketplaceCreditsMenuView MarketplaceCreditsMenuView { get; private set; }

        [field: Header("Explore Panel Shortcuts")]
        [field: SerializeField] public PersistentEmoteWheelOpenerView PersistentEmoteWheelOpener { get; private set; }
        [field: SerializeField] public Button InWorldCameraButton { get; private set; }
        [field: SerializeField] internal Button mapButton { get; private set; }
        [field: SerializeField] internal Button backpackButton { get; private set; }
        [field: SerializeField] internal Button cameraReelButton { get; private set; }
        [field: SerializeField] internal Button settingsButton { get; private set; }

        [field: Header("Friends")]
        [field: SerializeField] public PersistentFriendPanelOpenerView PersistentFriendsPanelOpener { get; private set; }
        [field: SerializeField] public NotificationIndicatorView FriendRequestNotificationIndicator { get; private set; }

        [field: Header("Skybox")]
        [field: SerializeField] internal SimpleHoverableButton skyboxButton { get; private set; }
        [field: SerializeField] public SkyboxMenuView SkyboxMenuView { get; private set; }

        [field: Header("Sidebar Settings")]
        [field: SerializeField] internal Button sidebarSettingsButton { get; private set; }
        [field: SerializeField] internal ElementWithCloseArea sidebarSettingsWidget { get; private set; }
        [field: SerializeField] internal Toggle autoHideToggle { get; private set; }


        [field: Header("Help")]
        [field: SerializeField] internal Button helpButton { get; private set; }

        [field: Header("Controls")]
        [field: SerializeField] internal Button controlsButton { get; private set; }

        [field: Header("Chat")]
        [field: SerializeField] internal Button unreadMessagesButton { get; private set; }
        [field: SerializeField] internal NumericBadgeUIElement chatUnreadMessagesNumber { get; private set; }
    }
}
