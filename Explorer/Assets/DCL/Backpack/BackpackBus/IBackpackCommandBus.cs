using System;

namespace DCL.Backpack.BackpackBus
{
    public interface IBackpackCommandBus
    {
        public event Action<BackpackEquipWearableCommand> EquipWearableMessageReceived;
        public event Action<BackpackUnEquipWearableCommand> UnEquipWearableMessageReceived;
        public event Action<BackpackEquipEmoteCommand> EquipEmoteMessageReceived;
        public event Action<BackpackUnEquipEmoteCommand> UnEquipEmoteMessageReceived;
        public event Action<BackpackEmoteSlotSelectCommand>? EmoteSlotSelectMessageReceived;
        public event Action<BackpackSelectWearableCommand> SelectWearableMessageReceived;
        public event Action<BackpackSelectEmoteCommand> SelectEmoteMessageReceived;
        public event Action<BackpackHideCommand> HideMessageReceived;
        public event Action<BackpackFilterCategoryCommand> FilterCategoryMessageReceived;
        public event Action<BackpackSearchCommand> SearchMessageReceived;
        public event Action<BackpackPublishProfileCommand> PublishProfileReceived;
        public event Action<BackpackUnEquipAllCommand>? UnEquipAllMessageReceived;

        void SendCommand(BackpackEquipWearableCommand command);

        void SendCommand(BackpackUnEquipWearableCommand command);

        void SendCommand(BackpackSelectWearableCommand command);

        void SendCommand(BackpackHideCommand command);

        void SendCommand(BackpackFilterCategoryCommand command);

        void SendCommand(BackpackSearchCommand command);

        void SendCommand(BackpackEmoteSlotSelectCommand command);

        void SendCommand(BackpackUnEquipEmoteCommand command);

        void SendCommand(BackpackUnEquipAllCommand command);
    }
}
