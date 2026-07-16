using System;

namespace DCL.SocialService
{
    public interface ISocialServiceEventBus
    {
        event Action TransportClosed;
        event Action RPCClientReconnected;
        event Action WebSocketConnectionEstablished;

        void SendTransportClosedNotification();

        void SendTransportReconnectedNotification();

        void SendWebSocketConnectionEstablishedNotification();
    }

    public class SocialServiceEventBus : ISocialServiceEventBus
    {
        public event Action TransportClosed;
        public event Action RPCClientReconnected;
        public event Action WebSocketConnectionEstablished;

        public void SendTransportClosedNotification()
        {
            TransportClosed?.Invoke();
        }

        public void SendTransportReconnectedNotification()
        {
            RPCClientReconnected?.Invoke();
        }

        public void SendWebSocketConnectionEstablishedNotification()
        {
            WebSocketConnectionEstablished?.Invoke();
        }
    }
}
