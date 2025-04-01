using LiveKit.Rooms.Participants;
using LiveKit.Rooms.TrackPublications;
using System.Diagnostics.CodeAnalysis;

namespace DCL.Multiplayer.Connections.Rooms
{
    public static class ParticipantExtensions
    {
        /// <summary>
        /// Contract predefined value
        /// </summary>
        public const string LIVEKIT_CURRENT_STREAM = "livekit-video://current-stream";

        public static string ReadableString(this Participant participant) =>
            $"Participant {participant.Name} - {participant.Sid} - {participant.Identity} - {participant.Metadata} - {participant.Speaking} - {participant.AudioLevel} - {participant.ConnectionQuality}";

        public static string ToLivekitAddress(this Participant participant, TrackPublication trackPublication) =>
            ToLivekitAddress(participant.Identity, trackPublication);

        public static string ToLivekitAddress(this string participantIdentity, TrackPublication trackPublication) =>
            ToLivekitAddress(participantIdentity, trackPublication.Sid);

        public static string ToLivekitAddress(this string participantIdentity, string sid) =>
            $"livekit-video://{participantIdentity}/{sid}";

        [SuppressMessage("ReSharper", "StringStartsWithIsCultureSpecific")]
        public static bool IsLivekitAddress(this string address) =>
            address.StartsWith("livekit-video://");

        public static (string identity, string sid) DeconstructLivekitAddress(this string address)
        {
            string[]? parts = address.Split('/');
            return (parts![2], parts[3]);
        }
    }
}
