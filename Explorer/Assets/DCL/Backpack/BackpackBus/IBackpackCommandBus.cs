using System;

namespace DCL.Backpack.BackpackBus
{
    public interface IBackpackCommandBus
    {
        public event Action<BackpackEquipWearableCommand> EquipWearableMessageReceived;
        public event Action<BackpackUnEquipWearableCommand> UnEquipWearableMessageReceived;
        public event Action<BackpackEquipEmoteCommand> EquipEmoteMessageReceived;
        public event Action<BackpackUnEquipEmoteCommand> UnEquipEmoteMessageReceived;
        public event Action<BackpackSelectCommand> SelectMessageReceived;
        public event Action<BackpackHideCommand> HideMessageReceived;
        public event Action<BackpackFilterCategoryCommand> FilterCategoryMessageReceived;
        public event Action<BackpackSearchCommand> SearchMessageReceived;
        public event Action<BackpackPublishProfileCommand> PublishProfileReceived;

        void SendCommand(BackpackEquipWearableCommand command);

        void SendCommand(BackpackUnEquipWearableCommand command);
        void SendCommand(BackpackSelectCommand command);
        void SendCommand(BackpackHideCommand command);
        void SendCommand(BackpackFilterCategoryCommand command);
        void SendCommand(BackpackSearchCommand command);
    }
}
