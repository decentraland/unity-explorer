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
using Utility;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms.Chat
{
    public class ChatConnectiveRoom : IActivatableConnectiveRoom
    {
        private static readonly TimeSpan HEARTBEATS_INTERVAL = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan CONNECTION_LOOP_RECOVER_INTERVAL = TimeSpan.FromSeconds(5);
        private const string LOG_PREFIX = nameof(ChatConnectiveRoom);
        private readonly IWebRequestController webRequests;
        private readonly URLAddress adapterAddress;
        private readonly InteriorRoom room = new ();
        private readonly Atomic<IConnectiveRoom.ConnectionLoopHealth> connectionLoopHealth = new (IConnectiveRoom.ConnectionLoopHealth.Stopped);
        private readonly Atomic<AttemptToConnectState> attemptToConnectState = new (AttemptToConnectState.None);
        private readonly Atomic<IConnectiveRoom.State> roomState = new (IConnectiveRoom.State.Stopped);
        private readonly IRoom roomInstance;

        private CancellationTokenSource? cts;

        public bool Activated { get; private set; }
        public IConnectiveRoom.ConnectionLoopHealth CurrentConnectionLoopHealth => connectionLoopHealth.Value();
        public IConnectiveRoom.State CurrentState() => roomState.Value();
        public AttemptToConnectState AttemptToConnectState => attemptToConnectState.Value();
        public IRoom Room() => room;

        public ChatConnectiveRoom(IWebRequestController webRequests, URLAddress adapterAddress)
        {
            this.webRequests = webRequests;
            this.adapterAddress = adapterAddress;

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

        public async UniTask ActivateAsync()
        {
            if (Activated)
            {
                return;
            }

            Activated = true;
            await this.StartIfNotAsync();
        }

        public async UniTask DeactivateAsync()
        {
            if (!Activated)
            {
                return;
            }

            Activated = false;
            await this.StopIfNotAsync();
        }

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            cts = null;
        }

        public async UniTask<bool> StartAsync()
        {
            if (CurrentState() is not IConnectiveRoom.State.Stopped)
                throw new InvalidOperationException("Room is already running");

            cts = cts.SafeRestart();
            attemptToConnectState.Set(AttemptToConnectState.None);
            roomState.Set(IConnectiveRoom.State.Starting);
            RunAsync(cts.Token).Forget();
            await UniTask.WaitWhile(() => attemptToConnectState.Value() is AttemptToConnectState.None);
            return attemptToConnectState.Value() is not AttemptToConnectState.Error;
        }

        public virtual async UniTask StopAsync()
        {
            if (CurrentState() is IConnectiveRoom.State.Stopped or IConnectiveRoom.State.Stopping)
                throw new InvalidOperationException("Room is already stopped");

            cts = cts.SafeRestart();
            roomState.Set(IConnectiveRoom.State.Stopping);
            await room.ResetRoomAsync(cts.Token);
            roomState.Set(IConnectiveRoom.State.Stopped);
        }

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            roomState.Set(IConnectiveRoom.State.Starting);

            while (ct.IsCancellationRequested == false)
            {
                await ExecuteWithRecoveryAsync(ct);
                await UniTask.Delay(HEARTBEATS_INTERVAL, cancellationToken: ct);
            }

            connectionLoopHealth.Set(IConnectiveRoom.ConnectionLoopHealth.Stopped);
        }

        private async UniTask ExecuteWithRecoveryAsync(CancellationToken ct)
        {
            do
            {
                try
                {
                    connectionLoopHealth.Set(IConnectiveRoom.ConnectionLoopHealth.Running);
                    await CycleStepAsync(ct);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ReportHub.LogError(ReportCategory.LIVEKIT, $"{LOG_PREFIX} - CycleStepAsync failed: {e}");
                    connectionLoopHealth.Set(IConnectiveRoom.ConnectionLoopHealth.CycleFailed);
                    await RecoveryDelayAsync(ct);
                }
            }
            while (!ct.IsCancellationRequested && connectionLoopHealth.Value() == IConnectiveRoom.ConnectionLoopHealth.CycleFailed);
        }

        private async UniTask CycleStepAsync(CancellationToken ct)
        {
            if (CurrentState() is not IConnectiveRoom.State.Running)
            {
                await room.DisconnectAsync(ct);

                string connectionString = await ConnectionStringAsync(ct);

                await TryConnectToRoomAsync(connectionString, ct);
            }
        }

        private async UniTask<string> ConnectionStringAsync(CancellationToken ct)
        {
            string metadata = FixedMetadata.Default.ToJson();
            var result = webRequests.SignedFetchGetAsync(adapterAddress, metadata, ct);
            AdapterResponse response = await result.CreateFromJson<AdapterResponse>(WRJsonParser.Unity);
            string connectionString = response.adapter;
            return connectionString;
        }

        private UniTask RecoveryDelayAsync(CancellationToken ct) =>
            UniTask.Delay(CONNECTION_LOOP_RECOVER_INTERVAL, cancellationToken: ct);

        private async UniTask<bool> TryConnectToRoomAsync(string connectionString, CancellationToken token)
        {
            var credentials = new ConnectionStringCredentials(connectionString);

            bool connectResult = await ConnectToRoomAsync(credentials, token);

            AttemptToConnectState connectionState = connectResult ? AttemptToConnectState.Success : AttemptToConnectState.Error;
            attemptToConnectState.Set(connectionState);

            if (connectResult == false)
            {
                return connectResult;
            }

            roomState.Set(IConnectiveRoom.State.Running);
            return connectResult;
        }

        private async UniTask<bool> ConnectToRoomAsync<T>(T credentials, CancellationToken ct)
            where T: ICredentials
        {
            bool connectResult;

            try
            {
                connectResult = await roomInstance.ConnectAsync(credentials.Url, credentials.AuthToken, ct, true);
            }
            catch (Exception e)
            {
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
