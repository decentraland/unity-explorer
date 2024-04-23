using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.CharacterPreview;
using DCL.Backpack.EmotesSection;
using DCL.Profiles;
using DCL.UI;
using ECS.StreamableLoading.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.Backpack
{
    public class BackpackControler : ISection, IDisposable
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

        private CancellationTokenSource? animationCts;
        private CancellationTokenSource? profileLoadingCts;
        private BackpackSections currentSection = BackpackSections.Avatar;
        private bool isAvatarLoaded;

        public BackpackControler(
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
            BackpackCharacterPreviewController backpackCharacterPreviewController)
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
                wearableInfoPanelController);

            backpackSections = new Dictionary<BackpackSections, ISection>
            {
                { BackpackSections.Avatar, avatarController },
                { BackpackSections.Emotes, emotesController },
            };

            foreach (KeyValuePair<BackpackSections, ISection> keyValuePair in backpackSections)
                keyValuePair.Value.Deactivate();

            var sectionSelectorController = new SectionSelectorController<BackpackSections>(backpackSections, BackpackSections.Avatar);

            foreach (BackpackPanelTabSelectorMapping tabSelector in view.TabSelectorMappedViews)
            {
                tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.RemoveAllListeners();

                BackpackSections section = tabSelector.Section;

                tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.AddListener(
                    isOn =>
                    {
                        animationCts.SafeCancelAndDispose();
                        animationCts = new CancellationTokenSource();
                        sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelector.TabSelectorViews, section, animationCts.Token, false).Forget();

                        if (isOn)
                            currentSection = section;
                    });
            }

            this.backpackCharacterPreviewController = backpackCharacterPreviewController;
            view.TipsButton.onClick.AddListener(ToggleTipsContent);
            view.TipsPanelDeselectable.OnDeselectEvent += ToggleTipsContent;
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
    }
}
