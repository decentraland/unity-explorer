using System;
using System.Collections.Generic;
using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Emoji
{
    public class EmojiPanelView : MonoBehaviour
    {
        public event Action<float, bool> SectionSelected;

        public event Action EmojiFirstOpen;

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

        private void Start()
        {
            EmojiFirstOpen?.Invoke();

            foreach (EmojiSectionToggle emojiSectionToggle in EmojiSections)
                emojiSectionToggle.SectionToggle.onValueChanged.AddListener((isOn) => SectionSelected?.Invoke(emojiSectionToggle.SectionPosition, isOn));
        }
    }
}
