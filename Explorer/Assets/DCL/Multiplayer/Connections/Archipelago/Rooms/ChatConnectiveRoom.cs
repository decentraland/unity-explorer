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
        private readonly IWebRequestController webRequests;
        private readonly URLAddress adapterAddress;

        public bool Activated { get; private set; }


        public ChatConnectiveRoom(IWebRequestController webRequests, URLAddress adapterAddress)
        {
            this.webRequests = webRequests;
            this.adapterAddress = adapterAddress;
            this.logPrefix = GetType().Name;

            roomInstance = new LogRoom(
                new Room(
                    new ArrayMemoryPool(),
                    new DefaultActiveSpeakers(),
                    new ParticipantsHub(),
                    new TracksFactory(),
                    new FfiHandleFactory(),
                    new ParticipantFactory(),
                    new TrackPublicationFactory(),
                    new DataPipe(),
                    new MemoryRoomInfo()
                )
            );
                    }

        protected async UniTask CycleStepAsync(CancellationToken ct)
        {
            if (CurrentState() is not IConnectiveRoom.State.Running)
            {
                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                    $"[ChatConnectiveRoom] Current state is not Running: {CurrentState()}");

                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                    $"[ChatConnectiveRoom] Starting disconnect");
                await room.DisconnectAsync(ct);

                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                    $"[ChatConnectiveRoom] Getting connection string");
                string connectionString = await ConnectionStringAsync(ct);

                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                    $"[ChatConnectiveRoom] Attempting connection with string: {connectionString}");
                await TryConnectToRoomAsync(connectionString, ct);

                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                    $"[ChatConnectiveRoom] Connection attempt completed");
            }
        }

        private async UniTask<string> ConnectionStringAsync(CancellationToken ct)
        {
            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                $"[ChatConnectiveRoom] Starting to fetch connection string");

            string metadata = FixedMetadata.Default.ToJson();
            var result = webRequests.SignedFetchGetAsync(adapterAddress, metadata, ct);
            AdapterResponse response = await result.CreateFromJson<AdapterResponse>(WRJsonParser.Unity);
            string connectionString = response.adapter;

            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                $"[ChatConnectiveRoom] Received connection string: {connectionString}");

            return connectionString;
        }


        public async UniTask ActivateAsync()
        {
            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                $"[ChatConnectiveRoom] Starting activation. Current state: {Activated}");

            if (Activated)
            {
                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                    $"[ChatConnectiveRoom] Already activated");
                return;
            }

            Activated = true;
            await this.StartIfNotAsync();

            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                $"[ChatConnectiveRoom] Activation completed");
        }

        public async UniTask DeactivateAsync()
        {
            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                $"[ChatConnectiveRoom] Starting deactivation. Current state: {Activated}");

            if (!Activated)
            {
                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                    $"[ChatConnectiveRoom] Already deactivated");
                return;
            }

            Activated = false;
            await this.StopIfNotAsync();

            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                $"[ChatConnectiveRoom] Deactivation completed");
        }

        private static readonly TimeSpan HEARTBEATS_INTERVAL = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan CONNECTION_LOOP_RECOVER_INTERVAL = TimeSpan.FromSeconds(5);
        internal readonly string logPrefix;

        private readonly InteriorRoom room = new ();

        private readonly Atomic<IConnectiveRoom.ConnectionLoopHealth> connectionLoopHealth = new (IConnectiveRoom.ConnectionLoopHealth.Stopped);

        private readonly Atomic<AttemptToConnectState> attemptToConnectState = new (AttemptToConnectState.None);

        private readonly Atomic<IConnectiveRoom.State> roomState = new (IConnectiveRoom.State.Stopped);

        private IRoom roomInstance;

        private readonly IObjectPool<IRoom> roomPool = new ObjectPool<IRoom>(
            () => new LogRoom(
                new Room(
                    new ArrayMemoryPool(),
                    new DefaultActiveSpeakers(),
                    new ParticipantsHub(),
                    new TracksFactory(),
                    new FfiHandleFactory(),
                    new ParticipantFactory(),
                    new TrackPublicationFactory(),
                    new DataPipe(),
                    new MemoryRoomInfo()
                )
            )
        );

        private CancellationTokenSource? cancellationTokenSource;

        public IConnectiveRoom.ConnectionLoopHealth CurrentConnectionLoopHealth => connectionLoopHealth.Value();

        public void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
            cancellationTokenSource = null;
        }

        public async UniTask<bool> StartAsync()
        {
            if (CurrentState() is not IConnectiveRoom.State.Stopped)
                throw new InvalidOperationException("Room is already running");

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

            roomState.Set(IConnectiveRoom.State.Stopping);
            await room.ResetRoom(roomPool, CancellationToken.None);
            roomState.Set(IConnectiveRoom.State.Stopped);
            cancellationTokenSource.SafeCancelAndDispose();
            cancellationTokenSource = null;
        }

        public IConnectiveRoom.State CurrentState() =>
            roomState.Value();

        public AttemptToConnectState AttemptToConnectState => attemptToConnectState.Value();

        public IRoom Room() =>
            room;

        private async UniTaskVoid RunAsync(CancellationToken token)
        {
            roomState.Set(IConnectiveRoom.State.Starting);

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

        private UniTask RecoveryDelayAsync(CancellationToken ct) =>
            UniTask.Delay(CONNECTION_LOOP_RECOVER_INTERVAL, cancellationToken: ct);


        protected async UniTask<bool> TryConnectToRoomAsync(string connectionString, CancellationToken token)
        {
            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                $"[ChatConnectiveRoom] Starting connection attempt with string: {connectionString}");

            var credentials = new ConnectionStringCredentials(connectionString);

            bool connectResult = await ConnectToRoomAsync(credentials, token);

            AttemptToConnectState connectionState = connectResult ? AttemptToConnectState.Success : AttemptToConnectState.Error;
            attemptToConnectState.Set(connectionState);

            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                $"[ChatConnectiveRoom] Connection state set to: {connectionState}");

            if (connectResult == false)
            {
                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                    $"[ChatConnectiveRoom] Failed to connect - URL: {credentials.Url}");
                return connectResult;
            }

            roomState.Set(IConnectiveRoom.State.Running);
            ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                $"[ChatConnectiveRoom] Room state set to Running");

            return connectResult;
        }

        private async UniTask<bool> ConnectToRoomAsync<T>(T credentials, CancellationToken ct)
            where T: ICredentials
        {
            bool connectResult;

            try
            {
                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                    $"[ChatConnectiveRoom] Starting connection attempt - URL: {credentials.Url}");

                connectResult = await roomInstance.ConnectAsync(credentials.Url, credentials.AuthToken, ct, true);

                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                    $"[ChatConnectiveRoom] Connection attempt result: {connectResult}");
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.CHAT_CONVERSATIONS,
                    $"[ChatConnectiveRoom] Connection failed with exception: {e.Message}");
                throw;
            }

            if (connectResult == false)
            {
                return (connectResult);
            }

            room.Assign(roomInstance, out IRoom _);

            return (connectResult);
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
    }
}
