using System;
using System.Collections.Generic;
using DCL.UI;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiPanelView : MonoBehaviour
    {
        public event Action<EmojiSectionName, bool> OnSectionSelected;
        [SerializeField] public List<EmojiSectionToggle> emojiSections;

        [SerializeField] public ScrollRect scrollView;
        [SerializeField] public Transform emojiContainer;
        [SerializeField] public Transform emojiContainerScrollView;
        [SerializeField] public Transform emojiSearchResults;
        [SerializeField] public Transform emojiSearchedContent;
        [SerializeField] public SearchBarView searchPanelView;

        public event Action OnEmojiFirstOpen;

        private void Start()
        {
            OnEmojiFirstOpen?.Invoke();

            foreach (EmojiSectionToggle emojiSectionToggle in emojiSections)
                emojiSectionToggle.SectionToggle.onValueChanged.AddListener((isOn) => OnSectionSelected?.Invoke(emojiSectionToggle.SectionName, isOn));
        }
    }
}
