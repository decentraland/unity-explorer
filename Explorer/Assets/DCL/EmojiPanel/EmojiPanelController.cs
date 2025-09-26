using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.Emoji
{
    public class EmojiPanelController
    {
        public event Action<string> EmojiSelected;

        public readonly EmojiMapping EmojiMapping;

        private readonly EmojiPanelView view;
        private readonly EmojiPanelConfigurationSO emojiPanelConfiguration;
        private readonly EmojiButton emojiButtonPrefab;
        private readonly EmojiSectionView emojiSectionPrefab;
        private readonly EmojiSearchController emojiSearchController;

        private readonly List<EmojiSectionView> emojiSectionViews = new ();
        private readonly Dictionary<string, RectTransform> sectionTransforms = new ();
        private readonly List<EmojiData> foundEmojis = new ();

        private CancellationTokenSource cts = new ();

        public EmojiPanelController(
            EmojiPanelView view,
            EmojiPanelConfigurationSO emojiPanelConfiguration,
            EmojiMapping emojiMapping,
            EmojiSectionView emojiSectionPrefab,
            EmojiButton emojiButtonPrefab)
        {
            this.view = view;
            this.emojiPanelConfiguration = emojiPanelConfiguration;
            this.emojiSectionPrefab = emojiSectionPrefab;
            this.emojiButtonPrefab = emojiButtonPrefab;
            EmojiMapping = emojiMapping;
            emojiSearchController = new EmojiSearchController(view.SearchPanelView, view.EmojiSearchedContent, emojiButtonPrefab);
            emojiSearchController.SearchTextChanged += OnSearchTextChanged;
            emojiSearchController.EmojiSelected += emoji => EmojiSelected?.Invoke(emoji);

            view.EmojiFirstOpen += ConfigureEmojiSectionSizes;
            ConfigureEmojiSections();
            view.SectionSelected += OnSectionSelected;
        }

        [Obsolete("This constructor is obsolete, use the one that takes an EmojiMapping object instead.")]
        public EmojiPanelController(
            EmojiPanelView view,
            EmojiPanelConfigurationSO emojiPanelConfiguration,
            EmojiSectionView emojiSectionPrefab,
            EmojiButton emojiButtonPrefab) : this(view, emojiPanelConfiguration, new EmojiMapping(emojiPanelConfiguration), emojiSectionPrefab, emojiButtonPrefab) { }

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
            // Uses the new EmojiMapping class, as per the July 17th refactor
            await DictionaryUtils.GetKeysContainingTextAsync(EmojiMapping.NameMapping, searchText, foundEmojis, ct);
            emojiSearchController.SetValues(foundEmojis);
        }

        private void OnSectionSelected(float sectionPosition, bool isOn)
        {
            if (!isOn)
                return;

            view.ScrollView.normalizedPosition = new Vector2(0, sectionPosition);
        }

        public void SetPanelVisibility(bool isVisible)
        {
            view.gameObject.SetActive(isVisible);
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
                EmojiSectionView sectionView = Object.Instantiate(emojiSectionPrefab, view.EmojiContainer);
                sectionView.SectionTitle.text = emojiSection.title;
                GenerateEmojis(emojiSection.emojis, sectionView);

                emojiSectionViews.Add(sectionView);
            }
        }

        private void SetUiSizes(EmojiSectionView sectionView)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(sectionView.EmojiContainer);
            sectionView.EmojiContainer.sizeDelta = new Vector2(sectionView.EmojiContainer.sizeDelta.x, LayoutUtility.GetPreferredHeight(sectionView.EmojiContainer));
            LayoutRebuilder.ForceRebuildLayoutImmediate(sectionView.SectionRectTransform);
            sectionView.SectionRectTransform.sizeDelta = new Vector2(sectionView.SectionRectTransform.sizeDelta.x, LayoutUtility.GetPreferredHeight(sectionView.SectionRectTransform));
            sectionTransforms.Add(sectionView.SectionTitle.text, sectionView.SectionRectTransform);
        }

        private void GenerateEmojis(List<SerializableKeyValuePair<string, int>> emojis, EmojiSectionView sectionView)
        {
            foreach (var kvp in emojis)
            {
                EmojiButton emojiButton = Object.Instantiate(emojiButtonPrefab, sectionView.EmojiContainer);
                emojiButton.EmojiImage.text = char.ConvertFromUtf32(kvp.value);
                emojiButton.TooltipText.text = kvp.key;
                emojiButton.EmojiSelected += OnEmojiSelected;
            }
        }

        private void OnEmojiSelected(string code)
        {
            EmojiSelected?.Invoke(code);
        }

        public void Dispose()
        {
            emojiSearchController.SearchTextChanged -= OnSearchTextChanged;
            view.EmojiFirstOpen -= ConfigureEmojiSectionSizes;
            view.SectionSelected -= OnSectionSelected;
            emojiSearchController?.Dispose();

            foreach (EmojiSectionView sectionView in emojiSectionViews)
            {
                foreach (Transform emojiButtonTransform in sectionView.EmojiContainer)
                {
                    UnityObjectUtils.SafeDestroy(emojiButtonTransform.gameObject);
                }

                UnityObjectUtils.SafeDestroy(sectionView.gameObject);
            }

            emojiSectionViews.Clear();
            sectionTransforms.Clear();
            foundEmojis.Clear();
        }
    }
}
