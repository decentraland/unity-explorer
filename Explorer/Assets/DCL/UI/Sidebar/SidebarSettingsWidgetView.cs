using DCL.UI;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.Sidebar
{
    public class SidebarSettingsWidgetView : ViewBaseWithAnimationElement, IView
    {
        [field: SerializeField] internal Button closeButton { get; private set; } = null!;
    }
}

