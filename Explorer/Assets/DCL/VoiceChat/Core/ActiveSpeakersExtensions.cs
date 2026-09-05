using LiveKit.Rooms.ActiveSpeakers;

namespace DCL.VoiceChat
{
    public static class ActiveSpeakersExtensions
    {
        /// <summary>
        ///     Allocation-free check whether <paramref name="identity"/> is among the current active speakers.
        /// </summary>
        public static bool Contains(this IActiveSpeakers speakers, string identity)
        {
            foreach (string speakerId in speakers)
            {
                if (speakerId == identity)
                    return true;
            }

            return false;
        }
    }
}
