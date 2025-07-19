using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Audio;
using DCL.Multiplayer.Connections.Credentials;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Internal;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Rooms;
using LiveKit.Rooms.ActiveSpeakers;
using LiveKit.Rooms.DataPipes;
using LiveKit.Rooms.Info;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Participants.Factory;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.Tracks.Factory;
using LiveKit.Rooms.VideoStreaming;
using System;
using System.ComponentModel;
using System.Threading;
using Utility;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms.Chat
{
    // TODO: This Room will be refactored following the comments left and tracked on this ticket: 4693
    public class VoiceChatActivatableConnectiveRoom : IActivatableConnectiveRoom
    {
        private const string LOG_PREFIX = "VoiceChatRoom";
        private static readonly TimeSpan HEARTBEATS_INTERVAL = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan CONNECTION_UPDATE_INTERVAL = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan CONNECTION_LOOP_RECOVER_INTERVAL = TimeSpan.FromSeconds(5);
        private readonly InteriorRoom room = new ();
        private readonly Atomic<IConnectiveRoom.ConnectionLoopHealth> connectionLoopHealth = new (IConnectiveRoom.ConnectionLoopHealth.Stopped);
        private readonly Atomic<AttemptToConnectState> attemptToConnectState = new (AttemptToConnectState.NONE);
        private readonly Atomic<IConnectiveRoom.State> roomState = new (IConnectiveRoom.State.Stopped);
        private CancellationTokenSource? cts;
        private string connectionString = string.Empty;
        public bool Activated { get; private set; }
        public IConnectiveRoom.ConnectionLoopHealth CurrentConnectionLoopHealth => connectionLoopHealth.Value();
        public AttemptToConnectState AttemptToConnectState => attemptToConnectState.Value();

        public void Dispose()
        {
            cts.SafeCancelAndDispose();
            cts = null;
        }

        public IConnectiveRoom.State CurrentState() =>
            roomState.Value();

        public IRoom Room() =>
            room;

        public async UniTask<bool> TrySetConnectionStringAndActivateAsync(string newConnectionString)
        {
            connectionString = newConnectionString;
            await DeactivateAsync();
            await ActivateAsync();
            return CurrentState() is IConnectiveRoom.State.Running;
        }

        public async UniTask ActivateAsync()
        {
            if (Activated) { return; }

            Activated = true;

            await this.StartIfNotAsync();
        }

        public async UniTask DeactivateAsync()
        {
            if (!Activated) { return; }

            Activated = false;
            await this.StopIfNotAsync();
        }

        public async UniTask<bool> StartAsync()
        {
            if (CurrentState() is not IConnectiveRoom.State.Stopped)
                throw new WarningException("Room is already running");

            cts = cts.SafeRestart();
            attemptToConnectState.Set(AttemptToConnectState.NONE);

            if (connectionString == string.Empty)
            {
                ReportHub.LogWarning(ReportCategory.LIVEKIT, $"{LOG_PREFIX} - No connection string specified");
                return false;
            }

            roomState.Set(IConnectiveRoom.State.Starting);
            RunAsync(cts.Token).Forget();
            await UniTask.WaitWhile(() => attemptToConnectState.Value() is AttemptToConnectState.NONE);

            if (attemptToConnectState.Value() is AttemptToConnectState.ERROR)
            {
                roomState.Set(IConnectiveRoom.State.Stopped);
                attemptToConnectState.Set(AttemptToConnectState.NONE);
                return false;
            }

            return true;
        }

        public async UniTask StopAsync()
        {
            if (CurrentState() is IConnectiveRoom.State.Stopped or IConnectiveRoom.State.Stopping)
                throw new InvalidOperationException("Room is already stopped");

            roomState.Set(IConnectiveRoom.State.Stopping);

            cts = cts.SafeRestart();

            if (connectionLoopHealth != IConnectiveRoom.ConnectionLoopHealth.Stopped)
                await room.ResetRoomAsync(cts.Token);

            roomState.Set(IConnectiveRoom.State.Stopped);
            connectionString = string.Empty;
        }

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            roomState.Set(IConnectiveRoom.State.Starting);

            SendConnectionStatusAsync(ct).Forget();

            while (ct.IsCancellationRequested == false)
            {
                await ExecuteWithRecoveryAsync(ct);
                await UniTask.Delay(HEARTBEATS_INTERVAL, cancellationToken: ct);
            }

            connectionLoopHealth.Set(IConnectiveRoom.ConnectionLoopHealth.Stopped);
            roomState.Set(IConnectiveRoom.State.Stopped);
        }

        private async UniTaskVoid SendConnectionStatusAsync(CancellationToken ct)
        {
            while (ct.IsCancellationRequested == false)
            {
                if (CurrentState() == IConnectiveRoom.State.Running)
                    room.SimulateConnectionStateChanged();

                await UniTask.Delay(CONNECTION_UPDATE_INTERVAL, cancellationToken: ct);
            }
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
                    ReportHub.LogWarning(ReportCategory.LIVEKIT, $"{LOG_PREFIX} - CycleStepAsync failed: {e}");
                    connectionLoopHealth.Set(IConnectiveRoom.ConnectionLoopHealth.CycleFailed);
                    await RecoveryDelayAsync(ct);
                }
            }
            while (!ct.IsCancellationRequested && connectionLoopHealth.Value() == IConnectiveRoom.ConnectionLoopHealth.CycleFailed);
        }

        private async UniTask CycleStepAsync(CancellationToken ct)
        {
            if (CurrentState() is not IConnectiveRoom.State.Running)
                await TryConnectToRoomAsync(ct);
        }

        private UniTask RecoveryDelayAsync(CancellationToken ct) =>
            UniTask.Delay(CONNECTION_LOOP_RECOVER_INTERVAL, cancellationToken: ct);

        private async UniTask<bool> TryConnectToRoomAsync(CancellationToken ct)
        {
            var credentials = new ConnectionStringCredentials(connectionString);

            // Create a fresh room instance each time to ensure clean state
            var freshRoom = CreateFreshRoom();

            (bool success, string? errorMessage) connectResult = await freshRoom.ConnectAsync(credentials.Url, credentials.AuthToken, ct, true);

            AttemptToConnectState connectionState = connectResult.success ? AttemptToConnectState.SUCCESS : AttemptToConnectState.ERROR;
            attemptToConnectState.Set(connectionState);

            if (connectResult.success)
            {
                room.Assign(freshRoom, out IRoom _);
                roomState.Set(IConnectiveRoom.State.Running);
            }

            return connectResult.success;
        }

        private static IRoom CreateFreshRoom()
        {
            var hub = new ParticipantsHub();
            var videoStreams = new VideoStreams(hub);
            var audioRemixConveyor = new ThreadedAudioRemixConveyor();
            var audioStreams = new AudioStreams(hub, audioRemixConveyor);
            var tracksFactory = new TracksFactory();

            var newRoom = new Room(
                new ArrayMemoryPool(),
                new DefaultActiveSpeakers(),
                hub,
                tracksFactory,
                new FfiHandleFactory(),
                new ParticipantFactory(),
                new TrackPublicationFactory(),
                new DataPipe(),
                new MemoryRoomInfo(),
                videoStreams,
                audioStreams,
                null!
            );

            return new LogRoom(newRoom);
        }

        public static class Null
        {
            public static readonly VoiceChatActivatableConnectiveRoom INSTANCE = new ();
        }
    }
}
