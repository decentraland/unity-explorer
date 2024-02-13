using Cysharp.Threading.Tasks;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.LiveConnections
{
    public class LogArchipelagoLiveConnection : IArchipelagoLiveConnection
    {
        private readonly IArchipelagoLiveConnection origin;
        private readonly Action<string> log;

        private bool? previousConnected;

        public LogArchipelagoLiveConnection(IArchipelagoLiveConnection origin) : this(origin, Debug.Log) { }

        public LogArchipelagoLiveConnection(IArchipelagoLiveConnection origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public bool Connected()
        {
            bool result = origin.Connected();

            if (previousConnected != result)
            {
                log($"ArchipelagoLiveConnection connected: {result}");
                previousConnected = result;
            }

            return result;
        }

        public async UniTask ConnectAsync(string adapterUrl, CancellationToken token)
        {
            log($"ArchipelagoLiveConnection ConnectAsync start to: {adapterUrl}");
            await origin.ConnectAsync(adapterUrl, token);
            log($"ArchipelagoLiveConnection ConnectAsync finished to: {adapterUrl}");
        }

        public async UniTask DisconnectAsync(CancellationToken token)
        {
            log("ArchipelagoLiveConnection DisconnectAsync start");
            await origin.DisconnectAsync(token);
            log("ArchipelagoLiveConnection DisconnectAsync finished");
        }

        public async UniTask SendAsync(MemoryWrap data, CancellationToken token)
        {
            log($"ArchipelagoLiveConnection SendAsync start with size: {data.Length}");
            await origin.SendAsync(data, token);
            log($"ArchipelagoLiveConnection SendAsync finished with size: {data.Length}");
        }

        public async UniTask<MemoryWrap> ReceiveAsync(CancellationToken token)
        {
            log("ArchipelagoLiveConnection ReceiveAsync start");
            var result = await origin.ReceiveAsync(token);
            log($"ArchipelagoLiveConnection ReceiveAsync finished with size: {result.Length}");
            return result;
        }
    }
}
