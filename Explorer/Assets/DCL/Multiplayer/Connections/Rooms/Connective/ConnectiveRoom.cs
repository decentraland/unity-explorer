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
    public enum RoomSelection : byte
    {
        NEW,
        PREVIOUS,
    }

    public enum AttemptToConnectState
    {
        None,
        Success,
        Error,
    }

    /// <summary>
    ///     Connective room can live forever in a loop stopping and connecting to different LiveKit rooms
    /// </summary>
    public abstract class ConnectiveRoom : IConnectiveRoom
    {
        private static readonly TimeSpan HEARTBEATS_INTERVAL = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan CONNECTION_LOOP_RECOVER_INTERVAL = TimeSpan.FromSeconds(5);
        internal readonly string logPrefix;

        private readonly InteriorRoom room = new ();

        private readonly Atomic<IConnectiveRoom.ConnectionLoopHealth> connectionLoopHealth = new (IConnectiveRoom.ConnectionLoopHealth.Stopped);

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

        public IConnectiveRoom.ConnectionLoopHealth CurrentConnectionLoopHealth => connectionLoopHealth.Value();

        protected ConnectiveRoom()
        {
            logPrefix = GetType().Name;
        }

        protected abstract UniTask PrewarmAsync(CancellationToken token);

        protected abstract UniTask CycleStepAsync(CancellationToken token);

        /// <summary>
        ///     When the new room is connected, it can be invalid so it's possible to revert to the previous one
        /// </summary>
        protected virtual RoomSelection SelectValidRoom() =>
            RoomSelection.NEW;

        public async UniTask<bool> StartAsync()
        {
            if (CurrentState() is not IConnectiveRoom.State.Stopped)
                throw new InvalidOperationException("Room is already running");

            attemptToConnectState.Set(AttemptToConnectState.None);
            roomState.Set(IConnectiveRoom.State.Starting);
            RunAsync((cancellationTokenSource = new CancellationTokenSource()).Token).Forget();
            await UniTask.WaitWhile(() => attemptToConnectState.Value() is AttemptToConnectState.None);
            return attemptToConnectState.Value() is AttemptToConnectState.Success;
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

        public IRoom Room() =>
            room;

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
                catch (Exception e)
                {
                    ReportHub.LogError(ReportCategory.LIVEKIT, $"{logPrefix} - {funcName} failed: {e}");
                    connectionLoopHealth.Set(IConnectiveRoom.ConnectionLoopHealth.PrewarmFailed);
                    await RecoveryDelayAsync(ct);
                }
            }
            while (!ct.IsCancellationRequested && connectionLoopHealth.Value() == IConnectiveRoom.ConnectionLoopHealth.PrewarmFailed);
        }

        private UniTask RecoveryDelayAsync(CancellationToken ct) =>
            UniTask.Delay(CONNECTION_LOOP_RECOVER_INTERVAL, cancellationToken: ct);

        protected async UniTask DisconnectCurrentRoomAsync(CancellationToken token)
        {
            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{logPrefix} - Trying to disconnect current room started");

            roomState.Set(IConnectiveRoom.State.Stopping);
            await room.ResetRoom(roomPool, token);
            roomState.Set(IConnectiveRoom.State.Stopped);

            ReportHub
               .WithReport(ReportCategory.LIVEKIT)
               .Log($"{logPrefix} - Trying to disconnect current room finished");
        }

        protected async UniTask<RoomSelection> TryConnectToRoomAsync(string connectionString, CancellationToken token)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} - Trying to connect to started: {connectionString}");

            var credentials = new ConnectionStringCredentials(connectionString);

            (bool connectResult, RoomSelection roomSelection) = await ChangeRoomsAsync(roomPool, credentials, token);

            AttemptToConnectState connectionState = connectResult ? AttemptToConnectState.Success : AttemptToConnectState.Error;
            attemptToConnectState.Set(connectionState);

            if (connectResult == false)
            {
                ReportHub.LogWarning(ReportCategory.LIVEKIT, $"{logPrefix} - Cannot connect to room with url: {credentials.Url} with token: {credentials.AuthToken}");
                return roomSelection;
            }

            switch (roomSelection)
            {
                case RoomSelection.NEW:
                    roomState.Set(IConnectiveRoom.State.Running);
                    ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} - Trying to connect to finished successfully {connectionString}");

                    break;
                case RoomSelection.PREVIOUS:
                    // preserve the previous state (for whatever reason)
                    ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} - Connection to the previous room was preserved");
                    break;
            }

            return roomSelection;
        }

        /// <summary>
        ///     Disconnect the previous room, assigns a new one, and connects to it<br />
        ///     This way the flow of events is preserved so room status will be propagated properly to the subscribers
        /// </summary>
        /// <returns>Previous room</returns>
        private async UniTask<(bool connectResult, RoomSelection selection)> ChangeRoomsAsync<T>(IObjectPool<IRoom> roomsPool, T credentials, CancellationToken ct)
            where T: ICredentials
        {
            IRoom? newRoom = roomsPool.Get();
            IRoom previous = room.assigned;

            bool connectResult;

            try
            {
                // Don't disconnect the current room as it can be used instead of the new one according to the SelectValidRoom delegate
                // Subscribers will miss connection callback
                connectResult = await newRoom.ConnectAsync(credentials.Url, credentials.AuthToken, ct, true);
            }
            catch (Exception)
            {
                roomsPool.Release(newRoom);
                throw;
            }

            if (connectResult == false)
            {
                roomsPool.Release(newRoom);
                return (connectResult, RoomSelection.PREVIOUS);
            }

            // now it's a moment to check if we should drop the new room and keep the previous one
            RoomSelection roomSelection = SelectValidRoom();

            await room.SwapRoomsAsync(roomSelection, previous, newRoom, roomsPool, ct);

            return (connectResult, roomSelection);
        }
    }
}
