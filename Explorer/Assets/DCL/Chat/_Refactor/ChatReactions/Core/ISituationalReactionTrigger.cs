using UnityEngine;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Local UI reaction triggers: bursts, defaults, and streaming.
    /// Consumed by the reaction button / selector bar.
    /// </summary>
    public interface ISituationalReactionTrigger
    {
        void TriggerUIReaction(int emojiIndex, int count);
        void TriggerUIReactionFromRect(RectTransform sourceRect, int emojiIndex, int count);
        void TriggerDefaultUIReaction();
        void TriggerDefaultUIReactionFromRect(RectTransform sourceRect);
        void BeginUIStream(RectTransform sourceRect);
        void EndUIStream();
        void ToggleUIStream(RectTransform sourceRect);
    }
}
