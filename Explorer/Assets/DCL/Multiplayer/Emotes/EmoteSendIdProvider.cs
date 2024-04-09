using CommunicationData.URLHelpers;

namespace DCL.Multiplayer.Emotes
{
    /// <summary>
    ///     Increments an ID and reuses it if the emote is looping
    /// </summary>
    public struct EmoteSendIdProvider
    {
        private uint incrementalId;
        private URN currentURN;

        public uint GetNextID(URN emoteURN, bool loopCyclePassed)
        {
            if (loopCyclePassed && currentURN.Equals(emoteURN))
                return incrementalId;

            currentURN = emoteURN;
            incrementalId++;

            return incrementalId;
        }
    }
}
