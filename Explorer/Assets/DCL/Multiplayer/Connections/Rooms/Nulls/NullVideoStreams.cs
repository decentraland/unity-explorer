using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.VideoStreaming;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullStreams<T> : IStreams<T> where T: class
    {
        protected NullStreams() { }

        public WeakReference<T>? ActiveStream(string identity, string sid) =>
            null;

        public bool Release(T stream)
        {
            // Do nothing
            return false;
        }

        public void Free()
        {
            // Do nothing
        }
    }

    public class NullVideoStreams : NullStreams<IVideoStream>, IVideoStreams
    {
        public static readonly NullVideoStreams INSTANCE = new ();
    }

    public class NullAudioStreams : NullStreams<IAudioStream>, IAudioStreams
    {
        public static readonly NullAudioStreams INSTANCE = new ();
    }
}
