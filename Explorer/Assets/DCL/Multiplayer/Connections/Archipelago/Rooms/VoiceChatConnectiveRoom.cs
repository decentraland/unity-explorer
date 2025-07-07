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

        private string connectionString = string.Empty;

        private async UniTask<bool> TrySetConnectionStringAndActivateAsync(string newConnectionString)
        {
            try
            {
                if (CurrentState() is not IConnectiveRoom.State.Stopped)
                    await StopAsync();
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
                    await StartAsync();
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

        protected override UniTask PrewarmAsync(CancellationToken token) =>
            UniTask.CompletedTask;

        protected override async UniTask CycleStepAsync(CancellationToken token)
        {
            if (CurrentState() is not IConnectiveRoom.State.Running && connectionString != string.Empty)
            {
                await TryConnectToRoomAsync(connectionString, token);
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
            roomState.Set(IConnectiveRoom.State.Running);
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
                null!
            );

            return new LogRoom(newRoom);
        }

        public static class Null
        {
            public static readonly VoiceChatConnectiveRoom INSTANCE = new();
        }
    }
}
