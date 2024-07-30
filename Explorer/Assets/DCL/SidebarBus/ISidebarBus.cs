using System;

namespace DCL.SidebarBus
{
    public interface ISidebarBus
    {
        public event Action<bool> SidebarBlockStatusChange;
        void BlockSidebar();
        void UnblockSidebar();
    }
}
