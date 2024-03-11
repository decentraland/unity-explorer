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
        [SerializeField] public Transform emojiContainer;

        public event Action OnEmojiFirstOpen;

        private void Start()
        {
            OnEmojiFirstOpen?.Invoke();
        }
    }
}
