using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Archipelago.LiveConnections
{
    public class AutoReconnectLiveConnection : IArchipelagoLiveConnection
    {
        private readonly IArchipelagoLiveConnection origin;
        private readonly Action<string> log;
        private string? cachedAdapterUrl;

        public bool IsConnected => origin.IsConnected;

        public AutoReconnectLiveConnection(IArchipelagoLiveConnection origin) : this(
            origin,
            m => ReportHub.Log(ReportCategory.ARCHIPELAGO_REQUEST, m)
        ) { }

        public AutoReconnectLiveConnection(IArchipelagoLiveConnection origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

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

        public async UniTask SendAsync(MemoryWrap data, CancellationToken token)
        {
            try { await origin.SendAsync(data, token); }
            catch (SocketException e)
            {
                log($"Connection lost on sending, trying to reconnect... {e}");
                await EnsureReconnectAsync(token);
                await SendAsync(data, token);
            }
        }

        public async UniTask<MemoryWrap> ReceiveAsync(CancellationToken token)
        {
            try { return await origin.ReceiveAsync(token); }
            catch (ConnectionClosedException)
            {
                log("Connection error on receiving, ensure to reconnect...");
                await EnsureReconnectAsync(token);
                return await ReceiveAsync(token);
            }
        }

        private async UniTask EnsureReconnectAsync(CancellationToken token)
        {
            if (origin.IsConnected == false) await origin.ConnectAsync(CachedAdapterUrl(), token);
        }

        private string CachedAdapterUrl()
        {
            if (cachedAdapterUrl == null)
                throw new Exception("Connection closed on receiving, no found cached adapter url");

            return cachedAdapterUrl;
        }
    }
}
