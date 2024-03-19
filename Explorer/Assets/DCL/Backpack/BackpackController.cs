using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Backpack.BackpackBus;
using DCL.Backpack.CharacterPreview;
using DCL.CharacterPreview;
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
    public class BackpackController : ISection, IDisposable
    {
        private readonly BackpackView view;
        private readonly BackpackCommandBus backpackCommandBus;
        private readonly RectTransform rectTransform;
        private readonly AvatarController avatarController;

        private readonly BackpackCharacterPreviewController backpackCharacterPreviewController;

        private readonly World world;
        private readonly Entity playerEntity;
        private readonly BackpackEmoteGridController backpackEmoteGridController;
        private CancellationTokenSource animationCts;

        private CancellationTokenSource profileLoadingCts;
        private bool initialLoadingIsDone;

        public BackpackController(
            BackpackView view,
            AvatarView avatarView,
            NftTypeIconSO rarityInfoPanelBackgrounds,
            BackpackCommandBus backpackCommandBus,
            BackpackEventBus backpackEventBus,
            ICharacterPreviewFactory characterPreviewFactory,
            BackpackGridController gridController,
            BackpackInfoPanelController infoPanelController,
            World world, Entity playerEntity,
            BackpackEmoteGridController backpackEmoteGridController,
            AvatarSlotView[] avatarSlotViews)
        {
            this.view = view;
            this.backpackCommandBus = backpackCommandBus;
            this.world = world;
            this.playerEntity = playerEntity;
            this.backpackEmoteGridController = backpackEmoteGridController;

            rectTransform = view.transform.parent.GetComponent<RectTransform>();

            avatarController = new AvatarController(
                avatarView,
                avatarSlotViews,
                rarityInfoPanelBackgrounds,
                backpackCommandBus,
                backpackEventBus,
                gridController,
                infoPanelController);

            Dictionary<BackpackSections, ISection> backpackSections = new ()
            {
                { BackpackSections.Avatar, avatarController },
                { BackpackSections.Emotes, new EmotesController(view.GetComponentInChildren<EmotesView>()) },
            };

            var sectionSelectorController = new SectionSelectorController<BackpackSections>(backpackSections, BackpackSections.Avatar);

            foreach (BackpackPanelTabSelectorMapping tabSelector in view.TabSelectorMappedViews)
            {
                tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.RemoveAllListeners();

                tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.AddListener(
                    isOn =>
                    {
                        animationCts.SafeCancelAndDispose();
                        animationCts = new CancellationTokenSource();
                        sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelector.TabSelectorViews, tabSelector.Section, animationCts.Token).Forget();
                    });
            }

            backpackCharacterPreviewController = new BackpackCharacterPreviewController(view.characterPreviewView, characterPreviewFactory, backpackEventBus, world);
            view.TipsButton.onClick.AddListener(ToggleTipsContent);
            view.TipsPanelDeselectable.OnDeselectEvent += ToggleTipsContent;
        }

        public void Dispose()
        {
            view.TipsPanelDeselectable.OnDeselectEvent -= ToggleTipsContent;
            avatarController?.Dispose();
            backpackEmoteGridController.Dispose();
            animationCts.SafeCancelAndDispose();
            profileLoadingCts.SafeCancelAndDispose();
            backpackCharacterPreviewController?.Dispose();
        }

        private void ToggleTipsContent()
        {
            if (!view.TipsPanelDeselectable.gameObject.activeInHierarchy)
                view.TipsPanelDeselectable.SelectElement();

            view.TipsPanelDeselectable.gameObject.SetActive(!view.TipsPanelDeselectable.gameObject.activeInHierarchy);
        }

        private async UniTaskVoid AwaitForProfileAsync(CancellationTokenSource cts)
        {
            world.TryGet(playerEntity, out AvatarShapeComponent avatarShapeComponent);

            Avatar avatar = world.Get<Profile>(playerEntity).Avatar;

            avatarController.RequestInitialWearablesPage();
            backpackEmoteGridController.RequestAndFillEmotes(1);
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

            initialLoadingIsDone = true;
        }

        public void Activate()
        {
            if (!initialLoadingIsDone)
            {
                profileLoadingCts = new CancellationTokenSource();
                AwaitForProfileAsync(profileLoadingCts).Forget();
            }

            view.gameObject.SetActive(true);
            backpackCharacterPreviewController.OnShow();
        }

        public void Deactivate()
        {
            if (!initialLoadingIsDone)
                profileLoadingCts.SafeCancelAndDispose();
            else
                backpackCommandBus.SendCommand(new BackpackPublishProfileCommand());

            view.gameObject.SetActive(false);
            backpackCharacterPreviewController.OnHide();
        }

        public RectTransform GetRectTransform() =>
            rectTransform;
    }
}
