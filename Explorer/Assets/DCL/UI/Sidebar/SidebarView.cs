using DCL.EmotesWheel;
using DCL.ExplorePanel;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Sidebar
{
    public class SidebarView : ViewBase, IView
    {
        [field: SerializeField]
        public ProfileWidgetView ProfileWidget { get; private set; }
        [field: SerializeField]
        public SystemMenuView SystemMenuView { get; private set; }

        [field: SerializeField]
        public PersistentEmoteWheelOpenerView PersistentEmoteWheelOpener { get; private set; }

        [field: SerializeField]
        public ProfileWidgetView ProfileMenuWidget { get; private set; }

        [field: SerializeField]
        internal Button mapButton { get; private set; }

        [field: SerializeField]
        internal Button backpackButton { get; private set; }

        [field: SerializeField]
        internal Button settingsButton { get; private set; }

        [field: SerializeField]
        internal Toggle autoHideToggle { get; private set; }

        [field: SerializeField]
        internal GameObject profileMenu { get; private set; }

        [field: SerializeField]
        internal GameObject backpackNotificationIndicator { get; private set; }

    }
}
