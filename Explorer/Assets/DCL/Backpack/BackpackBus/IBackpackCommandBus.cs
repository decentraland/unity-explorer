using System;

namespace DCL.Backpack.BackpackBus
{
    public interface IBackpackCommandBus
    {
        public event Action<BackpackEquipCommand> OnEquipMessageReceived;
        public event Action<BackpackUnEquipCommand> OnUnEquipMessageReceived;
        public event Action<BackpackSelectCommand> OnSelectMessageReceived;
        public event Action<BackpackHideCommand> OnHideMessageReceived;
        public event Action<BackpackFilterCategoryCommand> OnFilterCategoryMessageReceived;
        public event Action<BackpackSearchCommand> OnSearchMessageReceived;
        public event Action<BackpackPublishProfileCommand> OnPublishProfileReceived;

        void SendCommand(BackpackEquipCommand command);
        void SendCommand(BackpackUnEquipCommand command);
        void SendCommand(BackpackSelectCommand command);
        void SendCommand(BackpackHideCommand command);

        void SendCommand(BackpackFilterCategoryCommand command);
        void SendCommand(BackpackSearchCommand command);
    }
}
