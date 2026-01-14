using LiveKit;
using LiveKit.Rooms;
using LiveKit.Rooms.Tracks;
using LiveKit.RtcSources.Video;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullLocalTracks : ILocalTracks
    {
        public static readonly NullLocalTracks INSTANCE = new ();

        protected NullLocalTracks() { }

        public bool Release()
        {
            // Do nothing
            return false;
        }

        public void Free()
        {
            // Do nothing
        }

        // Do nothing
        public ITrack CreateAudioTrack(string name, IRtcAudioSource source) => null!;

        public ITrack CreateVideoTrack(string name, RtcVideoSource source) => null!;
    }
}
