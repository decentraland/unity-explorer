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
        private readonly Atomic<IConnectiveRoom.State> roomState = new (IConnectiveRoom.State.Sleep);
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
            if (CurrentState() is not IConnectiveRoom.State.Sleep)
                throw new InvalidOperationException("Room is already running");

            roomState.Set(IConnectiveRoom.State.Starting);
            RunAsync().Forget();
        }

        public void Stop()
        {
            roomState.Set(IConnectiveRoom.State.Sleep);
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }

        public IConnectiveRoom.State CurrentState() =>
            roomState.Value();

        public IRoom Room() =>
            room;

        private CancellationToken CancellationToken()
        {
            Stop();
            cancellationTokenSource = new CancellationTokenSource();
            return cancellationTokenSource.Token;
        }

        private async UniTaskVoid RunAsync()
        {
            CancellationToken token = CancellationToken();
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
            // IRoom newRoom = new LogRoom(
            //     new Room(
            //         new ArrayMemoryPool(),
            //         new DefaultActiveSpeakers(),
            //         new ParticipantsHub(),
            //         new TracksFactory(),
            //         new FfiHandleFactory(),
            //         new ParticipantFactory(),
            //         new TrackPublicationFactory(),
            //         new DataPipe(),
            //         new MemoryRoomInfo()
            //     )
            // );//TODO move to object pool, but avoid memory corruption
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
