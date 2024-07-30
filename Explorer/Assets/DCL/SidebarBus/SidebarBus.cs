using System;

namespace DCL.SidebarBus
{
    public class SidebarBus : ISidebarBus
    {
        public event Action<bool> SidebarBlockStatusChange;

        public void BlockSidebar()
        {
            SidebarBlockStatusChange?.Invoke(true);
        }

        public void UnblockSidebar()
        {
            SidebarBlockStatusChange?.Invoke(false);
        }
    }
}
