using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Net.WebSockets;
using Utility.Types;

namespace DCL.Multiplayer.Connections.Archipelago.LiveConnections
{
    public class ConnectionClosedException : Exception
    {
        private readonly WebSocket webSocket;

        public override string Message => $"WebSocket closed with state: {webSocket.State} with status: {webSocket.CloseStatus} with description: {webSocket.CloseStatusDescription} with inner message: {base.Message}";

        public ConnectionClosedException(WebSocket webSocket) : base("Connection closed")
        {
            this.webSocket = webSocket;
        }

        public static EnumResult<MemoryWrap, IArchipelagoLiveConnection.ReceiveResponse> NewErrorResult(WebSocket webSocket) =>
            EnumResult<MemoryWrap, IArchipelagoLiveConnection.ReceiveResponse>.ErrorResult(
                IArchipelagoLiveConnection.ReceiveResponse.ConnectionClosed,
                $"WebSocket closed with state: {webSocket.State} with status: {webSocket.CloseStatus} with description: {webSocket.CloseStatusDescription} - Connection closed"
            );
    }
}
