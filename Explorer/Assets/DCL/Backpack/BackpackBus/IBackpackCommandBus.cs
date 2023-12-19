using System;

namespace DCL.Backpack.BackpackBus
{
    public interface IBackpackCommandBus
    {
        public event Action<BackpackCommand> OnMessageReceived;

        void SendCommand(BackpackCommand command);
    }
}
