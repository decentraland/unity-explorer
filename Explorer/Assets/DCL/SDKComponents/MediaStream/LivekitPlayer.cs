using DCL.Multiplayer.Connections.RoomHubs;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.TrackPublications;
using LiveKit.Rooms.VideoStreaming;
using System;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    public class LivekitPlayer : IDisposable
    {
        private readonly IRoom room;
        private WeakReference<IVideoStream>? currentStream;
        private PlayerState playerState;
        private LivekitAddress? playingAddress;

        public bool MediaOpened => currentStream != null;

        public PlayerState State => playerState;

        public LivekitPlayer(IRoomHub roomHub)
        {
            room = roomHub.StreamingRoom();
        }

        public void EnsurePlaying()
        {
            if (this is { MediaOpened: false, State: PlayerState.PLAYING, playingAddress: { } address })
                OpenMedia(address);
        }

        public void OpenMedia(LivekitAddress livekitAddress)
        {
            CloseCurrentStream();

            switch (livekitAddress.StreamKind)
            {
                case LivekitAddress.Kind.CURRENT_STREAM:
                    var firstTrack = FirstAvailableTrack();

                    if (firstTrack.HasValue)
                        currentStream = room.VideoStreams.VideoStream(firstTrack.Value.identity, firstTrack.Value.sid);

                    break;
                case LivekitAddress.Kind.USER_STREAM:
                    (string identity, string sid) = livekitAddress.UserStream;
                    currentStream = room.VideoStreams.VideoStream(identity, sid);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }

            playerState = PlayerState.PLAYING;
            playingAddress = livekitAddress;
        }

        private (string identity, string sid)? FirstAvailableTrack()
        {
            foreach (string remoteParticipantIdentity in room.Participants.RemoteParticipantIdentities())
            {
                var participant = room.Participants.RemoteParticipant(remoteParticipantIdentity);

                if (participant == null)
                    continue;

                foreach ((string sid, TrackPublication value) in participant.Tracks)
                    if (value.Kind is TrackKind.KindVideo)
                        return (remoteParticipantIdentity, sid);
            }

            return null;
        }

        public void CloseCurrentStream()
        {
            // doesn't need to dispose the stream, because it's responsibility of the owning room
            currentStream = null;
            playerState = PlayerState.STOPPED;
        }

        public Texture? LastTexture()
        {
            if (playerState is not PlayerState.PLAYING)
                return null;

            return currentStream?.TryGetTarget(out var videoStream) ?? false
                ? videoStream.DecodeLastFrame()
                : null;
        }

        public void Dispose()
        {
            CloseCurrentStream();
        }

        public void Play()
        {
            playerState = PlayerState.PLAYING;
        }

        public void Pause()
        {
            playerState = PlayerState.PAUSED;
        }

        public void Stop()
        {
            playerState = PlayerState.STOPPED;
        }
    }
}
