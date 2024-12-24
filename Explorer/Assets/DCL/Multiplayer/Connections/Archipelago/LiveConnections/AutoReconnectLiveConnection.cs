using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using LiveKit.Internal.FFIClients.Pools.Memory;
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

        private readonly SemaphoreSlim semaphore = new (1, 1);

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
            while (true)
            {
                EnumResult<IArchipelagoLiveConnection.ResponseError> result = await origin.SendAsync(data, token);

                if (result.Error?.State is not IArchipelagoLiveConnection.ResponseError.ConnectionClosed)
                    return result;

                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, "Connection error on sending, ensure to reconnect...\n" + result.Error.Value.Message);
                Result connectionResult = await EnsureConnectionAsync(token);

                if (!connectionResult.Success)
                    return EnumResult<IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.ConnectionClosed, connectionResult.ErrorMessage!);
            }
        }

        public async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> ReceiveAsync(CancellationToken token)
        {
            while (true)
            {
                EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> result = await origin.ReceiveAsync(token);

                if (result.Error?.State is not IArchipelagoLiveConnection.ResponseError.ConnectionClosed)
                    return result;

                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, "Connection error on receiving, ensure to reconnect...\n" + result.Error.Value.Message);

                Result connectionResult = await EnsureConnectionAsync(token);

                if (!connectionResult.Success)
                    return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.ConnectionClosed, connectionResult.ErrorMessage!);
            }
        }

        private async UniTask<Result> EnsureConnectionAsync(CancellationToken token)
        {
            // Thus function must be entered only once, other calls should be waiting
            // Otherwise there is a race condition
            Result result = (await semaphore.WaitAsync(token).SuppressToResultAsync()).AsResult();

            if (!result.Success)
                return result;

            try
            {
                var attemptNumber = 1;

                if (origin.IsConnected) return Result.SuccessResult();

                result = Result.ErrorResult("Not Started");

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

                    if (!result.Success)
                        ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, $"Cannot ensure connection to {adapter} after {attemptNumber} attempts: {result.ErrorMessage}");

                    attemptNumber++;
                    lastRecoveryAttempt = DateTime.Now;
                }

                return result;
            }
            finally { semaphore.Release(); }

            UniTask DelayRecoveryAsync(CancellationToken ct)
            {
                TimeSpan delay = recoveryDelay - (DateTime.Now - lastRecoveryAttempt);
                return delay.TotalMilliseconds > 0 ? UniTask.Delay(delay, cancellationToken: ct) : UniTask.CompletedTask;
            }
        }
    }
}
