using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using LiveKit.Internal.FFIClients.Pools.Memory;
using Org.BouncyCastle.Utilities;
using System;
using System.Threading;
using Utility.Types;

namespace DCL.Multiplayer.Connections.Archipelago.LiveConnections
{
    /// <summary>
    ///     AutoReconnection will try to recover connection to the transport infinitely until it's cancelled
    /// </summary>
    public class AutoReconnectLiveConnection : IArchipelagoLiveConnection
    {
        private static readonly TimeSpan DEFAULT_RECOVERY_DELAY = TimeSpan.FromSeconds(5);

        private readonly TimeSpan recoveryDelay;

        private readonly IArchipelagoLiveConnection origin;
        private string? cachedAdapterUrl;

        private DateTime lastRecoveryAttempt = DateTime.MinValue;

        public bool IsConnected => origin.IsConnected;

        public AutoReconnectLiveConnection(IArchipelagoLiveConnection origin, TimeSpan recoveryDelay)
        {
            this.origin = origin;
            this.recoveryDelay = recoveryDelay;
        }

        public AutoReconnectLiveConnection(IArchipelagoLiveConnection origin) : this(origin, DEFAULT_RECOVERY_DELAY) { }

        public UniTask<Result> ConnectAsync(string adapterUrl, CancellationToken token)
        {
            cachedAdapterUrl = adapterUrl;
            return EnsureConnectionAsync(token);
        }

        public UniTask<Result> DisconnectAsync(CancellationToken token)
        {
            cachedAdapterUrl = null;
            return origin.DisconnectAsync(token);
        }

        public async UniTask<EnumResult<IArchipelagoLiveConnection.ResponseError>> SendAsync(MemoryWrap data, CancellationToken token)
        {
            EnumResult<IArchipelagoLiveConnection.ResponseError> result = await origin.SendAsync(data, token);

            if (result.Error?.State is IArchipelagoLiveConnection.ResponseError.ConnectionClosed)
            {
                ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, "Connection error on sending, ensure to reconnect...");
                Result connectionResult = await EnsureConnectionAsync(token);

                if (!connectionResult.Success)
                    return EnumResult<IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.ConnectionClosed, connectionResult.ErrorMessage!);

                return await SendAsync(data, token);
            }

            return result;
        }

        public async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> ReceiveAsync(CancellationToken token)
        {
            EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> result = await origin.ReceiveAsync(token);

            if (result.Error?.State is IArchipelagoLiveConnection.ResponseError.ConnectionClosed)
            {
                ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, "Connection error on receiving, ensure to reconnect...");

                Result connectionResult = await EnsureConnectionAsync(token);

                if (!connectionResult.Success)
                    return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.ConnectionClosed, connectionResult.ErrorMessage!);

                return await ReceiveAsync(token);
            }

            return result;
        }

        private async UniTask<Result> EnsureConnectionAsync(CancellationToken token)
        {
            var attemptNumber = 1;

            if (origin.IsConnected) return Result.SuccessResult();

            var result = Result.ErrorResult("Not Started");

            while (!origin.IsConnected)
            {
                if (token.IsCancellationRequested)
                    return Result.CancelledResult();

                if (cachedAdapterUrl == null)
                {
                    // Wait for the adapter URL to be set
                    await UniTask.Yield();
                    continue;
                }

                await DelayRecoveryAsync(token);

                string adapter = cachedAdapterUrl!;
                result = await origin.ConnectAsync(adapter, token);

                if (!result.Success) { ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, $"Cannot ensure connection to {adapter} after {attemptNumber} attempts: {result.ErrorMessage}"); }

                attemptNumber++;
                lastRecoveryAttempt = DateTime.Now;
            }

            return result;

            UniTask DelayRecoveryAsync(CancellationToken ct)
            {
                TimeSpan delay = recoveryDelay - (DateTime.Now - lastRecoveryAttempt);
                return delay.TotalMilliseconds > 0 ? UniTask.Delay(delay, cancellationToken: ct) : UniTask.CompletedTask;
            }
        }
    }
}
