using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace DCL.Emoji
{
    public class EmojiPanelView : MonoBehaviour
    {
        [SerializeField] private EmojiButton emojiButtonRef;
        [SerializeField] private Transform emojiContainer;
        public string hexRangeStart = "1F600"; // HEX code of the starting emoji
        public string hexRageEnd = "1F604"; // HEX code of the ending emoji

        public event Action<string> OnEmojiSelected;

        private void Start()
        {
            GenerateEmojis();
        }

        private void GenerateEmojis()
        {
            int startDec = int.Parse(hexRangeStart, System.Globalization.NumberStyles.HexNumber);
            int endDec = int.Parse(hexRageEnd, System.Globalization.NumberStyles.HexNumber);

            for (int i = 0; i < endDec-startDec; i++)
            {
                int emojiCode = startDec + i;
                string emojiChar = char.ConvertFromUtf32(emojiCode);
                EmojiButton emojiButton = Instantiate(emojiButtonRef, emojiContainer);
                emojiButton.EmojiImage.text = emojiChar;
                emojiButton.Button.onClick.AddListener(() => OnEmojiSelected?.Invoke(emojiChar));
            }
        }
    }
}
