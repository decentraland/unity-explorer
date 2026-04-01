namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Wire encoding for reaction add/remove over the network.
    /// Positive emojiIndex = add reaction, bitwise-NOT (~emojiIndex) = remove reaction.
    /// Both sides of the protocol (sender and receiver) must use these methods
    /// so the convention is defined in exactly one place.
    /// </summary>
    internal static class ReactionWireEncoding
    {
        public static int Encode(int emojiIndex, bool isRemoval) =>
            isRemoval ? ~emojiIndex : emojiIndex;

        public static (int emojiIndex, bool isRemoval) Decode(int wireValue) =>
            wireValue < 0 ? (~wireValue, true) : (wireValue, false);
    }
}
