using System;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackCommandBus : IBackpackCommandBus
    {
        public event Action<BackpackEquipWearableCommand>? EquipWearableMessageReceived;
        public event Action<BackpackEquipEmoteCommand>? EquipEmoteMessageReceived;
        public event Action<BackpackUnEquipEmoteCommand>? UnEquipEmoteMessageReceived;
        public event Action<BackpackEmoteSlotSelectCommand>? EmoteSlotSelectMessageReceived;
        public event Action<BackpackUnEquipWearableCommand>? UnEquipWearableMessageReceived;
        public event Action<BackpackSelectWearableCommand>? SelectWearableMessageReceived;
        public event Action<BackpackSelectEmoteCommand>? SelectEmoteMessageReceived;
        public event Action<BackpackHideCommand>? HideMessageReceived;
        public event Action<BackpackFilterCategoryCommand>? FilterCategoryMessageReceived;
        public event Action<BackpackSearchCommand>? SearchMessageReceived;
        public event Action<BackpackUnEquipAllCommand>? UnEquipAllMessageReceived;
        public event Action<BackpackPublishProfileCommand>? PublishProfileReceived;
        public event Action<BackpackChangeColorCommand>? ChangeColorMessageReceived;

        public void SendCommand(BackpackEquipWearableCommand command)
        {
            EquipWearableMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackUnEquipWearableCommand command)
        {
            UnEquipWearableMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackSelectWearableCommand command)
        {
            SelectWearableMessageReceived?.Invoke(command);
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

        public void SendCommand(BackpackChangeColorCommand command)
        {
            ChangeColorMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackUnEquipAllCommand command)
        {
            UnEquipAllMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackPublishProfileCommand command)
        {
            PublishProfileReceived?.Invoke(command);
        }

        public void SendCommand(BackpackEquipEmoteCommand command)
        {
            EquipEmoteMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackUnEquipEmoteCommand command)
        {
            UnEquipEmoteMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackEmoteSlotSelectCommand command)
        {
            EmoteSlotSelectMessageReceived?.Invoke(command);
        }
    }
}
