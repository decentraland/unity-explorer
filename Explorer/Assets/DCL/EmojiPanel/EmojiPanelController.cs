using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Emoji
{
    public class EmojiPanelController
    {
        public event Action<string> OnEmojiSelected;
        private readonly EmojiPanelView view;
        private readonly EmojiPanelConfigurationSO emojiPanelConfiguration;
        private readonly EmojiButton emojiButtonPrefab;
        private readonly EmojiSectionView emojiSectionPrefab;

        private readonly List<EmojiSectionView> emojiSectionViews = new ();
        public readonly Dictionary<string, EmojiData> emojiNameMapping = new ();
        private readonly Dictionary<string, string> emojiValueMapping = new ();
        private readonly Dictionary<EmojiSectionName, RectTransform> sectionTransforms = new ();

        private int startDec;
        private int endDec;
        private int emojiCode;
        private string emojiChar;

        public EmojiPanelController(
            EmojiPanelView view,
            EmojiPanelConfigurationSO emojiPanelConfiguration,
            TextAsset emojiMappingJson,
            EmojiSectionView emojiSectionPrefab,
            EmojiButton emojiButtonPrefab)
        {
            this.view = view;
            this.emojiPanelConfiguration = emojiPanelConfiguration;
            this.emojiSectionPrefab = emojiSectionPrefab;
            this.emojiButtonPrefab = emojiButtonPrefab;

            foreach (var emojiData in JsonConvert.DeserializeObject<Dictionary<string, string>>(emojiMappingJson.text))
            {
                emojiNameMapping.Add(emojiData.Key, new EmojiData($"\\U000{emojiData.Value.ToUpper()}", emojiData.Key));
                emojiValueMapping.Add(emojiData.Value.ToUpper(), emojiData.Key);
            }

            view.OnEmojiFirstOpen += ConfigureEmojiSectionSizes;
            ConfigureEmojiSections();
            view.OnSectionSelected += OnSectionSelected;
        }

        private void OnSectionSelected(EmojiSectionName obj, bool isOn)
        {
            if (!isOn)
                return;

            view.scrollView.normalizedPosition = new Vector2(0, 1 - Mathf.Clamp01(Mathf.Abs(sectionTransforms[obj].anchoredPosition.y) / view.scrollView.content.rect.height));
        }

        public void SetPanelVisibility(bool isVisible) =>
            view.gameObject.SetActive(isVisible);

        private void ConfigureEmojiSectionSizes()
        {
            foreach (EmojiSectionView emojiSectionView in emojiSectionViews)
                SetUiSizes(emojiSectionView);
        }

        private void ConfigureEmojiSections()
        {
            foreach (EmojiSection emojiSection in emojiPanelConfiguration.EmojiSections)
            {
                EmojiSectionView sectionView = Object.Instantiate(emojiSectionPrefab, view.emojiContainer).GetComponent<EmojiSectionView>();
                sectionView.SectionTitle.text = emojiSection.title;
                foreach (SerializableKeyValuePair<string, string> range in emojiSection.ranges)
                {
                    GenerateEmojis(range.key, range.value, sectionView);
                }

                sectionView.SectionName = emojiSection.sectionName;
                emojiSectionViews.Add(sectionView);
                LayoutRebuilder.ForceRebuildLayoutImmediate(sectionView.EmojiContainer);
                LayoutRebuilder.ForceRebuildLayoutImmediate(sectionView.SectionRectTransform);
            }
        }

        //This function is called at the first panel open, necessary due to the need of having the gameobject active to
        //calculate the proper sizing and positions
        private void SetUiSizes(EmojiSectionView sectionView)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(sectionView.EmojiContainer);
            sectionView.EmojiContainer.sizeDelta = new Vector2(sectionView.EmojiContainer.sizeDelta.x, LayoutUtility.GetPreferredHeight(sectionView.EmojiContainer));
            LayoutRebuilder.ForceRebuildLayoutImmediate(sectionView.SectionRectTransform);
            sectionView.SectionRectTransform.sizeDelta = new Vector2(sectionView.SectionRectTransform.sizeDelta.x, LayoutUtility.GetPreferredHeight(sectionView.SectionRectTransform));
            sectionTransforms.Add(sectionView.SectionName, sectionView.SectionRectTransform);
        }

        private void GenerateEmojis(string hexRangeStart, string hexRageEnd, EmojiSectionView sectionView)
        {
            startDec = int.Parse(hexRangeStart, System.Globalization.NumberStyles.HexNumber);
            endDec = int.Parse(hexRageEnd, System.Globalization.NumberStyles.HexNumber);

            for (int i = 0; i < endDec-startDec; i++)
            {
                emojiCode = startDec + i;
                emojiChar = char.ConvertFromUtf32(emojiCode);
                EmojiButton emojiButton = Object.Instantiate(emojiButtonPrefab, sectionView.EmojiContainer);
                emojiButton.EmojiImage.text = emojiChar;
                emojiButton.Button.onClick.AddListener(() => OnEmojiSelected?.Invoke(emojiChar));

                if (emojiValueMapping.TryGetValue(emojiCode.ToString("X"), out string emojiValue))
                    emojiButton.TooltipText.text = emojiValue;
                else
                    emojiButton.TooltipText.text = string.Empty;
            }
        }
    }
}
