using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;

namespace DCL.Multiplayer.Connections.Rooms
{
    public static class ParticipantExtensions
    {
        public static string ReadableString(this Participant participant) =>
            $"Participant {participant.Name} - {participant.Sid} - {participant.Identity} - {participant.Metadata} - {participant.Speaking} - {participant.AudioLevel} - {participant.ConnectionQuality}";

        public static string ToLivekitAddress(this Participant participant, TrackPublication trackPublication) =>
            ToLivekitAddress(participant.Identity, trackPublication);

        public static string ToLivekitAddress(this string participantIdentity, TrackPublication trackPublication) =>
            ToLivekitAddress(participantIdentity, trackPublication.Sid);

        public static string ToLivekitAddress(this string participantIdentity, string sid) =>
            $"livekit-video://{participantIdentity}/{sid}";
    }
}
