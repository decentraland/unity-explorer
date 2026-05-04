using DCL.Utility.Types;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using Utility.Networking;

namespace DCL.Multiplayer.Connections.Archipelago.LiveConnections
{
    public class ConnectionClosedException : Exception
    {
        private readonly DCLWebSocket webSocket;

        public override string Message => $"WebSocket closed with state: {webSocket.State} with inner message: {base.Message}";

        public ConnectionClosedException(DCLWebSocket webSocket) : base("Connection closed")
        {
            this.webSocket = webSocket;
        }

        public static EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> NewErrorResult(DCLWebSocket webSocket, Exception? inner = null) =>
            EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(
                IArchipelagoLiveConnection.ResponseError.ConnectionClosed,
                $"WebSocket closed with state: {webSocket.State} - Connection closed Connection closed: {(inner is null ? string.Empty : $" ({inner.GetType().Name}: {inner.Message})")}"
            );
    }
}
