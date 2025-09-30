using DCL.Multiplayer.Connections.Rooms.Nulls;
using LiveKit.Rooms;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms.VideoStreaming;
using RichTypes;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.Rooms.Interior
{
    public class InteriorVideoStreams : IVideoStreams, IInterior<IVideoStreams>
    {
        private IVideoStreams assigned = NullVideoStreams.INSTANCE;

        public Weak<IVideoStream> ActiveStream(StreamKey key) =>
            assigned.EnsureAssigned().ActiveStream(key);

        public bool Release(StreamKey videoStream) =>
            assigned.Release(videoStream);

        public void Free()
        {
            assigned.Free();
        }

        public void ListInfo(List<StreamInfo<VideoStreamInfo>> output)
        {
            assigned.ListInfo(output);
        }

        public void AssignRoom(Room room)
        {
            assigned.AssignRoom(room);
        }

        public void Assign(IVideoStreams value, out IVideoStreams? previous)
        {
            previous = assigned;
            assigned = value;

            previous = previous is NullVideoStreams ? null : previous;
        }

        public void Dispose()
        {
            assigned.Dispose();
        }
    }

    public class InteriorAudioStreams : IAudioStreams, IInterior<IAudioStreams>
    {
        private IAudioStreams assigned = NullAudioStreams.INSTANCE;

        public Weak<AudioStream> ActiveStream(StreamKey key) =>
            assigned.EnsureAssigned().ActiveStream(key);

        public bool Release(StreamKey stream) =>
            assigned.Release(stream);

        public void Free()
        {
            assigned.Free();
        }

        public void ListInfo(List<StreamInfo<AudioStreamInfo>> output)
        {
            assigned.ListInfo(output);
        }

        public void AssignRoom(Room room)
        {
            assigned.AssignRoom(room);
        }

        public void Assign(IAudioStreams value, out IAudioStreams? previous)
        {
            previous = assigned;
            assigned = value;

            previous = previous is NullAudioStreams ? null : previous;
        }

        public void Dispose()
        {
            assigned.Dispose();
        }
    }
}
