using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Typing;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;
using Utility.Types;

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

        public async UniTask<Result> ConnectAsync(string adapterUrl, CancellationToken token)
        {
            log($"ArchipelagoLiveConnection ConnectAsync start to: {adapterUrl}");
            var result = await origin.ConnectAsync(adapterUrl, token);
            log($"ArchipelagoLiveConnection ConnectAsync finished to: {adapterUrl} with result: {result.Success}");
            return result;
        }

        public async UniTask DisconnectAsync(CancellationToken token)
        {
            log("ArchipelagoLiveConnection DisconnectAsync start");
            await origin.DisconnectAsync(token);
            log("ArchipelagoLiveConnection DisconnectAsync finished");
        }

        public async UniTask<EnumResult<IArchipelagoLiveConnection.ResponseError>> SendAsync(MemoryWrap data, CancellationToken token)
        {
            log($"ArchipelagoLiveConnection SendAsync start with size: {data.Length} and content: {data.HexReadableString()}");
            var result = await origin.SendAsync(data, token);
            log($"ArchipelagoLiveConnection SendAsync finished with size: {data.Length} and content: {data.HexReadableString()}");
            return result;
        }

        public async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> ReceiveAsync(CancellationToken token)
        {
            log("ArchipelagoLiveConnection ReceiveAsync start");
            var result = await origin.ReceiveAsync(token);
            log($"ArchipelagoLiveConnection ReceiveAsync finished with error: {result.Error?.Message ?? "no error"}, size: {(result.Success ? result.Value.Length : 0)}");
            return result;
        }
    }
}
