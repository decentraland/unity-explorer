using DCL.Diagnostics;
using MVC;
using UnityEngine.EventSystems;

namespace DCL.UI.Sidebar
{
    public class SidebarConfigPanelView : ViewBaseWithAnimationElement, IView, IPointerClickHandler
    {
        // Swallow the click event so it's not processed by the main sidebar button: retriggers -> cancel previous token -> panel stuck
        public void OnPointerClick(PointerEventData eventData)
        {
#if UNITY_EDITOR
            ReportHub.Log(ReportCategory.UI, "Swallowed click on view level");
#endif
        }
    }
}

