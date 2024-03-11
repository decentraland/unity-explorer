using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackCommandBus : IBackpackCommandBus
    {
        public event Action<BackpackEquipCommand> OnEquipMessageReceived;
        public event Action<BackpackUnEquipCommand> OnUnEquipMessageReceived;
        public event Action<BackpackSelectCommand> OnSelectMessageReceived;
        public event Action<BackpackHideCommand> OnHideMessageReceived;
        public event Action<BackpackFilterCategoryCommand> OnFilterCategoryMessageReceived;
        public event Action<BackpackSearchCommand> OnSearchMessageReceived;
        public event Action<BackpackPublishProfileCommand> OnPublishProfileReceived;

        public void SendCommand(BackpackEquipCommand command)
        {
            OnEquipMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackUnEquipCommand command)
        {
            OnUnEquipMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackSelectCommand command)
        {
            OnSelectMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackHideCommand command)
        {
            OnHideMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackFilterCategoryCommand command)
        {
            OnFilterCategoryMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackSearchCommand command)
        {
            OnSearchMessageReceived?.Invoke(command);
        }

        public void SendCommand(BackpackPublishProfileCommand command)
        {
            OnPublishProfileReceived?.Invoke(command);
        }
    }
}
