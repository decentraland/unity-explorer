using DCL.UI;
using System;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackCommandBus : IBackpackCommandBus
    {
        public event Action<BackpackEquipCommand> OnEquipMessageReceived;
        public event Action<BackpackUnEquipCommand> OnUnEquipMessageReceived;
        public event Action<BackpackSelectCommand> OnSelectMessageReceived;
        public event Action<BackpackHideCommand> OnHideMessageReceived;

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
    }
}
