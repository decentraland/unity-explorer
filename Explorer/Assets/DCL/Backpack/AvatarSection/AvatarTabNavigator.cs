using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.UI;
using DG.Tweening;
using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    public sealed class AvatarTabsManager : IDisposable
    {
        private readonly Dictionary<AvatarSubSection, ISection> sectionsByKey;
        private readonly Dictionary<AvatarSubSection, TabSelectorView> tabsByKey;
        private readonly List<AvatarSubSection> tabOrder;
        private readonly RectTransform contentContainer;
        private const float animationDuration = 0.4f;
        private readonly AvatarSubSection defaultSection;
        private readonly bool useAnimation;

        private CancellationTokenSource? animationCts;
        private AvatarSubSection lastShownSection;
        private bool isAnimating;

        private AvatarTabsManager(
            Dictionary<AvatarSubSection, ISection> sectionsByKey,
            Dictionary<AvatarSubSection, TabSelectorView> tabsByKey,
            RectTransform contentContainer,
            List<AvatarSubSection> tabOrder,
            AvatarSubSection defaultSection,
            bool useAnimation)
        {
            this.sectionsByKey = sectionsByKey;
            this.tabsByKey = tabsByKey;
            this.contentContainer = contentContainer;
            this.tabOrder = tabOrder;
            this.defaultSection = defaultSection;
            this.useAnimation = useAnimation;
            lastShownSection = defaultSection;
        }

        public static AvatarTabsManager CreateFromView(
            AvatarView view,
            ISection categoriesSection,
            ISection outfitsSection,
            RectTransform contentContainer,
            bool useAnimation = true,
            AvatarSubSection defaultSection = AvatarSubSection.Categories)
        {
            var sections = new Dictionary<AvatarSubSection, ISection>
            {
                {
                    AvatarSubSection.Categories, categoriesSection
                },
                {
                    AvatarSubSection.Outfits, outfitsSection
                },
            };

            foreach (var s in sections.Values)
                s.Deactivate();

            var tabs = view.TabSelectorMappedViews.ToDictionary(m => m.Section, m => m.TabSelectorView);
            var tabOrder = new List<AvatarSubSection>
            {
                AvatarSubSection.Categories, AvatarSubSection.Outfits
            };

            return new AvatarTabsManager(
                sections,
                tabs,
                contentContainer,
                tabOrder,
                defaultSection,
                useAnimation);
        }

        public void InitializeAndEnable()
        {
            WireTabMappingHandlers();
            ActivateDefault();
        }

        public void Show()
        {
            ActivateDefault();
        }

        private void WireTabMappingHandlers()
        {
            foreach (var (section, tabSelector) in tabsByKey)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();
                var currentSection = section;
                tabSelector.TabSelectorToggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn)
                        ToggleSection(currentSection);
                });
            }
        }

        public void ActivateDefault()
        {
            foreach (var sectionController in sectionsByKey.Values)
            {
                sectionController.Deactivate();
                sectionController.GetRectTransform().gameObject.SetActive(false);
            }

            if (sectionsByKey.TryGetValue(defaultSection, out var defaultSectionController))
            {
                var rt = defaultSectionController.GetRectTransform();
                float originalY = rt.anchoredPosition.y;
                rt.anchoredPosition = new Vector2(0, originalY);
                rt.gameObject.SetActive(true);

                var canvasGroup = rt.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f;

                defaultSectionController.Activate();
            }

            foreach (var (section, tab) in tabsByKey)
            {
                tab.TabSelectorToggle.SetIsOnWithoutNotify(section == defaultSection);
                if (section != defaultSection)
                    SetTabAnimationState(tab, false);
            }

            UniTask.Void(async () =>
            {
                await UniTask.Yield();
                if (tabsByKey.TryGetValue(defaultSection, out var defaultTabView))
                    SetTabAnimationState(defaultTabView, true);
            });

            lastShownSection = defaultSection;
        }

        private void ToggleSection(AvatarSubSection newSection)
        {
            if (isAnimating || newSection == lastShownSection)
                return;

            if (tabsByKey.TryGetValue(lastShownSection, out var lastTabView))
                SetTabAnimationState(lastTabView, false);
            if (tabsByKey.TryGetValue(newSection, out var newTabView))
                SetTabAnimationState(newTabView, true);

            var lastSectionController = sectionsByKey[lastShownSection];
            var newSectionController = sectionsByKey[newSection];

            if (useAnimation)
            {
                isAnimating = true;
                animationCts.SafeCancelAndDispose();
                animationCts = new CancellationTokenSource();

                int lastIndex = tabOrder.IndexOf(lastShownSection);
                int newIndex = tabOrder.IndexOf(newSection);
                float slideDirection = newIndex > lastIndex ? 1f : -1f;

                var lastSectionRT = lastSectionController.GetRectTransform();
                var newSectionRT = newSectionController.GetRectTransform();
                var lastSectionCG = lastSectionRT.GetComponent<CanvasGroup>();
                var newSectionCG = newSectionRT.GetComponent<CanvasGroup>();
                float containerWidth = contentContainer.rect.width;

                newSectionController.Activate();

                float originalY = newSectionRT.anchoredPosition.y;
                newSectionRT.gameObject.SetActive(true);
                newSectionRT.anchoredPosition = new Vector2(containerWidth * slideDirection, originalY);

                if (newSectionCG != null) newSectionCG.alpha = 0f;

                var sequence = DOTween.Sequence();
                sequence.Append(lastSectionRT.DOAnchorPosX(-containerWidth * slideDirection, animationDuration).SetEase(Ease.OutBack))
                    .Join(newSectionRT.DOAnchorPosX(0, animationDuration).SetEase(Ease.OutBack));

                if (lastSectionCG != null) sequence.Join(lastSectionCG.DOFade(0f, animationDuration * 0.5f));
                if (newSectionCG != null) sequence.Join(newSectionCG.DOFade(1f, animationDuration * 0.7f));

                sequence.SetUpdate(true)
                    .OnComplete(() =>
                    {
                        lastSectionRT.gameObject.SetActive(false);
                        lastSectionController.Deactivate();
                        lastShownSection = newSection;
                        isAnimating = false;
                    });
            }
            else
            {
                lastSectionController.GetRectTransform().gameObject.SetActive(false);
                lastSectionController.Deactivate();
                newSectionController.GetRectTransform().gameObject.SetActive(true);
                newSectionController.Activate();
                lastShownSection = newSection;
            }
        }

        public void SetTabEnabled(AvatarSubSection section, bool isEnabled)
        {
            if (!tabsByKey.TryGetValue(section, out var tabView))
            {
                ReportHub.LogWarning(ReportCategory.OUTFITS, $"AvatarTabsManager: Could not find tab for section '{section}'.");
                return;
            }

            tabView.gameObject.SetActive(isEnabled);

            if (!isEnabled)
            {
                if (sectionsByKey.Remove(section, out var sectionController))
                    sectionController.Deactivate();

                tabsByKey.Remove(section);
                tabOrder.Remove(section);
                WireTabMappingHandlers();
            }
        }

        private void SetTabAnimationState(TabSelectorView tabView, bool isActive)
        {
            if (tabView.tabAnimator == null || !tabView.gameObject.activeInHierarchy) return;

            if (isActive)
                tabView.tabAnimator.SetTrigger(UIAnimationHashes.ACTIVE);
            else
            {
                tabView.tabAnimator.Rebind();
                tabView.tabAnimator.Update(0);
            }
        }

        public void Dispose()
        {
            DisableTabs();
            animationCts.SafeCancelAndDispose();
            DOTween.KillAll(true);
        }

        private void DisableTabs()
        {
            foreach (var tab in tabsByKey.Values)
                tab.TabSelectorToggle.onValueChanged.RemoveAllListeners();
        }

        public void DeactivateAll()
        {
            foreach (var section in sectionsByKey.Values)
                section.Deactivate();

            foreach (var tab in tabsByKey.Values)
                tab.TabSelectorToggle.SetIsOnWithoutNotify(false);
        }
    }
}