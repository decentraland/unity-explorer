using LiveKit.Proto;
using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using System.Reflection;

namespace DCL.Tests.Editor
{
    internal static class LiveKitTestObjects
    {
        internal static readonly FieldInfo PARTICIPANT_INFO = typeof(LKParticipant).GetField("info", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly FieldInfo PUBLICATION_INFO = typeof(TrackPublication).GetField("info", BindingFlags.Instance | BindingFlags.NonPublic)!;

        internal static LKParticipant NewParticipant(string identity, string metadata = "")
        {
            var participant = new LKParticipant();
            PARTICIPANT_INFO.SetValue(participant, new ParticipantInfo { Identity = identity, Metadata = metadata });
            return participant;
        }

        internal static TrackPublication NewPublication(string sid, TrackKind kind, TrackSource source, string name = "", bool muted = false)
        {
            var publication = new TrackPublication();
            PUBLICATION_INFO.SetValue(publication, new TrackPublicationInfo { Sid = sid, Kind = kind, Source = source, Name = name, Muted = muted });
            return publication;
        }
    }
}
