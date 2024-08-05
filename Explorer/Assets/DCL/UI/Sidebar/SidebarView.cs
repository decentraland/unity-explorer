using DCL.EmotesWheel;
using DCL.ExplorePanel;
using DCL.Notifications.NotificationsMenu;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Sidebar
{
    public class SidebarView : ViewBase, IView
    {
        [field: Header("Notifications")]
        [field: SerializeField] internal Button notificationsButton { get; private set; }
        [field: SerializeField] public NotificationsMenuView NotificationsMenuView { get; private set; }
        [field: SerializeField] internal GameObject backpackNotificationIndicator { get; private set; }


        [field: Header("Profile")]
        [field: SerializeField] public ProfileWidgetView ProfileWidget { get; private set; }
        [field: SerializeField] internal GameObject profileMenu { get; private set; }
        [field: SerializeField] public SidebarProfileView SidebarProfileView { get; private set; }

        [field: Header("Explore Panel Shortcuts")]
        [field: SerializeField] public PersistentEmoteWheelOpenerView PersistentEmoteWheelOpener { get; private set; }
        [field: SerializeField] internal Button mapButton { get; private set; }
        [field: SerializeField] internal Button backpackButton { get; private set; }
        [field: SerializeField] internal Button settingsButton { get; private set; }


        [field: Header("Sidebar Settings")]
        [field: SerializeField] internal Button sidebarSettingsButton { get; private set; }
        [field: SerializeField] internal UIWidgetWithCloseArea sidebarSettingsWidget { get; private set; }
        [field: SerializeField] internal Toggle autoHideToggle { get; private set; }
    }
}
