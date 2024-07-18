using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Sidebar
{
    public class SidebarView : ViewBase, IView
    {
        [field: SerializeField]
        internal Button profileButton { get; private set; }

        [field: SerializeField]
        internal Button mapButton { get; private set; }

        [field: SerializeField]
        internal Button backpackButton { get; private set; }

        [field: SerializeField]
        internal Button settingsButton { get; private set; }

        [field: SerializeField]
        internal Toggle autoHideToggle { get; private set; }

        [field: SerializeField]
        internal Button emotesButton { get; private set; }
    }
}
