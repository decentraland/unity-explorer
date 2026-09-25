using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.AvatarSection.Outfits;
using DCL.Backpack.AvatarSection.Outfits.Commands;
using DCL.Backpack.AvatarSection.Outfits.Logger;
using DCL.Backpack.AvatarSection.Outfits.Repository;
using DCL.Backpack.AvatarSection.Outfits.Services;
using DCL.Backpack.AvatarSection.Outfits.Slots;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.CharacterPreview;
using DCL.Backpack.EmotesSection;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Utility;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Backpack
{
    public class BackpackController : ISection, IDisposable
    {
        private const float COMPACT_HOST_MARGIN = 40f;

        /// <summary>
        ///     Gap the trimmed rects keep off the content panel's right border. Their contents are centred, so they move left
        ///     by half of it.
        /// </summary>
        private const float COMPACT_TRIMMED_RIGHT_MARGIN = 20f;

        private readonly BackpackView view;
        private readonly ISelfProfile selfProfile;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackInfoPanelController emoteInfoPanelController;
        private readonly RectTransform homeHost;
        private readonly AvatarController avatarController;
        private readonly BackpackCharacterPreviewController backpackCharacterPreviewController;
        private readonly ICursor cursor;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly BackpackEmoteGridController backpackEmoteGridController;
        private readonly EmotesController emotesController;
        private readonly Dictionary<BackpackSections, ISection> backpackSections;
        private readonly SectionSelectorController<BackpackSections> sectionSelectorController;
        private readonly Dictionary<BackpackSections, TabSelectorView> tabsBySections;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly BackpackSections currentSection = BackpackSections.Avatar;
        private readonly IRealmData realmData;
        private readonly RectSnapshot contentFull;
        private readonly Vector2[] shiftedFullPositions;
        private readonly RectSnapshot[] trimmedFull;
        private readonly RectSnapshot previewFull;
        private readonly RectSnapshot previewImageFull;
        private readonly RectSnapshot searchBarFull;
        private readonly float compactTrim;
        private readonly Vector2 compactCloseSlot;

        private BackpackSections lastShownSection;
        private CancellationTokenSource? animationCts;
        private CancellationTokenSource? profileLoadingCts;
        private bool isAvatarLoaded;
        private bool instantSectionToggle;
        private bool isActive;

        /// <summary>
        ///     Raised by the panel's own close control, which only the compact layout shows.
        /// </summary>
        public event Action? CloseRequested;

        public BackpackController(
            BackpackView view,
            ISelfProfile selfProfile,
            IWeb3IdentityCache web3IdentityCache,
            UnityAppWebBrowser webBrowser,
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
            INftNamesProvider nftNamesProvider,
            IEventBus eventBus,
            Sprite deleteIcon,
            IDecentralandUrlsSource decentralandUrlsSource,
            IOwnedNftFilter ownedNftFilter)
        {
            this.view = view;
            this.selfProfile = selfProfile;
            this.web3IdentityCache = web3IdentityCache;
            this.backpackCommandBus = backpackCommandBus;
            this.emoteInfoPanelController = emoteInfoPanelController;
            this.world = world;
            this.playerEntity = playerEntity;
            this.backpackEmoteGridController = backpackEmoteGridController;
            this.emotesController = emotesController;
            this.backpackEventBus = backpackEventBus;
            this.backpackCharacterPreviewController = backpackCharacterPreviewController;
            homeHost = view.transform.parent.GetComponent<RectTransform>();

            contentFull = new RectSnapshot(view.ContentRect);
            shiftedFullPositions = new Vector2[view.CompactShiftedRects.Length];

            for (var i = 0; i < view.CompactShiftedRects.Length; i++)
                shiftedFullPositions[i] = view.CompactShiftedRects[i].anchoredPosition;

            trimmedFull = new RectSnapshot[view.CompactTrimmedRects.Length];

            for (var i = 0; i < view.CompactTrimmedRects.Length; i++)
                trimmedFull[i] = new RectSnapshot(view.CompactTrimmedRects[i]);

            previewFull = new RectSnapshot((RectTransform)view.CharacterPreviewView.transform);
            previewImageFull = new RectSnapshot(view.CharacterPreviewView.RawImage.rectTransform);
            searchBarFull = new RectSnapshot(view.SearchBarRect);

            // Both are anchored to a single point horizontally, so the authored size delta is their width whether or not they were laid out yet
            var itemInfoRect = (RectTransform)view.ItemInfoPanels[0].transform;
            compactTrim = itemInfoRect.sizeDelta.x + Mathf.Abs(itemInfoRect.anchoredPosition.x);
            var closeRect = (RectTransform)view.CloseButton.transform;
            compactCloseSlot = new Vector2(closeRect.sizeDelta.x + Mathf.Abs(closeRect.anchoredPosition.x), 0f);

            var categoriesPresenter = new CategoriesPresenter(avatarView.CategoriesView,
                backpackGridController,
                backpackCommandBus,
                backpackEventBus,
                inputBlock);

            var screenshotService = new AvatarScreenshotService(selfProfile);
            var outfitsLogger = new OutfitsLogger(wearableStorage, realmData);
            var outfitSlotFactory = new OutfitSlotPresenterFactory(screenshotService);
            var outfitsCollection = new OutfitsCollection();
            var outfitApplier = new OutfitApplier(backpackCommandBus);
            var loadOutfitsCommand = new LoadOutfitsCommand(webController,
                selfProfile,
                decentralandUrlsSource,
                outfitsLogger,
                outfitsRepository);
            var saveOutfitCommand = new SaveOutfitCommand(selfProfile,
                outfitsRepository,
                wearableStorage,
                eventBus,
                outfitsLogger,
                ownedNftFilter);
            var deleteOutfitCommand = new DeleteOutfitCommand(outfitsRepository, screenshotService, deleteIcon);
            var checkOutfitsBannerCommand = new CheckOutfitsBannerVisibilityCommand(selfProfile, nftNamesProvider);
            var previewOutfitCommand = new PreviewOutfitCommand(outfitApplier,
                equippedWearables,
                selfProfile,
                wearableStorage,
                outfitsLogger,
                ownedNftFilter);

            var outfitsPresenter = new OutfitsPresenter(avatarView.OutfitsView,
                eventBus,
                backpackEventBus,
                outfitApplier,
                outfitsCollection,
                webBrowser,
                equippedWearables,
                loadOutfitsCommand,
                saveOutfitCommand,
                deleteOutfitCommand,
                checkOutfitsBannerCommand,
                previewOutfitCommand,
                screenshotService,
                backpackCharacterPreviewController,
                outfitSlotFactory,
                ownedNftFilter);

            avatarController = new AvatarController(
                avatarView,
                webBrowser,
                avatarSlotViews,
                rarityInfoPanelBackgrounds,
                backpackCommandBus,
                backpackEventBus,
                wearableInfoPanelController,
                backpackGridController,
                categoriesPresenter,
                outfitsPresenter,
                thumbnailProvider,
                decentralandUrlsSource);

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
            view.CloseButton.onClick.AddListener(RequestClose);
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
            view.CloseButton.onClick.RemoveListener(RequestClose);
            avatarController.Dispose();
            emotesController.Dispose();
            backpackEmoteGridController.Dispose();
            animationCts.SafeCancelAndDispose();
            profileLoadingCts.SafeCancelAndDispose();
            backpackCharacterPreviewController.Dispose();
            emoteInfoPanelController.Dispose();
        }

        private void RequestClose() =>
            CloseRequested?.Invoke();

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

            Profile? inWorldProfile = world.Has<Profile>(playerEntity) ? world.Get<Profile>(playerEntity) : null;

            // Before the world is loaded the player entity carries no profile, and after a logout it still carries the one of the
            // session that ended until the next world load replaces it, so only a profile owned by the current identity is trusted
            Avatar? avatar = inWorldProfile != null && inWorldProfile.UserId == web3IdentityCache.Identity?.Address
                ? inWorldProfile.Avatar
                : (await selfProfile.ProfileAsync(ct))?.Avatar;

            if (ct.IsCancellationRequested) return;

            if (avatar == null)
            {
                ReportHub.LogWarning(ReportCategory.BACKPACK, "Own profile is not available, the backpack cannot show the avatar");
                return;
            }

            backpackCharacterPreviewController.Initialize(avatar, CharacterPreviewUtils.BACKPACK_PREVIEW_POSITION);

            // Equipping while the in-world avatar is still resolving its own wearables would fight with it; with no avatar in the world there is nothing to wait for
            while (world.TryGet(playerEntity, out AvatarShapeComponent avatarShapeComponent) && !avatarShapeComponent.WearablePromise.IsConsumed)
                await UniTask.Yield();

            if (ct.IsCancellationRequested) return;

            var wearables = new List<string>(avatar.Wearables.Count);
            foreach (var w in avatar.Wearables) wearables.Add(w.Shorten());

            var command = new BackpackEquipOutfitCommand(
                avatar.BodyShape.Value,
                wearables,
                avatar.EyesColor,
                avatar.HairColor,
                avatar.SkinColor,
                avatar.ForceRender,
                useFullUrns: true
            );
            backpackCommandBus.SendCommand(command);

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

            isActive = true;
            backpackEventBus.SendBackpackActivateEvent(CurrentHost == homeHost);
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

            isActive = false;
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

        /// <summary>
        ///     The slot the view is parented to right now. The hosts use it to tell their own teardown from a late one.
        /// </summary>
        public RectTransform? CurrentHost =>
            view.transform.parent as RectTransform;

        /// <summary>
        ///     Moves the single backpack view under the host that is about to show it and stretches it to fill the slot. A
        ///     compact host is narrower than the screen, so the panel gives up its item info column to fit.
        /// </summary>
        public void AttachTo(RectTransform host, bool compact)
        {
            var viewRect = (RectTransform)view.transform;

            // A host can claim the view while another one still has it open (a fullscreen panel closes popups without awaiting
            // them), so the previous session is ended here: its host skips its own teardown once the view is no longer under it
            if (isActive && viewRect.parent != host)
                Deactivate();

            SetCompactLayout(compact);

            if (viewRect.parent == host) return;

            viewRect.SetParent(host, false);
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = Vector2.zero;
            viewRect.offsetMax = Vector2.zero;

            // Only the explore panel drives the panel animators, so a view coming from anywhere else would keep the state it was left in
            ResetAnimator();
        }

        public void AttachToHome() =>
            AttachTo(homeHost, false);

        /// <summary>
        ///     Trims the item info column off the content panel and pins what is left to the right border of the host, so every
        ///     pixel nothing else claims goes to the avatar. Everything centred on the panel is pushed back by half of what was
        ///     trimmed to hold its place in it. Rects that span the item info column themselves lose the same width instead:
        ///     centred, that keeps their left edge and re-centres their contents on the grid. The outfits row is a single
        ///     fixed width strip and cannot reflow into what is left, so it is scaled down by the same ratio instead. Every
        ///     width comes from the authored values snapshotted at construction, so neither an unlaid panel nor a previous
        ///     compact pass can skew it.
        /// </summary>
        private void SetCompactLayout(bool compact)
        {
            foreach (BackpackInfoPanelView itemInfoPanel in view.ItemInfoPanels)
                itemInfoPanel.gameObject.SetActive(!compact);

            float trim = compact ? compactTrim : 0f;
            float contentWidth = contentFull.SizeDelta.x - trim;

            if (compact)
            {
                view.ContentRect.anchorMin = new Vector2(1f, view.ContentRect.anchorMin.y);
                view.ContentRect.anchorMax = new Vector2(1f, view.ContentRect.anchorMax.y);
                view.ContentRect.sizeDelta = new Vector2(contentWidth, contentFull.SizeDelta.y);
                view.ContentRect.anchoredPosition = new Vector2(-(COMPACT_HOST_MARGIN + (contentWidth / 2f)), contentFull.AnchoredPosition.y);
            }
            else
                contentFull.ApplyTo(view.ContentRect);

            for (var i = 0; i < view.CompactShiftedRects.Length; i++)
                view.CompactShiftedRects[i].anchoredPosition = shiftedFullPositions[i] + new Vector2(trim / 2f, 0f);

            for (var i = 0; i < view.CompactTrimmedRects.Length; i++)
            {
                if (!compact)
                {
                    trimmedFull[i].ApplyTo(view.CompactTrimmedRects[i]);
                    continue;
                }

                view.CompactTrimmedRects[i].sizeDelta = trimmedFull[i].SizeDelta - new Vector2(trim + COMPACT_TRIMMED_RIGHT_MARGIN, 0f);
                view.CompactTrimmedRects[i].anchoredPosition = trimmedFull[i].AnchoredPosition - new Vector2(COMPACT_TRIMMED_RIGHT_MARGIN / 2f, 0f);
            }

            float scale = contentWidth / contentFull.SizeDelta.x;
            view.OutfitsRect.localScale = new Vector3(scale, scale, 1f);

            SetCompactPreview(compact, contentWidth);
            SetCompactHeader(compact);
        }

        /// <summary>
        ///     Hands the close button its slot at the right end of the header and takes that slot off the search strip. The full
        ///     screen layout has no close button, so there the strip spans the slot too.
        /// </summary>
        private void SetCompactHeader(bool compact)
        {
            view.CloseButton.gameObject.SetActive(compact);

            if (!compact)
            {
                searchBarFull.ApplyTo(view.SearchBarRect);
                return;
            }

            view.SearchBarRect.sizeDelta = searchBarFull.SizeDelta - compactCloseSlot;
            view.SearchBarRect.anchoredPosition = searchBarFull.AnchoredPosition - compactCloseSlot;
        }

        /// <summary>
        ///     Gives the avatar preview the whole border left of the content panel, whatever width the host has, instead of
        ///     the fixed rect the full screen layout hangs off the left of the screen.
        /// </summary>
        private void SetCompactPreview(bool compact, float contentWidth)
        {
            var previewRect = (RectTransform)view.CharacterPreviewView.transform;
            RectTransform previewImageRect = view.CharacterPreviewView.RawImage.rectTransform;

            if (!compact)
            {
                // The render texture is measured from the raw image and renewed when the preview resizes, so the image goes first
                previewImageFull.ApplyTo(previewImageRect);
                previewFull.ApplyTo(previewRect);
                return;
            }

            previewImageRect.anchorMin = Vector2.zero;
            previewImageRect.anchorMax = Vector2.one;
            previewImageRect.offsetMin = Vector2.zero;
            previewImageRect.offsetMax = Vector2.zero;

            previewRect.anchorMin = new Vector2(0f, previewRect.anchorMin.y);
            previewRect.anchorMax = new Vector2(1f, previewRect.anchorMax.y);
            previewRect.offsetMin = new Vector2(0f, previewFull.OffsetMin.y);
            previewRect.offsetMax = new Vector2(-(COMPACT_HOST_MARGIN + contentWidth), previewFull.OffsetMax.y);
        }

        // The explore panel toggles and positions its own section slot, so it stays behind when the view is borrowed
        public RectTransform GetRectTransform() =>
            homeHost;

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

        /// <summary>
        ///     Rect values the compact layout overwrites, kept so the full screen layout is put back exactly as authored.
        /// </summary>
        private readonly struct RectSnapshot
        {
            public readonly Vector2 OffsetMin;
            public readonly Vector2 OffsetMax;
            public readonly Vector2 SizeDelta;
            public readonly Vector2 AnchoredPosition;

            private readonly Vector2 anchorMin;
            private readonly Vector2 anchorMax;

            public RectSnapshot(RectTransform rect)
            {
                OffsetMin = rect.offsetMin;
                OffsetMax = rect.offsetMax;
                SizeDelta = rect.sizeDelta;
                AnchoredPosition = rect.anchoredPosition;
                anchorMin = rect.anchorMin;
                anchorMax = rect.anchorMax;
            }

            public void ApplyTo(RectTransform rect)
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.sizeDelta = SizeDelta;
                rect.anchoredPosition = AnchoredPosition;
            }
        }
    }
}
