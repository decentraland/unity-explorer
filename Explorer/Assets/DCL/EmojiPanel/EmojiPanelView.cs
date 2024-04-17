using System;
using System.Collections.Generic;
using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiPanelView : MonoBehaviour
    {
        public event Action<float, bool> OnSectionSelected;

        [field: SerializeField]
        public List<EmojiSectionToggle> EmojiSections { get; private set; }

        [field: SerializeField]
        public ScrollRect ScrollView { get; private set; }

        [field: SerializeField]
        public Transform EmojiContainer { get; private set; }

        [field: SerializeField]
        public Transform EmojiContainerScrollView { get; private set; }

        [field: SerializeField]
        public Transform EmojiSearchResults { get; private set; }

        [field: SerializeField]
        public Transform EmojiSearchedContent { get; private set; }

        [field: SerializeField]
        public SearchBarView SearchPanelView { get; private set; }

        public event Action OnEmojiFirstOpen;

        private void Start()
        {
            OnEmojiFirstOpen?.Invoke();

            foreach (EmojiSectionToggle emojiSectionToggle in EmojiSections)
                emojiSectionToggle.SectionToggle.onValueChanged.AddListener((isOn) => OnSectionSelected?.Invoke(emojiSectionToggle.SectionPosition, isOn));
        }
    }
}
