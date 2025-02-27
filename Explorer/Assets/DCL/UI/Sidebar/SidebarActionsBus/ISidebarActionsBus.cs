using System;

namespace DCL.UI.Sidebar.SidebarActionsBus
{
    // TODO: REMOVE
    public interface ISidebarActionsBus
    {
        void SubscribeOnWidgetOpen(Action callback);
        void SubscribeOnCloseAllWidgets(Action callback);
        void CloseAllWidgets();
        void OpenWidget();
    }
}
