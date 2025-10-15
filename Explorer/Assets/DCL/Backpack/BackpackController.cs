using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.CharacterPreview;
using DCL.Backpack.EmotesSection;
using DCL.CharacterPreview;
using DCL.Input;
using DCL.Profiles;
using DCL.UI;
using ECS.StreamableLoading.Common;
using Runtime.Wearables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Backpack.AvatarSection.Outfits.Slots;
using DCL.Browser;
using DCL.FeatureFlags;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using UnityEngine;
using Utility;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Backpack
{
    public class BackpackController : ISection, IDisposable
    {
        private readonly BackpackView view;
        private readonly ISelfProfile selfProfile;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3IdentityCache identityCache;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackInfoPanelController emoteInfoPanelController;
        private readonly RectTransform rectTransform;
        private readonly AvatarController avatarController;
        private readonly BackpackCharacterPreviewController backpackCharacterPreviewController;
        private readonly ICursor cursor;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly BackpackEmoteGridController backpackEmoteGridController;
        private readonly BackpackGridController backpackGridController;
        private readonly EmotesController emotesController;
        private readonly Dictionary<BackpackSections, ISection> backpackSections;
        private readonly SectionSelectorController<BackpackSections> sectionSelectorController;
        private readonly Dictionary<BackpackSections, TabSelectorView> tabsBySections;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly OutfitsRepository outfitsRepository;
        private readonly IRealmData realmData;
        private readonly IWebRequestController webController;
        private readonly IEquippedWearables equippedWearables;
        private readonly IWearableStorage wearableStorage;
        private readonly IWearablesProvider wearablesProvider;
        private readonly INftNamesProvider nftNamesProvider;
        private readonly IEventBus eventBus;
        private readonly FeatureFlagsConfiguration featureFlags;
        private BackpackSections lastShownSection;
        
        private CancellationTokenSource? animationCts;
        private CancellationTokenSource? profileLoadingCts;
        private BackpackSections currentSection = BackpackSections.Avatar;
        private bool isAvatarLoaded;
        private bool instantSectionToggle;

        public BackpackController(
            BackpackView view,
            FeatureFlagsConfiguration featureFlags,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IWeb3IdentityCache identityCache,
            AvatarView avatarView,
            NftTypeIconSO rarityInfoPanelBackgrounds,
            BackpackCommandBus backpackCommandBus,
            IBackpackEventBus backpackEventBus,
            BackpackGridController backpackGridController,
            BackpackInfoPanelController wearableInfoPanelController,
            BackpackInfoPanelController emoteInfoPanelController,
            World world, Entity playerEntity,
            BackpackEmoteGridController backpackEmoteGridController,
            AvatarSlotView[] avatarSlotViews,
            EmotesController emotesController,
            BackpackCharacterPreviewController backpackCharacterPreviewController,
            IThumbnailProvider thumbnailProvider,
            IInputBlock inputBlock,
            ICursor cursor,
            OutfitsRepository outfitsRepository,
            IRealmData realmData,
            IWebRequestController webController,
            IEquippedWearables equippedWearables,
            IWearableStorage wearableStorage,
            IWearablesProvider wearablesProvider,
            INftNamesProvider nftNamesProvider,
            IEventBus eventBus)
        {
            this.view = view;
            this.featureFlags = featureFlags;
            this.selfProfile = selfProfile;
            this.webBrowser = webBrowser;
            this.identityCache = identityCache;
            this.backpackCommandBus = backpackCommandBus;
            this.emoteInfoPanelController = emoteInfoPanelController;
            this.world = world;
            this.playerEntity = playerEntity;
            this.backpackEmoteGridController = backpackEmoteGridController;
            this.backpackGridController = backpackGridController;
            this.emotesController = emotesController;
            this.backpackEventBus = backpackEventBus;
            this.outfitsRepository = outfitsRepository;
            this.webController = webController;
            this.equippedWearables = equippedWearables;
            this.wearableStorage = wearableStorage;
            this.wearablesProvider = wearablesProvider;
            this.nftNamesProvider = nftNamesProvider;
            this.eventBus = eventBus;
            this.backpackCharacterPreviewController = backpackCharacterPreviewController;
            rectTransform = view.transform.parent.GetComponent<RectTransform>();

            var categoriesPresenter = new CategoriesPresenter(avatarView.CategoriesView,
                backpackGridController,
                backpackCommandBus,
                backpackEventBus,
                inputBlock);

            var screenshotService = new AvatarScreenshotService(selfProfile);
            var outfitSlotFactory = new OutfitSlotPresenterFactory(screenshotService);
            var outfitsCollection = new OutfitsCollection();
            var outfitApplier = new OutfitApplier(backpackCommandBus);
            var loadOutfitsCommand = new LoadOutfitsCommand(webController, selfProfile, realmData);
            var saveOutfitCommand = new SaveOutfitCommand(selfProfile, outfitsRepository, wearableStorage);
            var deleteOutfitCommand = new DeleteOutfitCommand(selfProfile, outfitsRepository, screenshotService);
            var checkOutfitsBannerCommand = new CheckOutfitsBannerVisibilityCommand(selfProfile, nftNamesProvider);
            var checkOutfitEquippedCommand = new CheckOutfitEquippedStateCommand(selfProfile, wearableStorage);
            var checkDuplicateOutfitCommand = new CheckForDuplicateOutfitCommand(checkOutfitEquippedCommand);
            var prewarmWearablesCacheCommand = new PrewarmWearablesCacheCommand(wearablesProvider);
            var previewOutfitCommand = new PreviewOutfitCommand(outfitApplier,
                equippedWearables,
                selfProfile,
                wearableStorage);
            
            var outfitsController = new OutfitsPresenter(avatarView.OutfitsView,
                eventBus,
                outfitApplier,
                outfitsCollection,
                webBrowser,
                equippedWearables,
                loadOutfitsCommand,
                saveOutfitCommand,
                deleteOutfitCommand,
                checkOutfitsBannerCommand,
                checkOutfitEquippedCommand,
                checkDuplicateOutfitCommand,
                prewarmWearablesCacheCommand,
                previewOutfitCommand,
                screenshotService,
                backpackCharacterPreviewController,
                outfitSlotFactory);
            
            avatarController = new AvatarController(
                avatarView,
                featureFlags,
                webBrowser,
                avatarSlotViews,
                rarityInfoPanelBackgrounds,
                backpackCommandBus,
                backpackEventBus,
                wearableInfoPanelController,
                backpackGridController,
                categoriesPresenter,
                outfitsController,
                thumbnailProvider);

            backpackSections = new Dictionary<BackpackSections, ISection>
            {
                { BackpackSections.Avatar, avatarController },
                { BackpackSections.Emotes, emotesController },
            };

            foreach (KeyValuePair<BackpackSections, ISection> keyValuePair in backpackSections)
                keyValuePair.Value.Deactivate();

            sectionSelectorController = new SectionSelectorController<BackpackSections>(backpackSections, BackpackSections.Avatar);
            tabsBySections = view.TabSelectorMappedViews.ToDictionary(map => map.Section, map => map.TabSelectorViews);

            foreach ((BackpackSections section, TabSelectorView? tabSelector) in tabsBySections)
            {
                tabSelector.TabSelectorToggle.onValueChanged.RemoveAllListeners();
                tabSelector.TabSelectorToggle.onValueChanged.AddListener(
                    isOn =>
                    {
                        ToggleSection(isOn, tabSelector, section, true);

                        if (isOn)
                        {
                            backpackEventBus.SendChangedBackpackSectionEvent(section);
                        }
                    }
                );
            }

            
            this.cursor = cursor;
            view.TipsButton.onClick.AddListener(ToggleTipsContent);
            view.TipsPanelDeselectable.OnDeselectEvent += ToggleTipsContent;
        }

        private void ToggleSection(bool isOn, TabSelectorView tabSelectorView, BackpackSections shownSection, bool animate)
        {
            if(isOn && animate && shownSection != lastShownSection)
                sectionSelectorController.SetAnimationState(false, tabsBySections[lastShownSection]);

            animationCts.SafeCancelAndDispose();
            animationCts = new CancellationTokenSource();
            sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelectorView, shownSection, animationCts.Token, animate).Forget();

            if (isOn)
            {
                lastShownSection = shownSection;
                backpackEventBus.SendChangedBackpackSectionEvent(shownSection);
            }
        }

        public void Dispose()
        {
            view.TipsPanelDeselectable.OnDeselectEvent -= ToggleTipsContent;
            avatarController.Dispose();
            emotesController.Dispose();
            backpackEmoteGridController.Dispose();
            animationCts.SafeCancelAndDispose();
            profileLoadingCts.SafeCancelAndDispose();
            backpackCharacterPreviewController.Dispose();
            backpackEmoteGridController.Dispose();
            emoteInfoPanelController.Dispose();
            
        }

        private void ToggleTipsContent()
        {
            if (!view.TipsPanelDeselectable.gameObject.activeInHierarchy)
                view.TipsPanelDeselectable.SelectElement();

            view.TipsPanelDeselectable.gameObject.SetActive(!view.TipsPanelDeselectable.gameObject.activeInHierarchy);
        }

        private async UniTaskVoid AwaitForProfileAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;

            isAvatarLoaded = false;

            world.TryGet(playerEntity, out AvatarShapeComponent avatarShapeComponent);

            Avatar avatar = world.Get<Profile>(playerEntity).Avatar;
            backpackGridController.RequestPage(1, true);
            backpackEmoteGridController.RequestAndFillEmotes(1, true);
            backpackCharacterPreviewController.Initialize(avatar, CharacterPreviewUtils.AVATAR_POSITION_1);

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                await avatarShapeComponent.WearablePromise.ToUniTaskAsync(world, cancellationToken: ct);

            if (ct.IsCancellationRequested) return;

            backpackCommandBus.SendCommand(new BackpackUnEquipAllCommand());
            backpackCommandBus.SendCommand(new BackpackChangeColorCommand(avatar.HairColor, WearableCategories.Categories.HAIR));
            backpackCommandBus.SendCommand(new BackpackChangeColorCommand(avatar.EyesColor, WearableCategories.Categories.EYES));
            backpackCommandBus.SendCommand(new BackpackChangeColorCommand(avatar.SkinColor, WearableCategories.Categories.BODY_SHAPE));
            backpackCommandBus.SendCommand(new BackpackHideCommand(avatar.ForceRender, true));
            backpackCommandBus.SendCommand(new BackpackEquipWearableCommand(avatar.BodyShape.Value));

            foreach (URN w in avatar.Wearables)
                backpackCommandBus.SendCommand(new BackpackEquipWearableCommand(w.Shorten()));

            for (var i = 0; i < avatar.Emotes.Count; i++)
            {
                URN avatarEmote = avatar.Emotes[i];
                if (avatarEmote.IsNullOrEmpty()) continue;
                backpackCommandBus.SendCommand(new BackpackEquipEmoteCommand(avatarEmote.Shorten(), i, false));
            }

            isAvatarLoaded = true;
        }

        public void Activate()
        {
            profileLoadingCts = profileLoadingCts.SafeRestart();
            AwaitForProfileAsync(profileLoadingCts.Token).Forget();

            backpackSections[currentSection].Activate();

            view.gameObject.SetActive(true);
            backpackCharacterPreviewController.OnBeforeShow();
            backpackCharacterPreviewController.OnShow();

            foreach ((BackpackSections section, TabSelectorView? tab) in tabsBySections)
                ToggleSection(section == BackpackSections.Avatar, tab, section, true);

            sectionSelectorController.SetAnimationState(true, tabsBySections[BackpackSections.Avatar]);

            cursor.Unlock();
        }

        public void Deactivate()
        {
            foreach (ISection backpackSectionsValue in backpackSections.Values)
                backpackSectionsValue.Deactivate();

            //Resets the tab selector to the default state (Avatar selected and open)
            foreach (BackpackPanelTabSelectorMapping tabSelector in view.TabSelectorMappedViews)
                tabSelector.TabSelectorViews.TabSelectorToggle.isOn = tabSelector.Section == BackpackSections.Avatar;

            profileLoadingCts.SafeCancelAndDispose();

            if (isAvatarLoaded)
                backpackCommandBus.SendCommand(new BackpackPublishProfileCommand());

            view.gameObject.SetActive(false);
            backpackCharacterPreviewController.OnHide();

            backpackEventBus.SendBackpackDeactivateEvent();
        }

        public void Animate(int triggerId)
        {
            view.PanelAnimator.SetTrigger(triggerId);
            view.HeaderAnimator.SetTrigger(triggerId);
        }

        public void ResetAnimator()
        {
            view.PanelAnimator.Rebind();
            view.HeaderAnimator.Rebind();
            view.PanelAnimator.Update(0);
            view.HeaderAnimator.Update(0);
        }

        public RectTransform GetRectTransform() =>
            rectTransform;

        public void Toggle(BackpackSections section)
        {
            bool tmp = instantSectionToggle;
            instantSectionToggle = true;

            foreach (BackpackPanelTabSelectorMapping tabSelector in view.TabSelectorMappedViews)
            {
                if (tabSelector.Section != section) continue;
                tabSelector.TabSelectorViews.TabSelectorToggle.isOn = true;
            }

            instantSectionToggle = tmp;
        }
    }
}
