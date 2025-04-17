using System;

namespace DCL.SocialService
{
    public interface ISocialServiceEventBus
    {
        event Action TransportClosed;
        event Action RPCClientReconnected;

        void OnTransportClosed();

        void OnTransportReconnected();
    }

    public class SocialServiceEventBus : ISocialServiceEventBus
    {
        public event Action TransportClosed;
        public event Action RPCClientReconnected;

        public void OnTransportClosed()
        {
            TransportClosed?.Invoke();
        }

        public void OnTransportReconnected()
        {
            RPCClientReconnected?.Invoke();
        }
    }
}
