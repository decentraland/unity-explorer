using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Credentials;
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
using UnityEngine.Pool;
using Utility;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Rooms.Connective
{
    public delegate UniTask PrewarmAsyncDelegate(CancellationToken token);

    public delegate UniTask CycleStepDelegate(
        ConnectToRoomAsyncDelegate connectToRoomAsyncDelegate,
        DisconnectCurrentRoomAsyncDelegate disconnectCurrentRoomAsyncDelegate,
        CancellationToken token
    );

    public delegate UniTask ConnectToRoomAsyncDelegate(string connectionString, CancellationToken token);

    public delegate UniTask DisconnectCurrentRoomAsyncDelegate(CancellationToken token);

    public class ConnectiveRoom : IConnectiveRoom
    {
        private readonly PrewarmAsyncDelegate prewarmAsync;
        private readonly CycleStepDelegate runConnectCycleStepAsync;
        private readonly Action<string> log;

        private readonly InteriorRoom room = new ();
        private readonly TimeSpan heartbeatsInterval = TimeSpan.FromSeconds(1);
        private readonly Atomic<IConnectiveRoom.State> roomState = new (IConnectiveRoom.State.Stopped);
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

        public ConnectiveRoom(PrewarmAsyncDelegate prewarmAsync, CycleStepDelegate runConnectCycleStepAsync, string logPrefix) : this(
            prewarmAsync,
            runConnectCycleStepAsync,
            m => ReportHub.WithReport(ReportCategory.LIVEKIT).Log($"Room log - {logPrefix}: {m}")
        )
        {
        }

        public ConnectiveRoom(
            PrewarmAsyncDelegate prewarmAsync,
            CycleStepDelegate runConnectCycleStepAsync,
            Action<string> log
        )
        {
            this.prewarmAsync = prewarmAsync;
            this.runConnectCycleStepAsync = runConnectCycleStepAsync;
            this.log = log;
        }

        public void Start()
        {
            if (CurrentState() is not IConnectiveRoom.State.Stopped)
                throw new InvalidOperationException("Room is already running");

            roomState.Set(IConnectiveRoom.State.Starting);
            RunAsync().Forget();
        }

        public async UniTask StopAsync()
        {
            if (CurrentState() is IConnectiveRoom.State.Stopped or IConnectiveRoom.State.Stopping)
                throw new InvalidOperationException("Room is already stopped");

            roomState.Set(IConnectiveRoom.State.Stopping);
            await AssignNewRoomAndReleasePreviousAsync(NullRoom.INSTANCE, CancellationToken.None);
            roomState.Set(IConnectiveRoom.State.Stopped);
            cancellationTokenSource.SafeCancelAndDispose();
            cancellationTokenSource = null;
        }

        public IConnectiveRoom.State CurrentState() =>
            roomState.Value();

        public IRoom Room() =>
            room;

        private async UniTask<CancellationToken> NewCancellationTokenAsync()
        {
            if (cancellationTokenSource != null)
                await StopAsync();

            cancellationTokenSource = new CancellationTokenSource();
            return cancellationTokenSource.Token;
        }

        private async UniTaskVoid RunAsync()
        {
            CancellationToken token = await NewCancellationTokenAsync();
            roomState.Set(IConnectiveRoom.State.Starting);
            await prewarmAsync(token);

            while (token.IsCancellationRequested == false)
            {
                await runConnectCycleStepAsync(TryConnectToRoomAsync, DisconnectCurrentRoomAsync, token);
                await UniTask.Delay(heartbeatsInterval, cancellationToken: token);
            }
        }

        private async UniTask DisconnectCurrentRoomAsync(CancellationToken token)
        {
            log($"Trying to disconnect current room started");
            roomState.Set(IConnectiveRoom.State.Stopping);
            await AssignNewRoomAndReleasePreviousAsync(NullRoom.INSTANCE, token);
            roomState.Set(IConnectiveRoom.State.Stopped);
            log($"Trying to disconnect current room finished");
        }

        private async UniTask TryConnectToRoomAsync(string connectionString, CancellationToken token)
        {
            log($"Trying to connect to started: {connectionString}");

            var newRoom = roomPool.Get()!;

            var credentials = new ConnectionStringCredentials(connectionString);
            bool connectResult = await newRoom.ConnectAsync(credentials, token);

            if (connectResult == false)
            {
                roomPool.Release(newRoom);
                ReportHub.LogWarning(ReportCategory.LIVEKIT, $"Cannot connect to room with url: {credentials.Url} with token: {credentials.AuthToken}");
                return;
            }

            await AssignNewRoomAndReleasePreviousAsync(newRoom, token);
            roomState.Set(IConnectiveRoom.State.Running);
            log($"Trying to connect to finished successfully: {connectionString}");
        }

        private async UniTask AssignNewRoomAndReleasePreviousAsync(IRoom newRoom, CancellationToken token)
        {
            room.Assign(newRoom, out IRoom? previous);

            if (previous is not null)
            {
                await previous.DisconnectAsync(token);
                roomPool.Release(previous);
            }
        }
    }
}
