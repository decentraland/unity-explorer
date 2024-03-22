using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
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
        private readonly EmojiSearchController emojiSearchController;

        private readonly List<EmojiSectionView> emojiSectionViews = new ();
        public readonly Dictionary<string, EmojiData> EmojiNameMapping = new ();
        private readonly Dictionary<int, string> emojiValueMapping = new ();
        private readonly Dictionary<EmojiSectionName, RectTransform> sectionTransforms = new ();
        private readonly List<EmojiData> foundEmojis = new ();

        private CancellationTokenSource cts = new ();

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
            emojiSearchController = new EmojiSearchController(view.SearchPanelView, view.EmojiSearchedContent, emojiButtonPrefab);
            emojiSearchController.OnSearchTextChanged += OnSearchTextChanged;
            emojiSearchController.OnEmojiSelected += emoji => OnEmojiSelected?.Invoke(emoji);
            foreach (var emojiData in JsonConvert.DeserializeObject<Dictionary<string, string>>(emojiMappingJson.text))
            {
                EmojiNameMapping.Add(emojiData.Key, new EmojiData($"\\U000{emojiData.Value.ToUpper()}", emojiData.Key));
                emojiValueMapping.Add(int.Parse(emojiData.Value, System.Globalization.NumberStyles.HexNumber), emojiData.Key);
            }

            view.OnEmojiFirstOpen += ConfigureEmojiSectionSizes;
            ConfigureEmojiSections();
            view.OnSectionSelected += OnSectionSelected;
        }

        private void OnSearchTextChanged(string searchText)
        {
            view.EmojiContainerScrollView.gameObject.SetActive(string.IsNullOrEmpty(searchText));
            view.EmojiSearchResults.gameObject.SetActive(!string.IsNullOrEmpty(searchText));
            if (string.IsNullOrEmpty(searchText))
                return;
            cts.SafeCancelAndDispose();
            cts = new CancellationTokenSource();
            OnSearchTextChangedAsync(searchText, cts.Token).Forget();
        }

        private async UniTaskVoid OnSearchTextChangedAsync(string searchText, CancellationToken ct)
        {
            await DictionaryUtils.GetKeysContainingTextAsync(EmojiNameMapping, searchText, foundEmojis, ct);
            emojiSearchController.SetValues(foundEmojis);
        }

        private void OnSectionSelected(EmojiSectionName obj, bool isOn)
        {
            if (!isOn)
                return;

            view.ScrollView.normalizedPosition = new Vector2(0, 1 - Mathf.Clamp01(Mathf.Abs(sectionTransforms[obj].anchoredPosition.y) / view.ScrollView.content.rect.height));
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
                EmojiSectionView sectionView = Object.Instantiate(emojiSectionPrefab, view.EmojiContainer);
                sectionView.SectionTitle.text = emojiSection.title;
                foreach (SerializableKeyValuePair<string, string> range in emojiSection.ranges)
                {
                    GenerateEmojis(range.key, range.value, sectionView);
                }

                sectionView.SectionName = emojiSection.sectionName;
                emojiSectionViews.Add(sectionView);
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
                emojiButton.Button.onClick.AddListener(() => OnEmojiSelected?.Invoke(emojiButton.EmojiImage.text));

                if (emojiValueMapping.TryGetValue(emojiCode, out string emojiValue))
                    emojiButton.TooltipText.text = emojiValue;
                else
                    emojiButton.TooltipText.text = string.Empty;
            }
        }

        public void Dispose()
        {
            emojiSearchController.OnSearchTextChanged -= OnSearchTextChanged;
            view.OnEmojiFirstOpen -= ConfigureEmojiSectionSizes;
            view.OnSectionSelected -= OnSectionSelected;
            emojiSearchController?.Dispose();
        }
    }
}
