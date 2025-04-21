using DCL.Multiplayer.Connections.Rooms.Nulls;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.VideoStreaming;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Interior
{
    public class InteriorVideoStreams : IVideoStreams, IInterior<IVideoStreams>
    {
        private IVideoStreams assigned = NullVideoStreams.INSTANCE;

        public WeakReference<IVideoStream>? ActiveStream(string identity, string sid) =>
            assigned.EnsureAssigned().ActiveStream(identity, sid);

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

    public class InteriorAudioStreams : IAudioStreams, IInterior<IAudioStreams>
    {
        private IAudioStreams assigned = NullAudioStreams.INSTANCE;

        public WeakReference<IAudioStream>? ActiveStream(string identity, string sid) =>
            assigned.EnsureAssigned().ActiveStream(identity, sid);

        public bool Release(IAudioStream stream) =>
            assigned.Release(stream);

        public void Free()
        {
            assigned.Free();
        }

        public void Assign(IAudioStreams value, out IAudioStreams? previous)
        {
            previous = assigned;
            assigned = value;

            previous = previous is NullAudioStreams ? null : previous;
        }
    }
}
