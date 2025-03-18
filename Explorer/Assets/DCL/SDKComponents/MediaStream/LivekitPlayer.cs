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

        public void OpenMedia(string identity, string sid)
        {
            CloseCurrentStream();
            currentStream = room.VideoStreams.VideoStream(identity, sid);
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
