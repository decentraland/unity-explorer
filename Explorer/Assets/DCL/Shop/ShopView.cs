using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.UI;
using DCL.UI.Credits;
using DG.Tweening;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Shop
{
    public class ShopView : MonoBehaviour
    {
        private const string BADGE_OVERFLOW = "99+";
        private const int BADGE_MAX = 99;
        private const int SEARCH_DEBOUNCE_MS = 500;
        private const float CART_BUMP_SCALE = 0.25f;
        private const float CART_BUMP_DURATION = 0.3f;

        [field: Header("Header")]
        [field: SerializeField] public SectionTab[] SectionTabs { get; private set; } = Array.Empty<SectionTab>();
        [field: SerializeField] public SearchBarView SearchBar { get; private set; } = null!;
        [field: SerializeField] public DropdownView SortDropdown { get; private set; } = null!;
        [field: SerializeField] public CreditsPanelView CreditsPanel { get; private set; } = null!;
        [field: SerializeField] public Button CartButton { get; private set; } = null!;
        [field: SerializeField] public GameObject CartBadge { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CartBadgeText { get; private set; } = null!;

        [field: Header("Columns")]
        [field: SerializeField] public CharacterPreviewView CharacterPreview { get; private set; } = null!;
        [field: SerializeField] public RectTransform Main { get; private set; } = null!;
        [field: SerializeField] public ShopItemPanelView ItemPanel { get; private set; } = null!;
        [field: SerializeField] public float ItemPanelInset { get; private set; } = 398f;

        [field: Header("Sections")]
        [field: SerializeField] public ShopOverviewView OverviewView { get; private set; } = null!;
        [field: SerializeField] public ShopGridSectionView WearablesView { get; private set; } = null!;
        [field: SerializeField] public ShopGridSectionView EmotesView { get; private set; } = null!;
        [field: SerializeField] public GameObject NamesRoot { get; private set; } = null!;
        [field: SerializeField] public GameObject LandsRoot { get; private set; } = null!;
        [field: SerializeField] public ShopGridSectionView FavoritesView { get; private set; } = null!;

        [field: Header("Animators")]
        [field: SerializeField] public Animator PanelAnimator { get; private set; } = null!;
        [field: SerializeField] public Animator HeaderAnimator { get; private set; } = null!;

        private CancellationTokenSource? searchCts;
        private Tweener? cartBumpTween;
        private float mainRightInset;

        public bool IsSearchBarFocused => SearchBar.inputField.isFocused;

        public event Action<ShopSection>? SectionTabClicked;
        public event Action? CartButtonClicked;
        public event Action? SearchBarSelected;
        public event Action? SearchBarDeselected;
        public event Action<string>? SearchChanged;
        public event Action<ShopSortOption>? SortChanged;

        private void Awake()
        {
            foreach (SectionTab tab in SectionTabs)
            {
                ShopSection section = tab.Section;
                tab.Tab.Button.onClick.AddListener(() => SectionTabClicked?.Invoke(section));
            }

            CartButton.onClick.AddListener(() => CartButtonClicked?.Invoke());
            SearchBar.inputField.onValueChanged.AddListener(OnSearchValueChanged);
            SearchBar.inputField.onSubmit.AddListener(OnSearchSubmitted);
            SearchBar.clearSearchButton.onClick.AddListener(OnSearchCleared);
            SearchBar.inputField.onSelect.AddListener(_ => SearchBarSelected?.Invoke());
            SearchBar.inputField.onDeselect.AddListener(_ => SearchBarDeselected?.Invoke());
            SortDropdown.Dropdown.onValueChanged.AddListener(index => SortChanged?.Invoke((ShopSortOption)index));
            mainRightInset = -Main.offsetMax.x;
        }

        private void OnDisable() =>
            ResetCartBump();

        private void OnDestroy() =>
            searchCts.SafeCancelAndDispose();

        public void SetViewActive(bool isActive) =>
            gameObject.SetActive(isActive);

        public void PlayAnimator(int triggerId)
        {
            PanelAnimator.SetTrigger(triggerId);
            HeaderAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            PanelAnimator.Rebind();
            HeaderAnimator.Rebind();
            PanelAnimator.Update(0);
            HeaderAnimator.Update(0);
        }

        /// <summary>
        ///     Selects the tab and shows the empty roots; the overview and grid sections are shown by their controllers.
        /// </summary>
        public void ShowSection(ShopSection section)
        {
            foreach (SectionTab tab in SectionTabs)
                tab.Tab.SetSelected(tab.Section == section);

            NamesRoot.SetActive(section == ShopSection.Names);
            LandsRoot.SetActive(section == ShopSection.Lands);
        }

        public void SetSectionTabVisible(ShopSection section, bool visible)
        {
            foreach (SectionTab tab in SectionTabs)
            {
                if (tab.Section == section)
                    tab.Tab.gameObject.SetActive(visible);
            }
        }

        public void SetItemPanelOpen(bool open)
        {
            ItemPanel.gameObject.SetActive(open);
            Main.offsetMax = new Vector2(-(mainRightInset + (open ? ItemPanelInset : 0f)), Main.offsetMax.y);
        }

        public void SetSearchAndSortVisible(bool visible)
        {
            SearchBar.gameObject.SetActive(visible);
            SortDropdown.gameObject.SetActive(visible);
        }

        public void SetSort(ShopSortOption sort) =>
            SortDropdown.Dropdown.SetValueWithoutNotify((int)sort);

        /// <summary>Sets the search text without raising SearchChanged.</summary>
        public void SetSearchText(string text)
        {
            searchCts.SafeCancelAndDispose();
            searchCts = null;
            SearchBar.inputField.SetTextWithoutNotify(text);
            SearchBar.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        public void SetCartBadge(int units)
        {
            CartBadge.SetActive(units > 0);
            CartBadgeText.text = units > BADGE_MAX ? BADGE_OVERFLOW : units.ToString();
        }

        public void PlayCartBump()
        {
            ResetCartBump();
            cartBumpTween = CartButton.transform.DOPunchScale(new Vector3(CART_BUMP_SCALE, CART_BUMP_SCALE, 0f), CART_BUMP_DURATION);
        }

        public void SetCartButtonVisible(bool visible) =>
            CartButton.gameObject.SetActive(visible);

        private void ResetCartBump()
        {
            cartBumpTween?.Kill();
            CartButton.transform.localScale = Vector3.one;
        }

        private void OnSearchValueChanged(string text)
        {
            SearchBar.clearSearchButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
            searchCts = searchCts.SafeRestart();
            DebounceSearchAsync(text, searchCts.Token).Forget();
        }

        private void OnSearchSubmitted(string text)
        {
            searchCts.SafeCancelAndDispose();
            searchCts = null;
            SearchChanged?.Invoke(text);
        }

        private void OnSearchCleared()
        {
            SetSearchText(string.Empty);
            SearchChanged?.Invoke(string.Empty);
        }

        private async UniTaskVoid DebounceSearchAsync(string text, CancellationToken ct)
        {
            if (await UniTask.Delay(SEARCH_DEBOUNCE_MS, cancellationToken: ct).SuppressCancellationThrow())
                return;

            SearchChanged?.Invoke(text);
        }

        [Serializable]
        public struct SectionTab
        {
            public ShopSection Section;
            public ButtonWithSelectableStateView Tab;
        }
    }
}
