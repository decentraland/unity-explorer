using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Input.Component;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Emoji
{
    public class EmojiPanelPresenter : IDisposable
    {
        private const int HEADER_ROW_PREFAB_INDEX = 0;
        private const int EMOJI_ROW_PREFAB_INDEX = 1;
        private const string SEARCH_RESULTS_HEADER = "SEARCH RESULTS";

        public event Action<string>? EmojiSelected;
        public event Action<bool>? PanelVisibilityChanged;

        private readonly EmojiMapping emojiMapping;
        private readonly EmojiPanelView view;
        private readonly EmojiPanelConfigurationSO emojiPanelConfiguration;
        private readonly IInputBlock? inputBlock;

        private readonly List<EmojiData> allEmojis = new ();
        private readonly List<EmojiData> searchResults = new ();
        private readonly List<EmojiPanelRowData> allRows = new ();
        private readonly List<EmojiPanelRowData> searchRows = new ();

        private IReadOnlyList<EmojiData> activeEmojis;
        private IReadOnlyList<EmojiPanelRowData> activeRows;

        private int[] sectionHeaderRowIndices = { };
        private CancellationTokenSource? searchCts = new ();

        private bool isSearchActive;
        private bool shortcutsBlocked;
        private bool isInitialized;

        public Transform PanelTransform => view.transform;

        public EmojiPanelPresenter(
            EmojiPanelView view,
            EmojiPanelConfigurationSO emojiPanelConfiguration,
            EmojiMapping emojiMapping,
            IInputBlock? inputBlock = null)
        {
            this.view = view;
            this.emojiPanelConfiguration = emojiPanelConfiguration;
            this.emojiMapping = emojiMapping;
            this.inputBlock = inputBlock;

            activeEmojis = allEmojis;
            activeRows = allRows;

            view.SetVisible(false);
        }

        public void SetPanelVisibility(bool isVisible)
        {
            if (isVisible == view.IsVisible)
                return;

            if (isVisible)
                EnsureInitialized();
            else
            {
                HideTooltip();
                view.BlurSearchInput();
                view.ClearSearchText();
            }

            view.SetVisible(isVisible);

            if (isVisible)
                view.FocusSearchInput();

            PanelVisibilityChanged?.Invoke(isVisible);
        }

        public void Dispose()
        {
            searchCts.SafeCancelAndDispose();
            searchCts = null;
            RestoreShortcuts();

            if (isInitialized)
            {
                view.SectionSelected -= OnSectionSelected;
                view.SearchTextChanged -= OnSearchTextChanged;
                view.SearchInputFocused -= BlockShortcuts;
                view.SearchInputBlurred -= RestoreShortcuts;
            }

            allEmojis.Clear();
            searchResults.Clear();
            allRows.Clear();
            searchRows.Clear();
        }

        private void EnsureInitialized()
        {
            if (isInitialized)
                return;

            isInitialized = true;

            BuildNormalRows(emojiPanelConfiguration);

            view.EmojiLoopList.InitListView(activeRows.Count, OnGetItemByIndex);
            view.SectionSelected += OnSectionSelected;
            view.SearchTextChanged += OnSearchTextChanged;
            view.SearchInputFocused += BlockShortcuts;
            view.SearchInputBlurred += RestoreShortcuts;
        }

        private void BuildNormalRows(EmojiPanelConfigurationSO emojiPanelConfiguration)
        {
            sectionHeaderRowIndices = new int[emojiPanelConfiguration.EmojiSections.Count];

            for (int sectionIndex = 0; sectionIndex < emojiPanelConfiguration.EmojiSections.Count; sectionIndex++)
            {
                EmojiSection emojiSection = emojiPanelConfiguration.EmojiSections[sectionIndex];

                sectionHeaderRowIndices[sectionIndex] = allRows.Count;
                allRows.Add(EmojiPanelRowData.Header(emojiSection.title));

                int sectionEmojiStartIndex = allEmojis.Count;

                for (int i = 0; i < emojiSection.emojis.Count; i++)
                {
                    SerializableKeyValuePair<string, int> kvp = emojiSection.emojis[i];
                    allEmojis.Add(new EmojiData(char.ConvertFromUtf32(kvp.value), kvp.key));
                }

                AddEmojiRows(allRows, sectionEmojiStartIndex, emojiSection.emojis.Count);
            }
        }

        private static void AddEmojiRows(List<EmojiPanelRowData> rows, int startIndex, int emojiCount)
        {
            int remainingEmojis = emojiCount;
            int rowStartIndex = startIndex;

            while (remainingEmojis > 0)
            {
                int rowEmojiCount = Math.Min(EmojiRowView.EMOJIS_PER_ROW, remainingEmojis);
                rows.Add(EmojiPanelRowData.EmojiRow(rowStartIndex, rowEmojiCount));

                rowStartIndex += rowEmojiCount;
                remainingEmojis -= rowEmojiCount;
            }
        }

        private LoopListViewItem2? OnGetItemByIndex(LoopListView2 listView, int index)
        {
            if (index < 0 || index >= activeRows.Count)
                return null;

            EmojiPanelRowData rowData = activeRows[index];

            switch (rowData.Type)
            {
                case EmojiPanelRowType.Header:
                {
                    LoopListViewItem2 item = listView.NewListViewItem(listView.ItemPrefabDataList[HEADER_ROW_PREFAB_INDEX].mItemPrefab.name);
                    EmojiSectionHeaderView headerView = item.GetComponent<EmojiSectionHeaderView>();
                    headerView.SetTitle(rowData.HeaderTitle);
                    return item;
                }
                case EmojiPanelRowType.Emoji:
                {
                    LoopListViewItem2 item = listView.NewListViewItem(listView.ItemPrefabDataList[EMOJI_ROW_PREFAB_INDEX].mItemPrefab.name);
                    EmojiRowView rowView = item.GetComponent<EmojiRowView>();
                    rowView.Bind(activeEmojis, rowData.EmojiStartIndex, rowData.EmojiCount, OnEmojiSelected, OnEmojiHovered, OnEmojiUnhovered);
                    return item;
                }
                default:
                    return null;
            }
        }

        private void OnSectionSelected(int sectionIndex, bool isOn)
        {
            if (!isOn)
                return;

            if (sectionIndex < 0 || sectionIndex >= sectionHeaderRowIndices.Length)
                return;

            if (isSearchActive)
                view.ClearSearchText();

            HideTooltip();
            view.EmojiLoopList.MovePanelToItemIndex(sectionHeaderRowIndices[sectionIndex], 0f);
        }

        private void OnSearchTextChanged(string searchText)
        {
            HideTooltip();

            isSearchActive = !string.IsNullOrEmpty(searchText);

            searchCts = searchCts.SafeRestart();

            if (!isSearchActive)
            {
                activeEmojis = allEmojis;
                activeRows = allRows;
                RefreshList(resetPosition: true);
                return;
            }

            SearchTextChangedAsync(searchText, searchCts.Token).Forget();
        }

        private async UniTaskVoid SearchTextChangedAsync(string searchText, CancellationToken ct)
        {
            try
            {
                searchResults.Clear();
                await DictionaryUtils.GetKeysContainingTextAsync(emojiMapping.NameMapping, searchText, searchResults, ct);

                if (ct.IsCancellationRequested)
                    return;

                searchRows.Clear();
                searchRows.Add(EmojiPanelRowData.Header(SEARCH_RESULTS_HEADER));
                AddEmojiRows(searchRows, 0, searchResults.Count);

                activeEmojis = searchResults;
                activeRows = searchRows;
                RefreshList(resetPosition: true);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.UI); }
        }

        private void RefreshList(bool resetPosition)
        {
            HideTooltip();
            view.EmojiLoopList.SetListItemCount(activeRows.Count, resetPosition);
            view.EmojiLoopList.RefreshAllShownItem();
        }

        private void BlockShortcuts()
        {
            if (inputBlock == null || shortcutsBlocked) return;

            shortcutsBlocked = true;
            inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);
        }

        private void RestoreShortcuts()
        {
            if (inputBlock == null || !shortcutsBlocked) return;

            shortcutsBlocked = false;
            inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA);
        }

        private void OnEmojiSelected(string code)
        {
            HideTooltip();
            EmojiSelected?.Invoke(code);
        }

        private void OnEmojiHovered(EmojiButton emojiButton) =>
            view.TooltipView.Show(emojiButton);

        private void OnEmojiUnhovered(EmojiButton emojiButton) =>
            view.TooltipView.Hide(emojiButton);

        private void HideTooltip() =>
            view.TooltipView.ForceHide();
    }
}
