using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DCL.Emoji
{
    public class EmojiPanelController
    {
        public event Action<string> OnEmojiSelected;
        private readonly EmojiPanelView view;
        private readonly EmojiPanelConfigurationSO emojiPanelConfiguration;

        private readonly List<EmojiSectionView> emojiSectionViews = new ();

        public EmojiPanelController(EmojiPanelView view, EmojiPanelConfigurationSO emojiPanelConfiguration)
        {
            this.view = view;
            this.emojiPanelConfiguration = emojiPanelConfiguration;

            view.OnEmojiFirstOpen += ConfigureEmojiSectionSizes;
            ConfigureEmojiSections();
        }

        private void ConfigureEmojiSectionSizes()
        {
            foreach (EmojiSectionView emojiSectionView in emojiSectionViews)
                SetUiSizes(emojiSectionView);
        }

        private void ConfigureEmojiSections()
        {
            foreach (EmojiSection emojiSection in emojiPanelConfiguration.EmojiSections)
            {
                EmojiSectionView sectionView = Object.Instantiate(view.emojiSectionView, view.emojiContainer).GetComponent<EmojiSectionView>();
                sectionView.SectionTitle.text = emojiSection.title;
                GenerateEmojis(emojiSection.startHex, emojiSection.endHex, sectionView);
                emojiSectionViews.Add(sectionView);
                LayoutRebuilder.ForceRebuildLayoutImmediate(sectionView.EmojiContainer);
                LayoutRebuilder.ForceRebuildLayoutImmediate(sectionView.SectionRectTransform);
            }
        }

        private void SetUiSizes(EmojiSectionView sectionView)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(sectionView.EmojiContainer);
            sectionView.EmojiContainer.sizeDelta = new Vector2(sectionView.EmojiContainer.sizeDelta.x, LayoutUtility.GetPreferredHeight(sectionView.EmojiContainer));
            LayoutRebuilder.ForceRebuildLayoutImmediate(sectionView.SectionRectTransform);
            sectionView.SectionRectTransform.sizeDelta = new Vector2(sectionView.SectionRectTransform.sizeDelta.x, LayoutUtility.GetPreferredHeight(sectionView.SectionRectTransform));
        }

        private void GenerateEmojis(string hexRangeStart, string hexRageEnd, EmojiSectionView sectionView)
        {
            int startDec = int.Parse(hexRangeStart, System.Globalization.NumberStyles.HexNumber);
            int endDec = int.Parse(hexRageEnd, System.Globalization.NumberStyles.HexNumber);

            for (int i = 0; i < endDec-startDec; i++)
            {
                int emojiCode = startDec + i;
                string emojiChar = char.ConvertFromUtf32(emojiCode);
                EmojiButton emojiButton = Object.Instantiate(view.emojiButtonRef, sectionView.EmojiContainer);
                emojiButton.EmojiImage.text = emojiChar;
                emojiButton.Button.onClick.AddListener(() => OnEmojiSelected?.Invoke(emojiChar));
            }
        }
    }
}
