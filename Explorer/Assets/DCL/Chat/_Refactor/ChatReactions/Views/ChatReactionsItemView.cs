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

        public event Action? OnClicked;

        public void Initialize(string emoji)
        {
            emojiLabel.text = emoji;
            button.onClick.AddListener(() => OnClicked?.Invoke());
        }
    }
}