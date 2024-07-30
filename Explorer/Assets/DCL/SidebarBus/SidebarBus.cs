using System;

namespace DCL.SidebarBus
{
    public class SidebarBus : ISidebarBus
    {
        public event Action<bool> SidebarBlockStatusChange;
        public event Action<bool> SidebarAutohideStatusChange;

        public void BlockSidebar()
        {
            SidebarBlockStatusChange?.Invoke(true);
        }

        public void UnblockSidebar()
        {
            SidebarBlockStatusChange?.Invoke(false);
        }

        public void SetAutoHideSidebarStatus(bool status)
        {
            SidebarAutohideStatusChange?.Invoke(status);
        }
    }
}
