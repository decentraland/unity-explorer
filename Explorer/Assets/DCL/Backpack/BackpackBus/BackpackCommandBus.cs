using System;

namespace DCL.Backpack.BackpackBus
{
    public class BackpackCommandBus : IBackpackCommandBus
    {
        public event Action<BackpackCommand> OnMessageReceived;

        public void SendCommand(BackpackCommand command)
        {
            OnMessageReceived?.Invoke(command);
        }
    }
}
