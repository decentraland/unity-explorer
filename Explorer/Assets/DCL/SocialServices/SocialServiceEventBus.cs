using System;

namespace DCL.SocialService
{
    public interface ISocialServiceEventBus
    {
        event Action TransportClosed;
        event Action RPCClientReconnected;

        void SendTransportClosedNotification();

        void SendTransportReconnectedNotification();
    }

    public class SocialServiceEventBus : ISocialServiceEventBus
    {
        public event Action TransportClosed;
        public event Action RPCClientReconnected;

        public void SendTransportClosedNotification()
        {
            TransportClosed?.Invoke();
        }

        public void SendTransportReconnectedNotification()
        {
            RPCClientReconnected?.Invoke();
        }
    }
}
