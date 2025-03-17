using System;

namespace DCL.UI.Sidebar.SidebarActionsBus
{
    public class SidebarActionsBusController : ISidebarActionsBus
    {
        private event Action? OnWidgetOpen;
        private event Action? OnCloseAllWidgets;

        public void SubscribeOnWidgetOpen(Action callback) =>
            OnWidgetOpen += callback;

        public void SubscribeOnCloseAllWidgets(Action callback) =>
            OnCloseAllWidgets += callback;

        public void CloseAllWidgets() =>
            OnCloseAllWidgets?.Invoke();

        public void OpenWidget() =>
            OnWidgetOpen?.Invoke();
    }
}
