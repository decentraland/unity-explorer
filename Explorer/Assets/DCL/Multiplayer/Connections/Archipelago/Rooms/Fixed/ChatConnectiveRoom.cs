using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Credentials;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.WebRequests;
using LiveKit.Internal;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Info;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Participants.Factory;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks.Factory;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms.Chat
{
    public class ChatConnectiveRoom : IActivatableConnectiveRoom
    {
        private static readonly TimeSpan HEARTBEATS_INTERVAL = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan CONNECTION_LOOP_RECOVER_INTERVAL = TimeSpan.FromSeconds(5);

        private readonly IWebRequestController webRequests;
        private readonly URLAddress adapterAddress;
        private readonly string logPrefix;
        private readonly Atomic<IConnectiveRoom.ConnectionLoopHealth> connectionLoopHealth = new (IConnectiveRoom.ConnectionLoopHealth.Stopped);
        private readonly Atomic<AttemptToConnectState> attemptToConnectState = new (AttemptToConnectState.None);
        private readonly Atomic<IConnectiveRoom.State> roomState = new (IConnectiveRoom.State.Stopped);
        private CancellationTokenSource? cancellationTokenSource;
        private readonly InteriorRoom room = new ();
        private readonly ObjectPool<IRoom> roomPool = new (
            createFunc: () => new LogRoom(new Room(
                new ArrayMemoryPool(),
                new DefaultActiveSpeakers(),
                new ParticipantsHub(),
                new TracksFactory(),
                new FfiHandleFactory(),
                new ParticipantFactory(),
                new TrackPublicationFactory(),
                new DataPipe(),
                new MemoryRoomInfo())),
            actionOnGet: null,
            actionOnRelease: room => room.DisconnectAsync(CancellationToken.None),
            actionOnDestroy: null,
            defaultCapacity: 10,
            maxSize: 100
        );

        public ChatConnectiveRoom(IWebRequestController webRequests, URLAddress adapterAddress)
        {
            this.webRequests = webRequests;
            this.adapterAddress = adapterAddress;
            logPrefix = GetType().Name;
        }

        public IConnectiveRoom.State CurrentState() => roomState.Value();
        public IRoom Room() => room;
        public AttemptToConnectState AttemptToConnectState => attemptToConnectState.Value();
        public IConnectiveRoom.ConnectionLoopHealth CurrentConnectionLoopHealth => connectionLoopHealth.Value();

        protected UniTask PrewarmAsync(CancellationToken token) =>
            UniTask.CompletedTask;

        protected async UniTask CycleStepAsync(CancellationToken token)
        {
            if (CurrentState() is not IConnectiveRoom.State.Running)
            {
                string connectionString = await ConnectionStringAsync(token);
                await TryConnectToRoomAsync(connectionString, token);
            }
        }

        private async UniTask<string> ConnectionStringAsync(CancellationToken token)
        {
            string metadata = FixedMetadata.Default.ToJson();
            var result = webRequests.SignedFetchGetAsync(adapterAddress, metadata, token);
            AdapterResponse response = await result.CreateFromJson<AdapterResponse>(WRJsonParser.Unity);
            string connectionString = response.adapter;
            ReportHub.WithReport(ReportCategory.COMMS_CHAT_HANDLER).Log($"String is: {connectionString}");
            return connectionString;
        }

        private async UniTask<bool> TryConnectToRoomAsync(string connectionString, CancellationToken token)
        {
            ReportHub.Log(ReportCategory.CHAT_CONVERSATIONS, $"{logPrefix} - Trying to connect to room started: {connectionString}");

            var credentials = new ConnectionStringCredentials(connectionString);

            bool connectResult = await ConnectToRoomAsync(credentials, token);

            AttemptToConnectState connectionState = connectResult ? AttemptToConnectState.Success : AttemptToConnectState.Error;
            attemptToConnectState.Set(connectionState);

            if (connectResult == false)
            {
                ReportHub.LogWarning(ReportCategory.CHAT_CONVERSATIONS, $"{logPrefix} - Cannot connect to room with url: {credentials.Url} with token: {credentials.AuthToken}");
                return false;
            }

            roomState.Set(IConnectiveRoom.State.Running);
            ReportHub.Log(ReportCategory.CHAT_CONVERSATIONS, $"{logPrefix} - Trying to connect to new room finished successfully {connectionString}");

            return true;
        }

        private async UniTask<bool> ConnectToRoomAsync<T>(T credentials, CancellationToken ct)
            where T: ICredentials
        {
            try
            {
                IRoom newRoom = roomPool.Get();
                bool connectResult = await newRoom.ConnectAsync(credentials.Url, credentials.AuthToken, ct, true);

                if (!connectResult)
                {
                    roomPool.Release(newRoom);
                    return false;
                }

                await room.SwapRoomsAsync(RoomSelection.NEW, room.assigned, newRoom, roomPool, ct);
                return true;
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS, $"{logPrefix} - Connection failed: {e}");
                return false;
            }
        }

        [Serializable]
        private struct FixedMetadata
        {
            public static FixedMetadata Default = new ()
            {
                signer = "dcl:explorer",
            };

            public string signer;

            public string ToJson() =>
                JsonUtility.ToJson(this)!;
        }

        [Serializable]
        private struct AdapterResponse
        {
            public string adapter;
        }

        public bool Activated { get; private set; }

        public async UniTask ActivateAsync()
        {
            if (Activated)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} is already activated");
                return;
            }

            ReportHub.Log(ReportCategory.CHAT_CONVERSATIONS, $"{logPrefix} - ActivateAsync");

            Activated = true;
            await this.StartIfNotAsync();
        }

        public async UniTask DeactivateAsync()
        {
            if (!Activated)
            {
                ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} is already deactivated");
                return;
            }

            ReportHub.Log(ReportCategory.CHAT_CONVERSATIONS, $"{logPrefix} - DeactivateAsync");

            Activated = false;
            await this.StopIfNotAsync();
        }

        public void Dispose()
        {
            roomPool.Dispose();
            cancellationTokenSource.SafeCancelAndDispose();
            cancellationTokenSource = null;
        }

        public async UniTask<bool> StartAsync()
        {
            if (CurrentState() is not IConnectiveRoom.State.Stopped)
                throw new InvalidOperationException("Room is already running");

            ReportHub.Log(ReportCategory.CHAT_CONVERSATIONS, $"{logPrefix} - StartAsync");

            attemptToConnectState.Set(AttemptToConnectState.None);
            roomState.Set(IConnectiveRoom.State.Starting);
            RunAsync((cancellationTokenSource = new CancellationTokenSource()).Token).Forget();
            await UniTask.WaitWhile(() => attemptToConnectState.Value() is AttemptToConnectState.None);
            return attemptToConnectState.Value() is not AttemptToConnectState.Error;
        }

        public virtual async UniTask StopAsync()
        {
            if (CurrentState() is IConnectiveRoom.State.Stopped or IConnectiveRoom.State.Stopping)
                throw new InvalidOperationException("Room is already stopped");

            ReportHub.Log(ReportCategory.CHAT_CONVERSATIONS, $"{logPrefix} - StopAsync");

            roomState.Set(IConnectiveRoom.State.Stopping);
            await room.ResetRoom(roomPool, CancellationToken.None);
            roomState.Set(IConnectiveRoom.State.Stopped);
            cancellationTokenSource.SafeCancelAndDispose();
            cancellationTokenSource = null;
        }

        private async UniTaskVoid RunAsync(CancellationToken token)
        {
            roomState.Set(IConnectiveRoom.State.Starting);

            await ExecuteWithRecoveryAsync(PrewarmAsync, nameof(PrewarmAsync), IConnectiveRoom.ConnectionLoopHealth.Prewarming, IConnectiveRoom.ConnectionLoopHealth.PrewarmFailed, token);

            while (token.IsCancellationRequested == false)
            {
                await ExecuteWithRecoveryAsync(CycleStepAsync, nameof(CycleStepAsync), IConnectiveRoom.ConnectionLoopHealth.Running, IConnectiveRoom.ConnectionLoopHealth.CycleFailed, token);
                await UniTask.Delay(HEARTBEATS_INTERVAL, cancellationToken: token);
            }

            connectionLoopHealth.Set(IConnectiveRoom.ConnectionLoopHealth.Stopped);
        }

        private async UniTask ExecuteWithRecoveryAsync(Func<CancellationToken, UniTask> func, string funcName, IConnectiveRoom.ConnectionLoopHealth enterState, IConnectiveRoom.ConnectionLoopHealth stateOnException, CancellationToken ct)
        {
            do
            {
                try
                {
                    connectionLoopHealth.Set(enterState);
                    await func(ct);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ReportHub.LogError(ReportCategory.LIVEKIT, $"{logPrefix} - {funcName} failed: {e}");
                    connectionLoopHealth.Set(stateOnException);
                    await RecoveryDelayAsync(ct);
                }
            }
            while (!ct.IsCancellationRequested && connectionLoopHealth.Value() == stateOnException);
        }

        private UniTask RecoveryDelayAsync(CancellationToken ct) => UniTask.Delay(CONNECTION_LOOP_RECOVER_INTERVAL, cancellationToken: ct);
    }
}
