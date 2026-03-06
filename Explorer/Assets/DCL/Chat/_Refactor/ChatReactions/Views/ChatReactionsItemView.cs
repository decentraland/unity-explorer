using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DCL.Chat.ChatReactions
{
    public sealed class ChatReactionItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text emojiLabel;

        public int AtlasIndex { get; private set; }

        public event Action? OnClicked;

        public void Initialize(int atlasIndex)
        {
            AtlasIndex = atlasIndex;
            emojiLabel.text = atlasIndex.ToString();
            button.onClick.AddListener(() => OnClicked?.Invoke());
        }
    }
}
