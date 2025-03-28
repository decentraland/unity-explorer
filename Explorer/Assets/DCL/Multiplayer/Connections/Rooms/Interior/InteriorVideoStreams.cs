using DCL.Multiplayer.Connections.Rooms.Nulls;
using LiveKit.Rooms.VideoStreaming;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Interior
{
    public class InteriorVideoStreams : IVideoStreams, IInterior<IVideoStreams>
    {
        private IVideoStreams assigned = NullVideoStreams.INSTANCE;

        public WeakReference<IVideoStream>? VideoStream(string identity, string sid) =>
            assigned.EnsureAssigned().VideoStream(identity, sid);

        public bool Release(IVideoStream videoStream) =>
            assigned.Release(videoStream);

        public void Free()
        {
            assigned.Free();
        }

        public void Assign(IVideoStreams value, out IVideoStreams? previous)
        {
            previous = assigned;
            assigned = value;

            previous = previous is NullVideoStreams ? null : previous;
        }
    }
}
