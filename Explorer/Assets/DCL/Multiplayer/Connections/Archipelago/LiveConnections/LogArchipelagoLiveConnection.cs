using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Typing;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.LiveConnections
{
    public class LogArchipelagoLiveConnection : IArchipelagoLiveConnection
    {
        private readonly IArchipelagoLiveConnection origin;
        private readonly Action<string> log;

        private bool? previousConnected;

        public bool IsConnected
        {
            get
            {
                bool result = origin.IsConnected;

                if (previousConnected != result)
                {
                    log($"ArchipelagoLiveConnection connected: {result}");
                    previousConnected = result;
                }

                return result;
            }
        }

        public LogArchipelagoLiveConnection(IArchipelagoLiveConnection origin) : this(origin, ReportHub.WithReport(ReportCategory.ARCHIPELAGO_REQUEST).Log) { }

        public LogArchipelagoLiveConnection(IArchipelagoLiveConnection origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
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
            log($"ArchipelagoLiveConnection SendAsync start with size: {data.Length} and content: {data.HexReadableString()}");
            await origin.SendAsync(data, token);
            log($"ArchipelagoLiveConnection SendAsync finished with size: {data.Length} and content: {data.HexReadableString()}");
        }

        public async UniTask<MemoryWrap> ReceiveAsync(CancellationToken token)
        {
            log("ArchipelagoLiveConnection ReceiveAsync start");
            MemoryWrap result = await origin.ReceiveAsync(token);
            log($"ArchipelagoLiveConnection ReceiveAsync finished with size: {result.Length}");
            return result;
        }
    }
}
