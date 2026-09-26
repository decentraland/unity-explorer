namespace DCL.Chat.ChatReactions.Networking
{
    /// <summary>
    /// Wire encoding for reaction add/remove over the network.
    /// A single int carries both the emoji index and the add/remove intent
    /// by using the sign bit via bitwise NOT (~).
    ///
    /// Encoding rules:
    ///   Add reaction:    wireValue = emojiIndex        (non-negative)
    ///   Remove reaction: wireValue = ~emojiIndex       (always negative, since ~n = -(n+1))
    ///
    /// Examples:
    ///   Encode(42, isRemoval: false) →  42             (add emoji 42)
    ///   Encode(42, isRemoval: true)  → -43  (~42)      (remove emoji 42)
    ///   Encode(0,  isRemoval: false) →  0              (add emoji 0)
    ///   Encode(0,  isRemoval: true)  → -1   (~0)       (remove emoji 0)
    ///
    ///   Decode(42)  → (emojiIndex: 42, isRemoval: false)
    ///   Decode(-43) → (emojiIndex: 42, isRemoval: true)    (~(-43) = 42)
    ///   Decode(0)   → (emojiIndex: 0,  isRemoval: false)
    ///   Decode(-1)  → (emojiIndex: 0,  isRemoval: true)    (~(-1)  = 0)
    ///
    /// This works because bitwise NOT is its own inverse: ~(~x) = x.
    /// Both sides of the protocol must use these methods so the convention
    /// is defined in exactly one place.
    /// </summary>
    internal static class ReactionWireEncoding
    {
        public static int Encode(int emojiIndex, bool isRemoval) =>
            isRemoval ? ~emojiIndex : emojiIndex;

        public static (int emojiIndex, bool isRemoval) Decode(int wireValue) =>
            wireValue < 0 ? (~wireValue, true) : (wireValue, false);
    }
}
