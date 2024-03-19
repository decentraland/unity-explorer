using System;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackCommandBus : IBackpackCommandBus
    {
        public event Action<BackpackEquipWearableCommand>? EquipWearableMessageReceived;
        public event Action<BackpackEquipEmoteCommand>? EquipEmoteMessageReceived;
        public event Action<BackpackUnEquipEmoteCommand>? UnEquipEmoteMessageReceived;
        public event Action<BackpackUnEquipWearableCommand>? UnEquipWearableMessageReceived;
        public event Action<BackpackSelectCommand>? SelectMessageReceived;
        public event Action<BackpackSelectEmoteCommand>? SelectEmoteMessageReceived;
        public event Action<BackpackHideCommand>? HideMessageReceived;
        public event Action<BackpackFilterCategoryCommand>? FilterCategoryMessageReceived;
        public event Action<BackpackSearchCommand>? SearchMessageReceived;
        public event Action<BackpackPublishProfileCommand>? PublishProfileReceived;

        public void SendCommand(BackpackEquipWearableCommand command)
        {
            EquipWearableMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackUnEquipWearableCommand command)
        {
            UnEquipWearableMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackSelectCommand command)
        {
            SelectMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackSelectEmoteCommand command)
        {
            SelectEmoteMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackHideCommand command)
        {
            HideMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackFilterCategoryCommand command)
        {
            FilterCategoryMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackSearchCommand command)
        {
            SearchMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackPublishProfileCommand command)
        {
            PublishProfileReceived?.Invoke(command);
        }

        public void SendCommand(BackpackEquipEmoteCommand command)
        {
            EquipEmoteMessageReceived?.Invoke(command);
        }
    }
}
