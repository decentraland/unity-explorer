using LiveKit;
using LiveKit.Internal;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.Tracks;
using LiveKit.RtcSources.Video;
using System;

namespace DCL.Multiplayer.Connections.Rooms.Nulls
{
    public class NullLocalTracks : ILocalTracks
    {
        public static readonly NullLocalTracks INSTANCE = new ();

        public ITrack CreateAudioTrack(string name, IRtcAudioSource source) =>
            NullTrack.INSTANCE;

        public ITrack CreateVideoTrack(string name, RtcVideoSource source) =>
            NullTrack.INSTANCE;

        private class NullTrack : ITrack
        {
            public static readonly NullTrack INSTANCE = new ();

            public Origin Origin => Origin.Local;
            public string Sid => "NullTrack: Sid null";
            public string Name => "NullTrack: Name null";
            public TrackKind Kind => TrackKind.KindUnknown;
            public StreamState StreamState => StreamState.StateUnknown;
            public bool Muted => true;
            public WeakReference<IRoom> Room => NullRoom.WEAK_INSTANCE;
            public WeakReference<Participant> Participant => NullParticipantsHub.WEAK_NULL_PARTICIPANT;
            public FfiHandle? Handle => null;

            public void UpdateMuted(bool muted) { }
        }
    }
}
