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

        private bool? previousConnected;

        public bool IsConnected
        {
            get
            {
                bool result = origin.IsConnected;

                if (previousConnected != result)
                {
                    ReportHub
                       .WithReport(ReportCategory.COMMS_SCENE_HANDLER)
                        .Log($"ArchipelagoLiveConnection connected: {result}");
                    previousConnected = result;
                }

                return result;
            }
        }

        public LogArchipelagoLiveConnection(IArchipelagoLiveConnection origin)
        {
            this.origin = origin;
        }

        public async UniTask<Result> ConnectAsync(string adapterUrl, CancellationToken token)
        {
            ReportHub
               .WithReport(ReportCategory.COMMS_SCENE_HANDLER)
                .Log($"ArchipelagoLiveConnection ConnectAsync start to: {adapterUrl}");
            var result = await origin.ConnectAsync(adapterUrl, token);
            ReportHub
               .WithReport(ReportCategory.COMMS_SCENE_HANDLER)
                .Log($"ArchipelagoLiveConnection ConnectAsync finished to: {adapterUrl} with result: {result.Success}");
            return result;
        }

        public async UniTask DisconnectAsync(CancellationToken token)
        {
            ReportHub
               .WithReport(ReportCategory.COMMS_SCENE_HANDLER)
                .Log("ArchipelagoLiveConnection DisconnectAsync start");
            await origin.DisconnectAsync(token);
            ReportHub
               .WithReport(ReportCategory.COMMS_SCENE_HANDLER)
                .Log("ArchipelagoLiveConnection DisconnectAsync finished");
        }

        public async UniTask<EnumResult<IArchipelagoLiveConnection.ResponseError>> SendAsync(MemoryWrap data, CancellationToken token)
        {
            ReportHub
               .WithReport(ReportCategory.COMMS_SCENE_HANDLER)
                .Log($"ArchipelagoLiveConnection SendAsync start with size: {data.Length} and content: {data.HexReadableString()}");
            var result = await origin.SendAsync(data, token);
            ReportHub
               .WithReport(ReportCategory.COMMS_SCENE_HANDLER)
                .Log($"ArchipelagoLiveConnection SendAsync finished with size: {data.Length} and content: {data.HexReadableString()}");
            return result;
        }

        public async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> ReceiveAsync(CancellationToken token)
        {
            ReportHub
               .WithReport(ReportCategory.COMMS_SCENE_HANDLER)
                .Log("ArchipelagoLiveConnection ReceiveAsync start");
            var result = await origin.ReceiveAsync(token);
            ReportHub
               .WithReport(ReportCategory.COMMS_SCENE_HANDLER)
                .Log($"ArchipelagoLiveConnection ReceiveAsync finished with error: {result.Error?.Message ?? "no error"}, size: {(result.Success ? result.Value.Length : 0)}");
            return result;
        }
    }
}
