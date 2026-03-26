using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Target for reactions received from remote players via the network bus.
    /// </summary>
    public interface IRemoteReactionTarget
    {
        void HandleRemoteReaction(ReactionReceivedArgs args);

        void TriggerWorldReaction(Vector3 worldPos, int emojiIndex, int count);
        void TriggerWorldReactionForAvatar(string walletId, int emojiIndex, int count);
        void TriggerRemoteUIReaction(int emojiIndex, int count);
    }
}
