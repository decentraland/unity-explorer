using LiveKit.Rooms.VideoStreaming;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullVideoStreams : IVideoStreams
    {
        public static readonly NullVideoStreams INSTANCE = new ();

        private NullVideoStreams() { }

        public WeakReference<IVideoStream>? VideoStream(string identity, string sid) =>
            null;

        public void Free()
        {
            // Do nothing
        }
    }
}
