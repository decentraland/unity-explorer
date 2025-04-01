using DCL.Multiplayer.Connections.RoomHubs;
using LiveKit.Rooms;
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

        public bool MediaOpened => currentStream != null;

        public PlayerState State => playerState;

        public LivekitPlayer(IRoomHub roomHub)
        {
            room = roomHub.StreamingRoom();
        }

        public void OpenMedia(LivekitAddress livekitAddress)
        {
            CloseCurrentStream();

            switch (livekitAddress.StreamKind)
            {
                case LivekitAddress.Kind.CURRENT_STREAM:
                    //TODO
                    break;
                case LivekitAddress.Kind.USER_STREAM:
                    (string identity, string sid) = livekitAddress.UserStream;
                    currentStream = room.VideoStreams.VideoStream(identity, sid);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }

            playerState = PlayerState.PLAYING;
        }

        public void CloseCurrentStream()
        {
            if (currentStream?.TryGetTarget(out var stream) ?? false)
                stream.Dispose();

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
