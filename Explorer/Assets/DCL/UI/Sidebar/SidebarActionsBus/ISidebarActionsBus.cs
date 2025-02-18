using System;

namespace DCL.UI.Sidebar.SidebarActionsBus
{
    public interface ISidebarActionsBus
    {
        void SubscribeOnWidgetOpen(Action callback);
        void SubscribeOnCloseAllWidgets(Action callback);
        void CloseAllWidgets();
        void OpenWidget();
    }
}
