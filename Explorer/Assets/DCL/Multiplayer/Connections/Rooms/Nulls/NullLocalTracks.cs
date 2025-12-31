using LiveKit;
using LiveKit.Rooms.Tracks;
using LiveKit.RtcSources.Video;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullLocalTracks : ILocalTracks
    {
        public static readonly NullLocalTracks INSTANCE = new ();

        private NullLocalTracks() { }

        public ITrack CreateAudioTrack(string name, IRtcAudioSource source) =>
            throw new NotSupportedException();

        public ITrack CreateVideoTrack(string name, RtcVideoSource source) =>
            throw new NotSupportedException();
    }
}
