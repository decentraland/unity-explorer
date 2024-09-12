using System;

namespace DCL.SidebarBus
{
    public interface ISidebarBus
    {
        public event Action<bool> SidebarBlockStatusChange;
        public event Action<bool> SidebarAutohideStatusChange;

        void BlockSidebar();
        void UnblockSidebar();

        void SetAutoHideSidebarStatus(bool status);
    }
}
