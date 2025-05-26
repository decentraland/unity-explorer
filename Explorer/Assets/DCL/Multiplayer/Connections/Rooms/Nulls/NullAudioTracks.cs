using LiveKit;
using LiveKit.Rooms;
using LiveKit.Rooms.Tracks;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullAudioTracks : IAudioTracks
    {
        public static readonly NullAudioTracks INSTANCE = new ();

        protected NullAudioTracks() { }

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
        public ITrack CreateAudioTrack(string name, RtcAudioSource source) => null!;


    }
}
