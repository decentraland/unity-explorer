using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Pools;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using Decentraland.Common;
using Decentraland.Kernel.Comms.V3;
using LiveKit.client_sdk_unity.Runtime.Scripts.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;
using Utility.Multithreading;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Multiplayer.Connections.Archipelago.SignFlow
{
    /// <summary>
    ///     Runs heavy operations in a thread pool and uses a live connection to communicate with the server.
    /// </summary>
    public class LiveConnectionArchipelagoSignFlow : IArchipelagoSignFlow
    {
        private readonly IArchipelagoLiveConnection connection;
        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;
        private readonly SessionControl? session;
        private int listenerGeneration;

        /// <summary>Uses the connection's automatic transport recovery within the current logical session.</summary>
        public LiveConnectionArchipelagoSignFlow(IArchipelagoLiveConnection connection, IMemoryPool memoryPool, IMultiPool multiPool, SessionControl? session = null)
        {
            this.session = session;
            this.connection = connection;
            this.memoryPool = memoryPool;
            this.multiPool = multiPool;
        }

        /// <summary>
        ///     A dumb transport: it always builds and sends the packet it is asked for, so a success result
        ///     always means the position really reached archipelago. Whether a heartbeat is wanted at all is the
        ///     <c>archipelago-heartbeats</c> kill switch's decision, taken once in
        ///     <c>ArchipelagoIslandRoom.SendHeartbeatIfEnabledAsync</c>.
        /// </summary>
        public async UniTask<Result> SendHeartbeatAsync(Vector3 playerPosition, CancellationToken token)
        {
            try
            {
                using SmartWrap<Position> position = multiPool.TempResource<Position>();
                position.value.X = playerPosition.x;
                position.value.Y = playerPosition.y;
                position.value.Z = playerPosition.z;

                using SmartWrap<Heartbeat> heartbeat = multiPool.TempResource<Heartbeat>();
                heartbeat.value.Position = position.value;

                using SmartWrap<ClientPacket> clientPacket = multiPool.TempResource<ClientPacket>();
                clientPacket.value.ClearMessage();
                clientPacket.value.Heartbeat = heartbeat.value;

                var result = await connection.SendAsync(clientPacket.value, memoryPool, token);

                return result.Success == false
                    ? Result.ErrorResult($"Cannot send heartbeat for position {playerPosition}: {result.Error!.Value.Message}")
                    : Result.SuccessResult();
            }
            // It seems to be not required
            catch (Exception e)
            {
                return Result.ErrorResult($"Cannot send heartbeat for position {playerPosition}: {e}");
            }
        }

        /// <summary>
        ///     This loop is launched once and should be free from exceptions
        /// </summary>
        public async UniTaskVoid StartListeningForConnectionStringAsync(Action<string, string> onNewIslandAssignment, CancellationToken token)
        {
            int listener = DCLInterlocked.Increment(ref listenerGeneration);
            int generation = session?.Generation ?? 0;
            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
            CancellationToken listenerToken = lifetime.Token;
            void Suppress()
            {
                if (session?.CanListen(generation) ?? true) return;
                try { lifetime.Cancel(); }
                catch (ObjectDisposedException) { }
            }
            if (session != null) session.Changed += Suppress;
            try
            {
                await ExecuteOnThreadPoolScope.NewScopeAsync();

                while (!listenerToken.IsCancellationRequested && listener == DCLVolatile.Read(ref listenerGeneration) && (session?.CanListen(generation) ?? true))
                {
                    EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> result = await connection.ReceiveAsync(listenerToken);

                    if (result.Success == false)
                    {
                        // AutoReconnectLiveConnection will recover the transport itself
                        if (listenerToken.IsCancellationRequested == false)
                            ReportHub.LogError(ReportCategory.LIVEKIT, $"Cannot listen for connection string: {result.Error?.Message}");
                        await UniTask.Delay(250, cancellationToken: listenerToken);
                        continue;
                    }

                    using MemoryWrap response = result.Value;
                    using var serverPacket = new SmartWrap<ServerPacket>(response.AsMessageServerPacket(), multiPool);

                    if (listenerToken.IsCancellationRequested || listener != DCLVolatile.Read(ref listenerGeneration) || !(session?.CanListen(generation) ?? true)) return;

                    if (serverPacket.value.MessageCase == ServerPacket.MessageOneofCase.Kicked)
                    {
                        session?.Stop(generation, serverPacket.value.Kicked.Reason switch
                        {
                            KickedReason.KrNewSession => SessionControl.Status.Superseded,
                            KickedReason.KrBanned => SessionControl.Status.Banned,
                            _ => SessionControl.Status.Unknown,
                        });
                        return;
                    }

                    if (serverPacket.value.MessageCase == ServerPacket.MessageOneofCase.SessionStatus)
                    {
                        SessionStatusMessage status = serverPacket.value.SessionStatus;
                        if (status.State == SessionStatus.TakeoverPending) session?.Pending(generation, status.RetryAfterMs, DateTime.UtcNow);
                        else session?.Stop(generation, status.State == SessionStatus.TakeoverFailed ? SessionControl.Status.Failed : SessionControl.Status.Unknown);
                        continue;
                    }

                    if (serverPacket.value.MessageCase is ServerPacket.MessageOneofCase.IslandChanged)
                    {
                        using var islandChanged = new SmartWrap<IslandChangedMessage>(serverPacket.value.IslandChanged!, multiPool);
                        if (string.IsNullOrWhiteSpace(islandChanged.value.IslandId) || string.IsNullOrWhiteSpace(islandChanged.value.ConnStr))
                        {
                            session?.Stop(generation, SessionControl.Status.Unknown);
                            return;
                        }
                        if (session?.AcceptAssignment(generation) ?? true)
                            onNewIslandAssignment(islandChanged.value.IslandId, islandChanged.value.ConnStr);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                session?.Stop(generation, SessionControl.Status.Unknown);
                ReportHub.LogException(e, ReportCategory.LIVEKIT);
            }
            finally
            {
                if (session != null) session.Changed -= Suppress;
                lifetime.Dispose();
                if (session != null && generation == session.Generation && !session.CanListen(generation))
                {
                    try { await connection.DisconnectAsync(CancellationToken.None); }
                    catch (Exception e) { ReportHub.LogException(e, ReportCategory.LIVEKIT); }
                }
            }
        }

        public UniTask DisconnectAsync(CancellationToken token) =>
            connection.DisconnectAsync(token);

        public async UniTask<Result> ConnectAsync(string adapterUrl, CancellationToken token)
        {
            int generation = session?.Generation ?? 0;
            Result result = await connection.ConnectAsync(adapterUrl, token);
            if (result.Success) session?.AwaitAssignment(generation, DateTime.UtcNow);
            return result;
        }
    }
}
