using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;
using Utility.Types;

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
            m => ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, m)
        ) { }

        public AutoReconnectLiveConnection(IArchipelagoLiveConnection origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public UniTask<Result> ConnectAsync(string adapterUrl, CancellationToken token)
        {
            cachedAdapterUrl = adapterUrl;
            return origin.ConnectAsync(adapterUrl, token);
        }

        public UniTask DisconnectAsync(CancellationToken token)
        {
            cachedAdapterUrl = null;
            return origin.DisconnectAsync(token);
        }

        public async UniTask<EnumResult<IArchipelagoLiveConnection.ResponseError>> SendAsync(MemoryWrap data, CancellationToken token)
        {
            var result = await origin.SendAsync(data, token);

            if (result.Error?.State is IArchipelagoLiveConnection.ResponseError.ConnectionClosed)
            {
                log("Connection error on sending, ensure to reconnect...");
                await EnsureReconnectAsync(token);
                return await SendAsync(data, token);
            }

            return result;
        }

        public async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> ReceiveAsync(CancellationToken token)
        {
            var result = await origin.ReceiveAsync(token);

            if (result.Error?.State is IArchipelagoLiveConnection.ResponseError.ConnectionClosed)
            {
                log("Connection error on receiving, ensure to reconnect...");
                await EnsureReconnectAsync(token);
                return await ReceiveAsync(token);
            }

            return result;
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
