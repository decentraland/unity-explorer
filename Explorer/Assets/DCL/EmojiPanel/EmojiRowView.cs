using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Emoji
{
    public class EmojiRowView : MonoBehaviour
    {
        public const int EMOJIS_PER_ROW = 8;

        [SerializeField] private EmojiButton[] emojiButtons = { };

        public void Bind(
            IReadOnlyList<EmojiData> emojis,
            int startIndex,
            int count,
            Action<string> emojiSelected,
            Action<EmojiButton> emojiHovered,
            Action<EmojiButton> emojiUnhovered)
        {
            for (int i = 0; i < emojiButtons.Length; i++)
            {
                EmojiButton emojiButton = emojiButtons[i];

                if (i < count)
                {
                    EmojiData emojiData = emojis[startIndex + i];
                    emojiButton.SetValues(emojiData.EmojiCode, emojiData.EmojiName);
                    emojiButton.SetCallbacks(emojiSelected, emojiHovered, emojiUnhovered);
                }
                else
                    emojiButton.SetEmpty();
            }
        }
    }
}
