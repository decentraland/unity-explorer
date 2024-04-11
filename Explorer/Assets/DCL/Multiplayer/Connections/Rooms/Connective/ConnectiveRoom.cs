using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Credentials;
using DCL.Utilities.Extensions;
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

    public delegate UniTask CycleStepDelegate(ConnectToRoomAsyncDelegate connectToRoomAsyncDelegate, CancellationToken token);

    public delegate UniTask ConnectToRoomAsyncDelegate(string connectionString, CancellationToken token);

    public class ConnectiveRoom : IConnectiveRoom
    {
        private readonly PrewarmAsyncDelegate prewarmAsync;
        private readonly CycleStepDelegate runConnectCycleStepAsync;

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

        public ConnectiveRoom(
            PrewarmAsyncDelegate prewarmAsync,
            CycleStepDelegate runConnectCycleStepAsync
        )
        {
            this.prewarmAsync = prewarmAsync;
            this.runConnectCycleStepAsync = runConnectCycleStepAsync;
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
            await room.DisconnectAsync(cancellationTokenSource.EnsureNotNull("Cancellation token must exist").Token);
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
                await runConnectCycleStepAsync(TryConnectToRoomAsync, token);
                await UniTask.Delay(heartbeatsInterval, cancellationToken: token);
            }
        }

        private async UniTask TryConnectToRoomAsync(string connectionString, CancellationToken token)
        {
            var newRoom = roomPool.Get()!;

            var credentials = new ConnectionStringCredentials(connectionString);
            bool connectResult = await newRoom.ConnectAsync(credentials, token);

            if (connectResult == false)
            {
                roomPool.Release(newRoom);
                ReportHub.LogWarning(ReportCategory.LIVEKIT, $"Cannot connect to room with url: {credentials.Url} with token: {credentials.AuthToken}");
                return;
            }

            room.Assign(newRoom, out IRoom? previous);

            if (previous is not null)
            {
                await previous.DisconnectAsync(token);
                roomPool.Release(previous);
            }

            roomState.Set(IConnectiveRoom.State.Running);
        }
    }
}
