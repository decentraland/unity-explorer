using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.VideoStreaming;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullStreams<T, TInfo> : IStreams<T, TInfo> where T: class
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

        public void ListInfo(List<StreamInfo<TInfo>> output)
        {
            output.Clear();
        }
    }

    public class NullVideoStreams : NullStreams<IVideoStream, VideoStreamInfo>, IVideoStreams
    {
        public static readonly NullVideoStreams INSTANCE = new ();
    }

    public class NullAudioStreams : NullStreams<AudioStream, AudioStreamInfo>, IAudioStreams
    {
        public static readonly NullAudioStreams INSTANCE = new ();
    }
}
