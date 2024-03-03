using LiveKit.Rooms.Participants;

namespace DCL.Multiplayer.Connections.Rooms
{
    public static class ParticipantExtensions
    {
        public static string ReadableString(this Participant participant) =>
            $"Participant {participant.Name} - {participant.Sid} - {participant.Identity} - {participant.Metadata} - {participant.Speaking} - {participant.AudioLevel} - {participant.ConnectionQuality}";
    }
}
