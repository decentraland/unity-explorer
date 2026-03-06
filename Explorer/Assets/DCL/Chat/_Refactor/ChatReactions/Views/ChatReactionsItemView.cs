using System;
using DCL.Chat.ChatReactions.Configs;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatReactions
{
    public sealed class ChatReactionItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private RawImage emojiImage;

        public int AtlasIndex { get; private set; }

        public event Action? OnClicked;

        public void Initialize(int atlasIndex, ChatReactionsAtlasConfig atlasConfig)
        {
            AtlasIndex = atlasIndex;

            emojiImage.texture = atlasConfig.Atlas;
            emojiImage.uvRect = atlasConfig.GetUVRect(atlasIndex);

            button.onClick.AddListener(() => OnClicked?.Invoke());
        }
    }
}