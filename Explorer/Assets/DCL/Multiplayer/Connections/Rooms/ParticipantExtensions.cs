using LiveKit.Rooms.Participants;

#if !UNITY_WEBGL
using LiveKit.Rooms.TrackPublications;
#endif

namespace DCL.Multiplayer.Connections.Rooms
{
    public static class ParticipantExtensions
    {
        public static string ReadableString(this Participant participant) =>
#if !UNITY_WEBGL
            $"Participant {participant.Name} - {participant.Sid} - {participant.Identity} - {participant.Metadata} - {participant.Speaking} - {participant.AudioLevel} - {participant.ConnectionQuality}";
#else
            $"Participant {participant.Identity}";
#endif

#if !UNITY_WEBGL
        public static string ToLivekitAddress(this Participant participant, TrackPublication trackPublication) =>
            ToLivekitAddress(participant.Identity, trackPublication);

        public static string ToLivekitAddress(this string participantIdentity, TrackPublication trackPublication) =>
            ToLivekitAddress(participantIdentity, trackPublication.Sid);
#endif

        public static string ToLivekitAddress(this string participantIdentity, string sid) =>
            $"livekit-video://{participantIdentity}/{sid}";
    }
}
