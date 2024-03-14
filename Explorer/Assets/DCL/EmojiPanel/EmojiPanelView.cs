using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiPanelView : MonoBehaviour
    {
        public event Action<EmojiSectionName, bool> OnSectionSelected;
        [SerializeField] public List<EmojiSectionToggle> emojiSections;

        [SerializeField] public ScrollRect scrollView;
        [SerializeField] public Transform emojiContainer;

        public event Action OnEmojiFirstOpen;

        private void Start()
        {
            OnEmojiFirstOpen?.Invoke();

            foreach (EmojiSectionToggle emojiSectionToggle in emojiSections)
            {
                emojiSectionToggle.SectionToggle.onValueChanged.AddListener((isOn) => OnSectionSelected?.Invoke(emojiSectionToggle.SectionName, isOn));
            }
        }
    }
}
