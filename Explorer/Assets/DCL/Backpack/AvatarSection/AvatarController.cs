using DCL.AvatarRendering.Wearables;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.Input;
using DCL.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Browser;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    public class AvatarController : ISection, IDisposable
    {
        private readonly RectTransform rectTransform;
        private readonly ISelfProfile selfProfile;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3IdentityCache identityCache;
        private readonly BackpackSlotsController slotsController;
        private readonly CategoriesController categoriesController;
        private readonly OutfitsController outfitsController;
        private readonly BackpackGridController backpackGridController;
        private readonly AvatarView view;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackInfoPanelController backpackInfoPanelController;
        private readonly SectionSelectorController<AvatarSubSection> sectionSelectorController;
        private readonly Dictionary<AvatarSubSection, TabSelectorView> tabsBySections;
        private readonly Dictionary<AvatarSubSection, ISection> avatarSections;
        private AvatarSubSection lastShownSection;
        private readonly AvatarSubSection currentSection = AvatarSubSection.Categories;
        private CancellationTokenSource? animationCts;

        public AvatarController(AvatarView view,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IWeb3IdentityCache identityCache,
            AvatarSlotView[] slotViews,
            NftTypeIconSO rarityBackgrounds,
            BackpackCommandBus backpackCommandBus,
            IBackpackEventBus backpackEventBus,
            BackpackGridController backpackGridController,
            BackpackInfoPanelController backpackInfoPanelController,
            IThumbnailProvider thumbnailProvider,
            IInputBlock inputBlock)
        {
            this.view = view;
            this.selfProfile = selfProfile;
            this.webBrowser = webBrowser;
            this.identityCache = identityCache;
            this.backpackCommandBus = backpackCommandBus;
            this.backpackInfoPanelController = backpackInfoPanelController;
            this.backpackGridController = backpackGridController;
            
            rectTransform = view.GetComponent<RectTransform>();

            view.marketplaceButton.onClick.AddListener(OnOpenMarketplace);

            slotsController = new BackpackSlotsController(slotViews,
                backpackCommandBus,
                backpackEventBus,
                rarityBackgrounds,
                thumbnailProvider);

            categoriesController = new CategoriesController(view.CategoriesView,
                backpackGridController,
                backpackCommandBus,
                backpackEventBus,
                inputBlock);

            outfitsController = new OutfitsController(view.OutfitsView,
                selfProfile,
                new MockOutfitsService(),
                webBrowser,
                identityCache,
                backpackCommandBus);

            avatarSections = new Dictionary<AvatarSubSection, ISection>
            {
                {
                    AvatarSubSection.Categories, categoriesController
                },
                {
                    AvatarSubSection.Outfits, outfitsController
                }
            };

            foreach (KeyValuePair<AvatarSubSection, ISection> keyValuePair in avatarSections)
                keyValuePair.Value.Deactivate();

            sectionSelectorController = new SectionSelectorController<AvatarSubSection>(avatarSections, AvatarSubSection.Categories);
            tabsBySections = view.TabSelectorMappedViews.ToDictionary(map => map.Section, map => map.TabSelectorView);

            foreach (var (section, tabSelector) in tabsBySections)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();
                tabSelector.TabSelectorToggle.onValueChanged.AddListener(isOn =>
                    {
                        ToggleSection(isOn, tabSelector, section, true);

                        if (isOn)
                        {
                            //backpackEventBus.SendChangedBackpackSectionEvent(section);
                        }
                    }
                );
            }

            EnableTabs();
        }

        private void OnOpenMarketplace()
        {
            webBrowser.OpenUrl("https://market.decentraland.org/");
        }

        private void ToggleSection(bool isOn, TabSelectorView tabSelectorView, AvatarSubSection shownSection, bool animate)
        {
            if (isOn && animate && shownSection != lastShownSection)
                sectionSelectorController.SetAnimationState(false, tabsBySections[lastShownSection]);

            animationCts.SafeCancelAndDispose();
            animationCts = new CancellationTokenSource();
            sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelectorView, shownSection, animationCts.Token, animate).Forget();

            if (isOn)
            {
                lastShownSection = shownSection;
                //backpackEventBus.SendChangedBackpackSectionEvent(shownSection);
            }
        }

        private void EnableTabs()
        {
            view.CategoriesTabSelector.TabSelectorToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    ToggleSection(true, tabsBySections[AvatarSubSection.Categories], AvatarSubSection.Categories, true);
            });


            view.OutfitsTabSelector.TabSelectorToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                    ToggleSection(true, tabsBySections[AvatarSubSection.Outfits], AvatarSubSection.Outfits, true);
            });
        }

        private void DisableTabs()
        {
            foreach (var tab in tabsBySections.Values)
                tab.TabSelectorToggle.onValueChanged.RemoveAllListeners();
        }

        public void Dispose()
        {
            slotsController?.Dispose();
            categoriesController?.Dispose();
            outfitsController?.Dispose();
            
            backpackInfoPanelController?.Dispose();
            backpackGridController?.Dispose();

            view.marketplaceButton.onClick.RemoveAllListeners();

            DisableTabs();
        }

        public void RequestInitialWearablesPage() =>
            backpackGridController.RequestPage(1, true);

        public void Activate()
        {
            avatarSections[currentSection].Activate();

            foreach (var (section, tab) in tabsBySections)
                ToggleSection(section == AvatarSubSection.Categories, tab, section, true);

            var defaultTab = tabsBySections[AvatarSubSection.Categories];
            defaultTab.TabSelectorToggle.SetIsOnWithoutNotify(true);

            // 2) Make sure the other toggles look OFF (visual hygiene)
            foreach (var kv in tabsBySections)
                if (kv.Key != AvatarSubSection.Categories)
                    kv.Value.TabSelectorToggle.SetIsOnWithoutNotify(false);

            UniTask.Void(async () =>
            {
                await UniTask.Yield();
                sectionSelectorController.SetAnimationState(true, defaultTab);
            });
        }

        public void Deactivate()
        {
            backpackCommandBus.SendCommand(new BackpackFilterCommand(string.Empty, AvatarWearableCategoryEnum.Body, string.Empty));
            backpackGridController.Deactivate();
        }

        public void Animate(int triggerId) =>
            view.gameObject.SetActive(triggerId == UIAnimationHashes.IN);

        public void ResetAnimator() { }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
