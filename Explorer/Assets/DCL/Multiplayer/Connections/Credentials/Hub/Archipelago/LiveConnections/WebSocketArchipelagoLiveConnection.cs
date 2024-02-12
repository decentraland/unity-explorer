using Cysharp.Threading.Tasks;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.LiveConnections
{
    public class WebSocketArchipelagoLiveConnection : IArchipelagoLiveConnection
    {
        //private //TODO websocket connection

        public bool Connected() =>
            throw new NotImplementedException();

        public UniTask Connect(string adapterUrl, CancellationToken token) =>
            throw new NotImplementedException();

        public UniTask Disconnect(CancellationToken token) =>
            throw new NotImplementedException();

        public UniTask<MemoryWrap> Send(MemoryWrap data, CancellationToken token) =>
            throw new NotImplementedException();
    }
}
