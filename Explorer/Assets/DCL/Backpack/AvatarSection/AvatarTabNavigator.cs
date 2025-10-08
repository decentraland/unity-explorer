using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.UI;
using UnityEngine.Events;
using Utility;

namespace DCL.Backpack
{
    public sealed class AvatarTabsManager : IDisposable
    {
        private readonly SectionSelectorController<AvatarSubSection> selector;
        private readonly Dictionary<AvatarSubSection, ISection> sectionsByKey;
        private readonly Dictionary<AvatarSubSection, TabSelectorView> tabsByKey;
        private readonly TabSelectorView categoriesTab;
        private readonly TabSelectorView outfitsTab;
        private readonly AvatarSubSection defaultSection;

        private readonly Dictionary<AvatarSubSection, UnityAction<bool>> handlers = new();
        private CancellationTokenSource? animationCts;
        private AvatarSubSection lastShownSection;

        private AvatarTabsManager(
            SectionSelectorController<AvatarSubSection> selector,
            Dictionary<AvatarSubSection, ISection> sectionsByKey,
            Dictionary<AvatarSubSection, TabSelectorView> tabsByKey,
            TabSelectorView categoriesTab,
            TabSelectorView outfitsTab,
            AvatarSubSection defaultSection)
        {
            this.selector      = selector;
            this.sectionsByKey = sectionsByKey;
            this.tabsByKey     = tabsByKey;
            this.categoriesTab = categoriesTab;
            this.outfitsTab    = outfitsTab;
            this.defaultSection = defaultSection;

            lastShownSection = defaultSection;
        }

        public static AvatarTabsManager CreateFromView(
            AvatarView view,
            ISection categoriesSection,
            ISection outfitsSection,
            AvatarSubSection defaultSection = AvatarSubSection.Categories)
        {
            var sections = new Dictionary<AvatarSubSection, ISection>
            {
                {
                    AvatarSubSection.Categories, categoriesSection
                },
                {
                    AvatarSubSection.Outfits,    outfitsSection
                }
            };

            foreach (var s in sections.Values)
                s.Deactivate();

            var tabs = view.TabSelectorMappedViews.ToDictionary(m => m.Section, m => m.TabSelectorView);

            var selector = new SectionSelectorController<AvatarSubSection>(sections, defaultSection);

            return new AvatarTabsManager(
                selector,
                sections,
                tabs,
                view.CategoriesTabSelector,
                view.OutfitsTabSelector,
                defaultSection);
        }

        public void InitializeAndEnable()
        {
            WireTabMappingHandlers();
            EnableTabs();
        }

        public void WireTabMappingHandlers()
        {
            foreach (var (section, tabSelector) in tabsByKey)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();
                UnityAction<bool> handler = isOn =>
                {
                    if (isOn) ToggleSection(true, tabSelector, section, animate: true);
                };
                handlers[section] = handler;
                tabSelector.TabSelectorToggle.onValueChanged.AddListener(handler);
            }
        }

        public void EnableTabs()
        {
            categoriesTab.TabSelectorToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    ToggleSection(true, tabsByKey[AvatarSubSection.Categories], AvatarSubSection.Categories, true);
            });

            outfitsTab.TabSelectorToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    ToggleSection(true, tabsByKey[AvatarSubSection.Outfits], AvatarSubSection.Outfits, true);
            });
        }

        public void ActivateDefault()
        {
            sectionsByKey[defaultSection].Activate();

            foreach (var (section, tab) in tabsByKey)
                ToggleSection(section == AvatarSubSection.Categories, tab, section, animate: true);

            var defaultTab = tabsByKey[AvatarSubSection.Categories];
            defaultTab.TabSelectorToggle.SetIsOnWithoutNotify(true);

            foreach (var kv in tabsByKey)
                if (kv.Key != AvatarSubSection.Categories)
                    kv.Value.TabSelectorToggle.SetIsOnWithoutNotify(false);

            UniTask.Void(async () =>
            {
                await UniTask.Yield();
                selector.SetAnimationState(true, defaultTab);
            });
        }

        private void ToggleSection(bool isOn, TabSelectorView tabSelectorView, AvatarSubSection shownSection, bool animate)
        {
            if (isOn && animate && shownSection != lastShownSection)
                selector.SetAnimationState(false, tabsByKey[lastShownSection]);

            animationCts.SafeCancelAndDispose();
            animationCts = new CancellationTokenSource();

            selector.OnTabSelectorToggleValueChangedAsync(
                    isOn,
                    tabSelectorView,
                    shownSection,
                    animationCts.Token,
                    animate)
                .Forget();

            if (isOn)
                lastShownSection = shownSection;
        }

        private void DisableTabs()
        {
            foreach (var tab in tabsByKey.Values)
                tab.TabSelectorToggle.onValueChanged.RemoveAllListeners();
        }

        public void Dispose()
        {
            DisableTabs();
            animationCts.SafeCancelAndDispose();
        }
    }
}