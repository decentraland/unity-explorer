using Cysharp.Threading.Tasks;
using DCL.Character;
using DCL.Diagnostics;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.Archipelago.AdapterAddress.Current;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Archipelago.SignFlow;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Typing;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Buffers;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public class ArchipelagoIslandRoom : ConnectiveRoom
    {
        private const int MAX_RECONNECT_ATTEMPTS_BEFORE_FRESH_HANDSHAKE = 3;
        private static readonly TimeSpan RECONNECT_BACKOFF = TimeSpan.FromSeconds(5);

        private readonly IArchipelagoSignFlow signFlow;
        private readonly ICharacterObject characterObject;

        private readonly ICurrentAdapterAddress currentAdapterAddress;

        private readonly Mutex<ConnectionStringState> connectionState = new (ConnectionStringState.None);

        private int consecutiveConnectFailures;
        private DateTime nextReconnectAttemptUtc = DateTime.MinValue;

        public ArchipelagoIslandRoom(ICharacterObject characterObject, IWeb3IdentityCache web3IdentityCache,
            IMultiPool multiPool, IMemoryPool memoryPool, ICurrentAdapterAddress currentAdapterAddress) : this(

            // TODO Validate the following assumption
            // We cannot use ArrayPool<byte>.Shared since some operations might not be thread safe (like the handshake)
            // producing unexpected errors when sending the data through the websocket
            new LiveConnectionArchipelagoSignFlow(
                new ArchipelagoSignedConnection(new WebSocketArchipelagoLiveConnection(memoryPool), multiPool, memoryPool, web3IdentityCache)
                   .WithLog(), memoryPool, multiPool).WithLog(), characterObject, currentAdapterAddress) { }

        public ArchipelagoIslandRoom(
            IArchipelagoSignFlow signFlow,
            ICharacterObject characterObject,
            ICurrentAdapterAddress currentAdapterAddress
        ) : base()
        {
            this.signFlow = signFlow;
            this.characterObject = characterObject;
            this.currentAdapterAddress = currentAdapterAddress;
        }

        protected override async UniTask PrewarmAsync(CancellationToken token)
        {
            // Reset the per-session state: the room instance is reused across StopAsync/StartAsync (teleport, logout)
            ResetConnectionState();

            consecutiveConnectFailures = 0;
            nextReconnectAttemptUtc = DateTime.MinValue;

            await ConnectToArchipelagoAsync(token);
            signFlow.StartListeningForConnectionStringAsync(OnNewConnectionString, token).Forget();
        }

        protected override async UniTask CycleStepAsync(CancellationToken token)
        {
            await TryConnectIfNeededAsync(token);

            if (token.IsCancellationRequested) return;

            await UniTask.SwitchToMainThread(token);
            Vector3 position = characterObject.Position;
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync();

            var result = await signFlow.SendHeartbeatAsync(position, token);

            if (result.Success == false)
                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER, $"Cannot send heartbeat, connection is closed: {result.ErrorMessage}");
        }

        private void OnNewConnectionString(string connectionString)
        {
            using var guard = connectionState.Lock();
            guard.Value = ConnectionStringState.NewPending(connectionString);
        }

        private void ResetConnectionState()
        {
            using var guard = connectionState.Lock();
            guard.Value = ConnectionStringState.None;
        }

        // Reads the state and consumes it (NewPending -> Current) atomically so a pushed string is acted on once.
        private ConnectionStringState ReadAndConsumeConnectionState()
        {
            using var guard = connectionState.Lock();
            ConnectionStringState state = guard.Value;
            guard.Value = state.Consume();
            return state;
        }

        private async UniTask TryConnectIfNeededAsync(CancellationToken token)
        {
            ConnectionStringState state = ReadAndConsumeConnectionState();

            if (state.State == ConnectionStringState.Kind.NONE) return;

            string connectionString = state.ConnectionString!;
            bool isNewString = state.State == ConnectionStringState.Kind.NEW_PENDING;

            if (CurrentState() is not (IConnectiveRoom.State.Starting or IConnectiveRoom.State.Running)) return;

            bool roomIsDisconnected = Room().Info.ConnectionState != LKConnectionState.ConnConnected;

            if (!ShouldAttemptConnection(isNewString, roomIsDisconnected, DateTime.UtcNow, nextReconnectAttemptUtc))
                return;

            await TryConnectToRoomAsync(connectionString, token);

            if (token.IsCancellationRequested) return;

            if (Room().Info.ConnectionState == LKConnectionState.ConnConnected)
            {
                consecutiveConnectFailures = 0;
                nextReconnectAttemptUtc = DateTime.MinValue;
                return;
            }

            consecutiveConnectFailures++;
            nextReconnectAttemptUtc = DateTime.UtcNow + RECONNECT_BACKOFF;

            if (ShouldForceFreshHandshake(consecutiveConnectFailures))
            {
                ReportHub.LogWarning(ReportCategory.COMMS_SCENE_HANDLER,
                    $"Island room connection failed {consecutiveConnectFailures} times with the cached connection string, forcing a fresh archipelago handshake");

                consecutiveConnectFailures = 0;
                await ForceFreshIslandAssignmentAsync(token);
            }
        }

        internal static bool ShouldAttemptConnection(bool isNewString, bool roomIsDisconnected, DateTime nowUtc, DateTime nextAttemptUtc) =>
            isNewString || (roomIsDisconnected && nowUtc >= nextAttemptUtc);

        internal static bool ShouldForceFreshHandshake(int consecutiveFailures) =>
            consecutiveFailures >= MAX_RECONNECT_ATTEMPTS_BEFORE_FRESH_HANDSHAKE;

        /// <summary>
        ///     The cached connection string keeps being rejected (e.g. its token expired during a long outage):
        ///     re-handshaking with archipelago creates a new peer session, which makes the server push a fresh
        ///     <c>IslandChangedMessage</c> after the next heartbeat.
        /// </summary>
        private async UniTask ForceFreshIslandAssignmentAsync(CancellationToken token)
        {
            ResetConnectionState();

            await signFlow.DisconnectAsync(token);

            if (token.IsCancellationRequested) return;

            await ConnectToArchipelagoAsync(token);
        }

        private async UniTask ConnectToArchipelagoAsync(CancellationToken token)
        {
            string adapterUrl = currentAdapterAddress.AdapterUrl();
            Result welcomePeerId = await signFlow.ConnectAsync(adapterUrl, token);
            welcomePeerId.EnsureSuccess("Cannot authorize with current address and signature, peer id is invalid");
        }

        internal readonly struct ConnectionStringState
        {
            public enum Kind
            {
                NONE,
                NEW_PENDING,
                CURRENT,
            }

            public readonly Kind State;
            public readonly string? ConnectionString;

            private ConnectionStringState(Kind state, string? connectionString)
            {
                State = state;
                ConnectionString = connectionString;
            }

            public static ConnectionStringState None => new (Kind.NONE, null);

            public static ConnectionStringState NewPending(string connectionString) =>
                new (Kind.NEW_PENDING, connectionString);

            /// <summary>A NEW_PENDING string becomes CURRENT once read by the cycle loop; other states are unchanged.</summary>
            public ConnectionStringState Consume() =>
                State == Kind.NEW_PENDING ? new ConnectionStringState(Kind.CURRENT, ConnectionString) : this;
        }
    }
}
