using DCL.UI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Shop
{
    public class ShopView : MonoBehaviour
    {
        private const string BADGE_OVERFLOW = "99+";
        private const int BADGE_MAX = 99;

        [field: Header("Header")]
        [field: SerializeField] public ButtonWithSelectableStateView OverviewTab { get; private set; } = null!;
        [field: SerializeField] public ButtonWithSelectableStateView CollectiblesTab { get; private set; } = null!;
        [field: SerializeField] public Button CartButton { get; private set; } = null!;
        [field: SerializeField] public GameObject CartBadge { get; private set; } = null!;
        [field: SerializeField] public TMP_Text CartBadgeText { get; private set; } = null!;
        [field: SerializeField] public Button BuyCreditsButton { get; private set; } = null!;

        [field: Header("Pages")]
        [field: SerializeField] public ShopOverviewView OverviewView { get; private set; } = null!;
        [field: SerializeField] public GameObject CollectiblesPageRoot { get; private set; } = null!;
        [field: SerializeField] public ShopCollectiblesView CollectiblesView { get; private set; } = null!;
        [field: SerializeField] public ShopFiltersView FiltersView { get; private set; } = null!;

        [field: Header("Animators")]
        [field: SerializeField] public Animator PanelAnimator { get; private set; } = null!;
        [field: SerializeField] public Animator HeaderAnimator { get; private set; } = null!;

        public bool IsSearchBarFocused => CollectiblesView.IsSearchBarFocused;

        public event Action<ShopPage>? PageTabClicked;
        public event Action? CartButtonClicked;
        public event Action? BuyCreditsClicked;

        private void Awake()
        {
            OverviewTab.Button.onClick.AddListener(() => PageTabClicked?.Invoke(ShopPage.Overview));
            CollectiblesTab.Button.onClick.AddListener(() => PageTabClicked?.Invoke(ShopPage.Collectibles));
            CartButton.onClick.AddListener(() => CartButtonClicked?.Invoke());
            BuyCreditsButton.onClick.AddListener(() => BuyCreditsClicked?.Invoke());
        }

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

        public void ShowPage(ShopPage page)
        {
            OverviewView.gameObject.SetActive(page == ShopPage.Overview);
            CollectiblesPageRoot.SetActive(page == ShopPage.Collectibles);
            OverviewTab.SetSelected(page == ShopPage.Overview);
            CollectiblesTab.SetSelected(page == ShopPage.Collectibles);
        }

        public void SetCartBadge(int units)
        {
            CartBadge.SetActive(units > 0);
            CartBadgeText.text = units > BADGE_MAX ? BADGE_OVERFLOW : units.ToString();
        }

        public void SetCartButtonVisible(bool visible) =>
            CartButton.gameObject.SetActive(visible);

        public void SetBuyCreditsVisible(bool visible) =>
            BuyCreditsButton.gameObject.SetActive(visible);
    }
}
