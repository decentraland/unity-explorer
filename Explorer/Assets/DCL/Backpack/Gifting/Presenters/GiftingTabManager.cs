using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Views;
using DCL.UI;
using DG.Tweening;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Presenters
{
    public class GiftingTabsManager : IDisposable
    {
        private const float ANIMATION_DURATION = 0.4f;

        private readonly RectTransform contentContainer;
        private readonly Dictionary<GiftingSection, IGiftingGridPresenter> presenters;
        private readonly Dictionary<GiftingSection, TabSelectorView> tabs;

        private CancellationTokenSource? animationCts;
        private bool isAnimating;
        private GiftingSection lastShownSection;

        public IGiftingGridPresenter? ActivePresenter => presenters[lastShownSection];

        public GiftingTabsManager(
            GiftingView view,
            IGiftingGridPresenter wearablesPresenter,
            IGiftingGridPresenter emotesPresenter)
        {
            contentContainer = view.ContentContainer;

            presenters = new Dictionary<GiftingSection, IGiftingGridPresenter>
            {
                {
                    GiftingSection.Wearables, wearablesPresenter
                },
                {
                    GiftingSection.Emotes, emotesPresenter
                }
            };

            tabs = view.TabSelectorMappedViews.ToDictionary(m => m.Section, m => m.TabSelectorView);

            foreach (var (section, tabView) in tabs)
            {
                tabView.TabSelectorToggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) ToggleSection(section);
                });
            }
        }

        public void Initialize()
        {
            ActivateDefault();
        }

        public void ActivateDefault()
        {
            // Deactivate all presenters and hide their GameObjects
            foreach (var presenter in presenters.Values)
            {
                presenter.Deactivate();
                presenter.GetRectTransform().gameObject.SetActive(false);
            }

            // Set Wearables as the default
            lastShownSection = GiftingSection.Wearables;

            // Activate and correctly position the default presenter
            var defaultPresenter = presenters[lastShownSection];
            var defaultRt = defaultPresenter.GetRectTransform();
            defaultRt.gameObject.SetActive(true);
            defaultRt.anchoredPosition = new Vector2(0, defaultRt.anchoredPosition.y);
            defaultPresenter.GetCanvasGroup().alpha = 1f;
            defaultPresenter.Activate();

            // Set tab states
            foreach (var (section, tab) in tabs)
            {
                tab.TabSelectorToggle.SetIsOnWithoutNotify(section == lastShownSection);

                if (section != lastShownSection)
                    SetTabAnimationState(tab, false);
            }

            UniTask.Void(async () =>
            {
                await UniTask.Yield();
                if (tabs.TryGetValue(lastShownSection, out var defaultTabView))
                    SetTabAnimationState(defaultTabView, true);
            });
        }

        private void SetTabAnimationState(TabSelectorView tabView, bool isActive)
        {
            if (tabView.tabAnimator == null || !tabView.gameObject.activeInHierarchy) return;

            if (isActive)
            {
                tabView.tabAnimator.SetTrigger(UIAnimationHashes.ACTIVE);
            }
            else
            {
                tabView.tabAnimator.Rebind();
                tabView.tabAnimator.Update(0);
            }
        }
        
        private void ToggleSection(GiftingSection newSection)
        {
            if (isAnimating || newSection == lastShownSection) return;

            if (tabs.TryGetValue(lastShownSection, out var lastTabView))
                SetTabAnimationState(lastTabView, false);

            if (tabs.TryGetValue(newSection, out var newTabView))
                SetTabAnimationState(newTabView, true);
            
            var lastPresenter = presenters[lastShownSection];
            var newPresenter = presenters[newSection];

            isAnimating = true;
            animationCts = animationCts.SafeRestart();

            float containerWidth = contentContainer.rect.width;

            // Determine slide direction (1 for right, -1 for left)
            int lastIndex = (int)lastShownSection;
            int newIndex = (int)newSection;
            float slideDirection = newIndex > lastIndex ? 1f : -1f;

            var lastSectionRT = lastPresenter.GetRectTransform();
            var newSectionRT = newPresenter.GetRectTransform();
            var lastSectionCG = lastPresenter.GetCanvasGroup();
            var newSectionCG = newPresenter.GetCanvasGroup();

            // Prepare the new section off-screen
            newPresenter.Activate();
            newSectionRT.gameObject.SetActive(true);
            newSectionRT.anchoredPosition = new Vector2(containerWidth * slideDirection, newSectionRT.anchoredPosition.y);
            newSectionCG.alpha = 0f;

            // Create and run the animation sequence
            var sequence = DOTween.Sequence();
            sequence.Append(lastSectionRT.DOAnchorPosX(-containerWidth * slideDirection, ANIMATION_DURATION).SetEase(Ease.OutCubic))
                .Join(newSectionRT.DOAnchorPosX(0, ANIMATION_DURATION).SetEase(Ease.OutCubic));

            sequence.Join(lastSectionCG.DOFade(0f, ANIMATION_DURATION * 0.5f));
            sequence.Join(newSectionCG.DOFade(1f, ANIMATION_DURATION * 0.7f));

            sequence.SetUpdate(true)
                .OnComplete(() =>
                {
                    lastSectionRT.gameObject.SetActive(false);
                    lastPresenter.Deactivate();
                    lastShownSection = newSection;
                    isAnimating = false;
                });
        }

        public void Dispose()
        {
            animationCts.SafeCancelAndDispose();
            foreach (var tabView in tabs.Values)
                tabView.TabSelectorToggle.onValueChanged.RemoveAllListeners();
        }
    }

    public interface IGiftingGridPresenter
    {
        void Activate();
        void Deactivate();
        void SetSearchText(string text);
        RectTransform GetRectTransform();
        CanvasGroup GetCanvasGroup();
        event Action<string?> OnSelectionChanged;
        string? SelectedUrn { get; }
    }

    // Generic interface for the Adapter, inheriting the base
    public interface IGiftingGridPresenter<TViewModel> : IGiftingGridPresenter where TViewModel : IGiftableItemViewModel
    {
        int ItemCount { get; }
        TViewModel GetViewModel(int itemIndex);
        void RequestThumbnailLoad(int itemIndex);
    }
}