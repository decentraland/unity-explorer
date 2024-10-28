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

    public enum RoomSelection : byte
    {
        NEW,
        PREVIOUS,
    }

    /// <summary>
    ///     When the new room is connected, it can be invalid so it's possible to revert to the previous one
    /// </summary>
    public delegate RoomSelection SelectValidRoom();

    public delegate UniTask<RoomSelection> ConnectToRoomAsyncDelegate(string connectionString, SelectValidRoom selectValidRoom, CancellationToken token);

    public delegate UniTask DisconnectCurrentRoomAsyncDelegate(CancellationToken token);

    public enum AttemptToConnectState
    {
        None,
        Success,
        Error,
    }

    public class ConnectiveRoom : IConnectiveRoom
    {
        private readonly PrewarmAsyncDelegate prewarmAsync;
        private readonly CycleStepDelegate runConnectCycleStepAsync;
        private string logPrefix;

        private readonly InteriorRoom room = new ();
        private readonly TimeSpan heartbeatsInterval = TimeSpan.FromSeconds(1);
        private readonly Atomic<AttemptToConnectState> attemptToConnectState = new (AttemptToConnectState.None);
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

        public ConnectiveRoom(PrewarmAsyncDelegate prewarmAsync, CycleStepDelegate runConnectCycleStepAsync, string logPrefix)
        {
            this.prewarmAsync = prewarmAsync;
            this.runConnectCycleStepAsync = runConnectCycleStepAsync;
            this.logPrefix = logPrefix;
        }

        public async UniTask<bool> StartAsync()
        {
            if (CurrentState() is not IConnectiveRoom.State.Stopped)
                throw new InvalidOperationException("Room is already running");

            attemptToConnectState.Set(AttemptToConnectState.None);
            roomState.Set(IConnectiveRoom.State.Starting);
            RunAsync().Forget();
            await UniTask.WaitWhile(() => attemptToConnectState.Value() is AttemptToConnectState.None);
            return attemptToConnectState.Value() is AttemptToConnectState.Success;
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
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{logPrefix} - Trying to disconnect current room started");
            roomState.Set(IConnectiveRoom.State.Stopping);
            await AssignNewRoomAndReleasePreviousAsync(NullRoom.INSTANCE, token);
            roomState.Set(IConnectiveRoom.State.Stopped);
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{logPrefix} - Trying to disconnect current room finished");
        }

        private async UniTask<RoomSelection> TryConnectToRoomAsync(string connectionString, SelectValidRoom selectValidRoom, CancellationToken token)
        {
            ReportHub
                .WithReport(ReportCategory.LIVEKIT)
                .Log($"{logPrefix} - Trying to connect to started: {connectionString}");

            var newRoom = roomPool.Get()!;

            var credentials = new ConnectionStringCredentials(connectionString);

            bool connectResult = await newRoom.ConnectAsync(credentials, token);

            AttemptToConnectState connectionState = connectResult ? AttemptToConnectState.Success : AttemptToConnectState.Error;
            attemptToConnectState.Set(connectionState);

            if (connectResult == false)
            {
                roomPool.Release(newRoom);
                ReportHub.LogWarning(ReportCategory.LIVEKIT, $"{logPrefix} - Cannot connect to room with url: {credentials.Url} with token: {credentials.AuthToken}");
                return RoomSelection.PREVIOUS;
            }

            RoomSelection roomSelection = selectValidRoom();

            switch (roomSelection)
            {
                case RoomSelection.NEW:
                    await AssignNewRoomAndReleasePreviousAsync(newRoom, token);
                    roomState.Set(IConnectiveRoom.State.Running);

                    ReportHub
                       .WithReport(ReportCategory.LIVEKIT)
                       .Log($"{logPrefix} - Trying to connect to finished successfully {connectionString}");

                    break;
                case RoomSelection.PREVIOUS:
                    // drop the new room
                    await ReleaseRoomAsync(newRoom, token);

                    // preserve the previous state (for whatever reason)
                    ReportHub
                       .WithReport(ReportCategory.LIVEKIT)
                       .Log($"{logPrefix} - Connection to the previous room was preserved");

                    break;
                default: throw new ArgumentOutOfRangeException(nameof(roomSelection));
            }

            return roomSelection;
        }

        private async UniTask AssignNewRoomAndReleasePreviousAsync(IRoom newRoom, CancellationToken token)
        {
            room.Assign(newRoom, out IRoom? previous);

            if (previous is not null)
                await ReleaseRoomAsync(previous, token);
        }

        private async UniTask ReleaseRoomAsync(IRoom room, CancellationToken token)
        {
            await room.DisconnectAsync(token);
            roomPool.Release(room);
        }
    }
}
