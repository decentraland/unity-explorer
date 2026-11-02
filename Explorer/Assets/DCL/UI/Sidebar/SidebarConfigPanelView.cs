using DCL.Diagnostics;
using MVC;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.UI.Sidebar
{
    public class SidebarConfigPanelView : ViewBaseWithAnimationElement, IView, IPointerClickHandler
    {
        [field: SerializeField] public Toggle AutoHideToggle { get; private set; } = null!;

        // Swallow the click event so it's not processed by the main sidebar button: retriggers -> cancel previous token -> panel stuck
        public void OnPointerClick(PointerEventData eventData)
        {
#if UNITY_EDITOR
            ReportHub.Log(ReportCategory.UI, "Swallowed click on view level");
#endif
        }
    }
}

