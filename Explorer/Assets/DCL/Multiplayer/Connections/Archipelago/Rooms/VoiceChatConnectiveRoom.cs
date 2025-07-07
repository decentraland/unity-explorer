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
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms.Chat
{
    public interface IVoiceChatActivatableConnectiveRoom : IActivatableConnectiveRoom
    {
        UniTask<bool> TrySetConnectionStringAndActivateAsync(string newConnectionString);
    }

    public class VoiceChatConnectiveRoom : ConnectiveRoom
    {
        public enum VoiceChatConnectionState
        {
            DISCONNECTED,
            CONNECTING,
            CONNECTED,
            RECONNECTING,
            FAILED
        }

        public event Action<VoiceChatConnectionState>? ConnectionStateChanged;

        private class Activatable : ActivatableConnectiveRoom, IVoiceChatActivatableConnectiveRoom
        {
            private readonly VoiceChatConnectiveRoom origin;

            public Activatable(VoiceChatConnectiveRoom origin, bool initialState = true) : base(origin, initialState)
            {
                this.origin = origin;
            }

            public async UniTask<bool> TrySetConnectionStringAndActivateAsync(string newConnectionString) =>
                await origin.TrySetConnectionStringAndActivateAsync(newConnectionString);
        }

        private const int CONNECTION_TIMEOUT_SECONDS = 60;
        private string connectionString = string.Empty;
        private VoiceChatConnectionState currentConnectionState = VoiceChatConnectionState.DISCONNECTED;

        private void NotifyConnectionStateChanged(VoiceChatConnectionState newState)
        {
            if (currentConnectionState != newState)
            {
                ReportHub.Log(ReportCategory.VOICE_CHAT, $"{logPrefix} - Connection state changed: {currentConnectionState} -> {newState}");
                currentConnectionState = newState;
                ConnectionStateChanged?.Invoke(newState);
            }
        }

        private async UniTask<bool> TrySetConnectionStringAndActivateAsync(string newConnectionString)
        {
            // Set the connection string first
            connectionString = newConnectionString;

            if (string.IsNullOrEmpty(connectionString))
            {
                ReportHub.LogWarning(ReportCategory.LIVEKIT, $"{logPrefix} - Empty connection string provided");
                return false;
            }

            try
            {
                if (CurrentState() is not IConnectiveRoom.State.Stopped)
                {
                    await StopAsync();
                }
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.LIVEKIT, $"{logPrefix} - Failed to stop room during connection string update: {e}");
            }

            connectionString = newConnectionString;

            if (connectionString != string.Empty)
            {
                try
                {
                    // Wrap StartAsync with timeout
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(CONNECTION_TIMEOUT_SECONDS));
                    var startTask = StartAsync();
                    var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(CONNECTION_TIMEOUT_SECONDS), cancellationToken: timeoutCts.Token).ContinueWith(() => false);

                    var (winIndex, result) = await UniTask.WhenAny(new[] { startTask, timeoutTask });

                    if (winIndex == 1) // Timeout won
                    {
                        ReportHub.LogError(ReportCategory.LIVEKIT, $"{logPrefix} - Connection timeout after {CONNECTION_TIMEOUT_SECONDS} seconds");
                        timeoutCts.Cancel();

                        // Stop the ongoing connection attempt
                        try
                        {
                            await StopAsync();
                        }
                        catch (Exception stopEx)
                        {
                            ReportHub.LogWarning(ReportCategory.LIVEKIT, $"{logPrefix} - Failed to stop room after timeout: {stopEx}");
                        }

                        return false;
                    }

                    return result;
                }
                catch (Exception e)
                {
                    ReportHub.LogWarning(ReportCategory.LIVEKIT, $"{logPrefix} - Failed to start room during connection string update: {e}");
                }
            }

            return CurrentState() is IConnectiveRoom.State.Running;
        }

        public IVoiceChatActivatableConnectiveRoom AsActivatable() =>
            new Activatable(this);

        protected override async UniTask CycleStepAsync(CancellationToken token)
        {
            if (CurrentState() is not IConnectiveRoom.State.Running)
            {
                if (string.IsNullOrEmpty(connectionString))
                {
                    // No connection string set, abort the connection attempt
                    attemptToConnectState.Set(AttemptToConnectState.NO_CONNECTION_REQUIRED);
                    return;
                }

                await TryConnectToRoomAsync(connectionString, token);
            }
        }

        protected override async UniTask PrewarmAsync(CancellationToken token)
        {
            // Voice chat rooms don't need prewarming since we always create fresh rooms
            await UniTask.CompletedTask;

            StateChanged += OnStateChanged;
            ConnectionLoopHealthChanged += OnHealthChanged;
        }

        public override void Dispose()
        {
            // Unsubscribe from events
            StateChanged -= OnStateChanged;
            ConnectionLoopHealthChanged -= OnHealthChanged;

            base.Dispose();
        }

        private void OnStateChanged(IConnectiveRoom.State newState)
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{logPrefix} - Room state changed: {newState}");

            switch (newState)
            {
                case IConnectiveRoom.State.Starting:
                    NotifyConnectionStateChanged(VoiceChatConnectionState.CONNECTING);
                    break;
                case IConnectiveRoom.State.Running:
                    if (CurrentConnectionLoopHealth == IConnectiveRoom.ConnectionLoopHealth.Running)
                    {
                        NotifyConnectionStateChanged(VoiceChatConnectionState.CONNECTED);
                    }
                    break;
                case IConnectiveRoom.State.Stopping:
                case IConnectiveRoom.State.Stopped:
                    NotifyConnectionStateChanged(VoiceChatConnectionState.DISCONNECTED);
                    break;
            }
        }

        private void OnHealthChanged(IConnectiveRoom.ConnectionLoopHealth newHealth)
        {
            ReportHub.Log(ReportCategory.VOICE_CHAT, $"{logPrefix} - Connection loop health changed: {newHealth}");
            
            switch (newHealth)
            {
                case IConnectiveRoom.ConnectionLoopHealth.Running:
                    if (CurrentState() == IConnectiveRoom.State.Running)
                    {
                        NotifyConnectionStateChanged(VoiceChatConnectionState.CONNECTED);
                    }
                    break;
                case IConnectiveRoom.ConnectionLoopHealth.CycleFailed:
                    NotifyConnectionStateChanged(VoiceChatConnectionState.RECONNECTING);
                    break;
                case IConnectiveRoom.ConnectionLoopHealth.PrewarmFailed:
                    NotifyConnectionStateChanged(VoiceChatConnectionState.FAILED);
                    break;
                case IConnectiveRoom.ConnectionLoopHealth.Stopped:
                    NotifyConnectionStateChanged(VoiceChatConnectionState.DISCONNECTED);
                    break;
            }
        }



        // We override this to use fresh rooms instead of pooling, because right now reusing rooms causes issues with the audio.
        // TODO: We should use a pool of rooms, but we need to fix the audio issues first.
        protected override async UniTask<RoomSelection> TryConnectToRoomAsync(string connectionString, CancellationToken token)
        {
            ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} - Trying to connect to started: {connectionString}");

            var credentials = new ConnectionStringCredentials(connectionString);

            // Create fresh room instead of using pool
            var freshRoom = CreateFreshRoom();

            bool connectResult = await freshRoom.ConnectAsync(credentials.Url, credentials.AuthToken, token, true);

            AttemptToConnectState connectionState = connectResult ? AttemptToConnectState.SUCCESS : AttemptToConnectState.ERROR;
            attemptToConnectState.Set(connectionState);

            if (connectResult == false)
            {
                ReportHub.LogWarning(ReportCategory.LIVEKIT, $"{logPrefix} - Cannot connect to room with url: {credentials.Url} with token: {credentials.AuthToken}");
                return RoomSelection.PREVIOUS;
            }

            // Always use new room for voice chat
            room.Assign(freshRoom, out IRoom _);
            SetRoomState(IConnectiveRoom.State.Running);
            ReportHub.Log(ReportCategory.LIVEKIT, $"{logPrefix} - Trying to connect to finished successfully {connectionString}");

            return RoomSelection.NEW;
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
                null
            );

            return new LogRoom(newRoom);
        }

        public static class Null
        {
            public static readonly IVoiceChatActivatableConnectiveRoom INSTANCE = new NullVoiceChatActivatableConnectiveRoom();
        }

        private class NullVoiceChatActivatableConnectiveRoom : IVoiceChatActivatableConnectiveRoom
        {
            public bool Activated { get; private set; } = true;
            public IConnectiveRoom.ConnectionLoopHealth CurrentConnectionLoopHealth => IConnectiveRoom.ConnectionLoopHealth.Stopped;
            public AttemptToConnectState AttemptToConnectState => AttemptToConnectState.NO_CONNECTION_REQUIRED;
            public IConnectiveRoom.State CurrentState() => IConnectiveRoom.State.Stopped;
            public IRoom Room() => NullRoom.INSTANCE;

            public UniTask<bool> StartAsync() => UniTask.FromResult(true);
            public UniTask StopAsync() => UniTask.CompletedTask;
            public void Dispose() { }

            public UniTask ActivateAsync() => UniTask.CompletedTask;
            public UniTask DeactivateAsync() => UniTask.CompletedTask;
            public UniTask<bool> TrySetConnectionStringAndActivateAsync(string newConnectionString) => UniTask.FromResult(true);
        }
    }
}
