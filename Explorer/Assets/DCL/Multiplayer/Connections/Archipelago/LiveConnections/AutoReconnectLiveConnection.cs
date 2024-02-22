using Cysharp.Threading.Tasks;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Archipelago.LiveConnections
{
    public class AutoReconnectLiveConnection : IArchipelagoLiveConnection
    {
        private readonly IArchipelagoLiveConnection origin;
        private readonly Action<string> log;
        private string? cachedAdapterUrl;

        public AutoReconnectLiveConnection(IArchipelagoLiveConnection origin) : this(origin, Debug.LogWarning)
        {
        }

        public AutoReconnectLiveConnection(IArchipelagoLiveConnection origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public bool IsConnected => origin.IsConnected;

        public UniTask ConnectAsync(string adapterUrl, CancellationToken token)
        {
            cachedAdapterUrl = adapterUrl;
            return origin.ConnectAsync(adapterUrl, token);
        }

        public UniTask DisconnectAsync(CancellationToken token)
        {
            cachedAdapterUrl = null;
            return origin.DisconnectAsync(token);
        }

        public UniTask SendAsync(MemoryWrap data, CancellationToken token) =>
            origin.SendAsync(data, token);

        public async UniTask<MemoryWrap> ReceiveAsync(CancellationToken token)
        {
            try
            {
                var result = await origin.ReceiveAsync(token);
                return result;
            }
            catch (ConnectionClosedException)
            {
                log("Connection closed on receiving, trying to reconnect...");

                if (cachedAdapterUrl == null)
                    throw new Exception("Connection closed on receiving, no found cached adapter url");

                await origin.ConnectAsync(cachedAdapterUrl, token);
                return await ReceiveAsync(token);
            }
        }
    }
}
