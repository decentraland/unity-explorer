using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatReactions
{
    /// <summary>
    /// Grouping view for the chat reaction button and its emoji picker popup.
    /// The picker panel sits above the button in the prefab hierarchy and is hidden by default.
    /// </summary>
    public class ChatReactionButtonView : MonoBehaviour
    {
        [field: SerializeField] public Button ReactionButton { get; private set; } = null!;
        [field: SerializeField] public RawImage EmojiIcon { get; private set; } = null!;
        [field: SerializeField] public SituationalReactionPickerView PickerView { get; private set; } = null!;

        public void SetEmoji(int atlasIndex, ChatReactionsAtlasConfig atlasConfig)
        {
            if (EmojiIcon == null) return;

            EmojiIcon.texture = atlasConfig.Atlas;
            EmojiIcon.uvRect = atlasConfig.GetUVRect(atlasIndex);
        }
    }
}