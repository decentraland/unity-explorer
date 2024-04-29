using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.CharacterPreview;
using DCL.Backpack.EmotesSection;
using DCL.Profiles;
using DCL.UI;
using ECS.StreamableLoading.Common;
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
        private readonly BackpackView view;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly BackpackInfoPanelController emoteInfoPanelController;
        private readonly RectTransform rectTransform;
        private readonly AvatarController avatarController;
        private readonly BackpackCharacterPreviewController backpackCharacterPreviewController;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly BackpackEmoteGridController backpackEmoteGridController;
        private readonly EmotesController emotesController;
        private readonly Dictionary<BackpackSections, ISection> backpackSections;
        private readonly SectionSelectorController<BackpackSections> sectionSelectorController;
        private readonly Dictionary<BackpackSections, TabSelectorView> tabsBySections;
        private BackpackSections lastShownSection;

        private CancellationTokenSource? animationCts;
        private CancellationTokenSource? profileLoadingCts;
        private BackpackSections currentSection = BackpackSections.Avatar;
        private bool isAvatarLoaded;
        private bool instantSectionToggle;

        public BackpackController(
            BackpackView view,
            AvatarView avatarView,
            NftTypeIconSO rarityInfoPanelBackgrounds,
            BackpackCommandBus backpackCommandBus,
            BackpackEventBus backpackEventBus,
            BackpackGridController gridController,
            BackpackInfoPanelController wearableInfoPanelController,
            BackpackInfoPanelController emoteInfoPanelController,
            World world, Entity playerEntity,
            BackpackEmoteGridController backpackEmoteGridController,
            AvatarSlotView[] avatarSlotViews,
            EmotesController emotesController,
            BackpackCharacterPreviewController backpackCharacterPreviewController,
            IThumbnailProvider thumbnailProvider)
        {
            this.view = view;
            this.backpackCommandBus = backpackCommandBus;
            this.emoteInfoPanelController = emoteInfoPanelController;
            this.world = world;
            this.playerEntity = playerEntity;
            this.backpackEmoteGridController = backpackEmoteGridController;
            this.emotesController = emotesController;

            rectTransform = view.transform.parent.GetComponent<RectTransform>();

            avatarController = new AvatarController(
                avatarView,
                avatarSlotViews,
                rarityInfoPanelBackgrounds,
                backpackCommandBus,
                backpackEventBus,
                gridController,
                wearableInfoPanelController,
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
                    }
                );
            }

            this.backpackCharacterPreviewController = backpackCharacterPreviewController;
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
                lastShownSection = shownSection;
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
            emoteInfoPanelController.Dispose();
        }

        private void ToggleTipsContent()
        {
            if (!view.TipsPanelDeselectable.gameObject.activeInHierarchy)
                view.TipsPanelDeselectable.SelectElement();

            view.TipsPanelDeselectable.gameObject.SetActive(!view.TipsPanelDeselectable.gameObject.activeInHierarchy);
        }

        private async UniTaskVoid AwaitForProfileAsync(CancellationTokenSource cts)
        {
            isAvatarLoaded = false;

            world.TryGet(playerEntity, out AvatarShapeComponent avatarShapeComponent);

            Avatar avatar = world.Get<Profile>(playerEntity).Avatar;

            avatarController.RequestInitialWearablesPage();
            backpackEmoteGridController.RequestAndFillEmotes(1, true);
            backpackCharacterPreviewController.Initialize(avatar);

            if (!avatarShapeComponent.WearablePromise.IsConsumed)
                await avatarShapeComponent.WearablePromise.ToUniTaskAsync(world, cancellationToken: cts.Token);

            backpackCommandBus.SendCommand(new BackpackHideCommand(avatar.ForceRender));
            backpackCommandBus.SendCommand(new BackpackEquipWearableCommand(avatar.BodyShape.Value));

            foreach (URN w in avatar.Wearables)
                backpackCommandBus.SendCommand(new BackpackEquipWearableCommand(w.Shorten()));

            for (var i = 0; i < avatar.Emotes.Count; i++)
            {
                URN avatarEmote = avatar.Emotes[i];
                if (avatarEmote.IsNullOrEmpty()) continue;
                backpackCommandBus.SendCommand(new BackpackEquipEmoteCommand(avatarEmote.Shorten(), i));
            }

            isAvatarLoaded = true;
        }

        public void Activate()
        {
            profileLoadingCts = new CancellationTokenSource();
            AwaitForProfileAsync(profileLoadingCts).Forget();

            backpackSections[currentSection].Activate();

            view.gameObject.SetActive(true);
            backpackCharacterPreviewController.OnShow();
            foreach ((BackpackSections section, TabSelectorView? tab) in tabsBySections)
            {
                ToggleSection(section == BackpackSections.Avatar, tab, section, true);
            }
        }

        public void Deactivate()
        {
            profileLoadingCts.SafeCancelAndDispose();

            if (isAvatarLoaded)
                backpackCommandBus.SendCommand(new BackpackPublishProfileCommand());

            foreach (ISection backpackSectionsValue in backpackSections.Values)
                backpackSectionsValue.Deactivate();

            view.gameObject.SetActive(false);
            backpackCharacterPreviewController.OnHide();
        }

        public void Animate(int triggerId)
        {
            view.PanelAnimator.SetTrigger(triggerId);
            view.HeaderAnimator.SetTrigger(triggerId);
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
