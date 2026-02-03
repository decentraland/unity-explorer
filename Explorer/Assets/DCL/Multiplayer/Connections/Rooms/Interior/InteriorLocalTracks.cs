#if !UNITY_WEBGL

using DCL.Multiplayer.Connections.Rooms.Nulls;
using LiveKit;
using LiveKit.RtcSources.Video;
using LiveKit.Rooms;
using LiveKit.Rooms.Tracks;

namespace DCL.Multiplayer.Connections.Rooms.Interior
{
    public class InteriorLocalTracks : ILocalTracks, IInterior<ILocalTracks>
    {
        private ILocalTracks assigned = NullLocalTracks.INSTANCE;

        public ITrack CreateAudioTrack(string name, IRtcAudioSource source) =>
            assigned.EnsureAssigned().CreateAudioTrack(name, source);

        public ITrack CreateVideoTrack(string name, RtcVideoSource source) =>
            assigned.EnsureAssigned().CreateVideoTrack(name, source);

        public void Assign(ILocalTracks value, out ILocalTracks? previous)
        {
            previous = assigned;
            assigned = value;
            previous = previous is NullLocalTracks ? null : previous;
        }
    }
}

#endif
