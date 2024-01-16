using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack.BackpackBus;
using DCL.CharacterPreview;
using DCL.Profiles;
using DCL.UI;
using DCL.Web3Authentication.Identities;
using ECS.StreamableLoading.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Backpack
    {
        public class BackpackControler : ISection, IDisposable
        {
            private readonly BackpackView view;
            private readonly BackpackCommandBus backpackCommandBus;
            private readonly RectTransform rectTransform;
            private CancellationTokenSource animationCts;
            private CancellationTokenSource profileLoadingCts;
            private readonly AvatarController avatarController;

            private bool initialLoading = false;

            private readonly BackpackCharacterPreviewController backpackCharacterPreviewController;

            public BackpackControler(
                BackpackView view,
                NftTypeIconSO rarityBackgrounds,
                NftTypeIconSO rarityInfoPanelBackgrounds,
                NftTypeIconSO categoryIcons,
                NFTColorsSO rarityColors,
                BackpackCommandBus backpackCommandBus,
                BackpackEventBus backpackEventBus,
                IWeb3IdentityCache web3IdentityCache,
                IWearableCatalog wearableCatalog,
                PageButtonView pageButtonView)
            {
                this.view = view;
                this.backpackCommandBus = backpackCommandBus;
                var backpackEquipStatusController = new BackpackEquipStatusController(backpackEventBus);
                BackpackSortController backpackSortController = new BackpackSortController(view.BackpackSortView);
                BackpackBusController busController = new BackpackBusController(wearableCatalog, backpackEventBus, backpackCommandBus, backpackEquipStatusController);

                rectTransform = view.transform.parent.GetComponent<RectTransform>();
                avatarController = new AvatarController(
                    view.GetComponentInChildren<AvatarView>(),
                    view.GetComponentsInChildren<AvatarSlotView>(),
                    rarityBackgrounds,
                    rarityInfoPanelBackgrounds,
                    categoryIcons,
                    rarityColors,
                    backpackCommandBus,
                    backpackEventBus,
                    web3IdentityCache,
                    backpackEquipStatusController,
                    backpackSortController,
                    pageButtonView);

                Dictionary<BackpackSections, ISection> backpackSections = new ()
                {
                    { BackpackSections.Avatar, avatarController },
                    { BackpackSections.Emotes,  new EmotesController(view.GetComponentInChildren<EmotesView>()) },
                };
                var sectionSelectorController = new SectionSelectorController<BackpackSections>(backpackSections, BackpackSections.Avatar);
                foreach (var tabSelector in view.TabSelectorMappedViews)
                {
                    tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.RemoveAllListeners();
                    tabSelector.TabSelectorViews.TabSelectorToggle.onValueChanged.AddListener(
                        (isOn) =>
                        {
                            animationCts.SafeCancelAndDispose();
                            animationCts = new CancellationTokenSource();
                            sectionSelectorController.OnTabSelectorToggleValueChangedAsync(isOn, tabSelector.TabSelectorViews, tabSelector.Section, animationCts.Token).Forget();
                        });
                }
                backpackCharacterPreviewController = new BackpackCharacterPreviewController(view.backpackCharacterPreviewView, new CharacterPreviewFactory(), backpackEventBus);
            }

            public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct)
            {
                await avatarController.InitialiseAssetsAsync(assetsProvisioner, ct);
            }

            public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in Entity playerEntity)
            {
                World world = builder.World;
                avatarController.InjectToWorld(ref builder, playerEntity);
                backpackCharacterPreviewController.InjectToWorld(ref builder, playerEntity);
                profileLoadingCts = new CancellationTokenSource();
                AwaitForProfileAsync(world, playerEntity, profileLoadingCts).Forget();
            }


            private async UniTaskVoid AwaitForProfileAsync(World world, Entity playerEntity, CancellationTokenSource cts)
            {
                do
                {
                    await UniTask.Delay(1000, cancellationToken: cts.Token);
                }
                while (!initialLoading);

                world.TryGet(playerEntity, out AvatarShapeComponent avatarShapeComponent);

                avatarController.RequestInitialWearablesPage();
                backpackCharacterPreviewController.OnShow();

                if(!avatarShapeComponent.WearablePromise.IsConsumed)
                    await avatarShapeComponent.WearablePromise.ToUniTaskAsync(world, cancellationToken: cts.Token);

                foreach (URN avatarSharedWearable in world.Get<Profile>(playerEntity).Avatar.SharedWearables)
                    backpackCommandBus.SendCommand(new BackpackEquipCommand(avatarSharedWearable.ToString()));
            }

            public void Activate()
            {
                view.gameObject.SetActive(true);
                initialLoading = true;
            }

            public void Deactivate()
            {
                view.gameObject.SetActive(false);
            }

            public RectTransform GetRectTransform() =>
                rectTransform;

            public void Dispose()
            {
                avatarController?.Dispose();
                animationCts.SafeCancelAndDispose();
                profileLoadingCts.SafeCancelAndDispose();
                backpackCharacterPreviewController?.Dispose();
            }
        }
    }
