using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Backpack.BackpackBus;
using DCL.Profiles;
using DCL.UI;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<
    DCL.AvatarRendering.Wearables.Components.IWearable[],
    DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.Backpack
    {
        public class BackpackControler : ISection, IDisposable
        {
            private readonly BackpackView view;
            private readonly BackpackCommandBus backpackCommandBus;
            private readonly IProfileRepository profileRepository;
            private readonly RectTransform rectTransform;
            private CancellationTokenSource animationCts;
            private readonly AvatarController avatarController;
            private AvatarShapeComponent avatarShapeComponent;

            public BackpackControler(
                BackpackView view,
                NftTypeIconSO rarityBackgrounds,
                NftTypeIconSO categoryIcons,
                NFTColorsSO rarityColors,
                BackpackCommandBus backpackCommandBus,
                BackpackEventBus backpackEventBus,
                IProfileRepository profileRepository)
            {
                this.view = view;
                this.backpackCommandBus = backpackCommandBus;
                this.profileRepository = profileRepository;

                rectTransform = view.transform.parent.GetComponent<RectTransform>();
                avatarController = new AvatarController(view.GetComponentInChildren<AvatarView>(),view.GetComponentsInChildren<AvatarSlotView>(), rarityBackgrounds, categoryIcons, rarityColors, backpackCommandBus, backpackEventBus);

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
            }

            public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct)
            {
                await avatarController.InitialiseAssetsAsync(assetsProvisioner, ct);
            }

            public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in Entity playerEntity)
            {
                World world = builder.World;

                AwaitForProfile(world, playerEntity).Forget();
            }

            //TODO: Temporary solution to test to wait for profile to be loaded, discuss better solution
            private async UniTaskVoid AwaitForProfile(World world, Entity playerEntity)
            {
                bool canContinue = false;

                do
                {
                    await UniTask.Delay(1000);
                    world.Query(new QueryDescription().WithAll<Profile, AvatarShapeComponent>(),
                        (ref AvatarShapeComponent shapeComponent, ref Profile profile) =>
                        {
                            if (!shapeComponent.IsDirty && string.Equals(profile.UserId, "0x8e41609eD5e365Ac23C28d9625Bd936EA9C9E22c", StringComparison.CurrentCultureIgnoreCase)) canContinue = true;
                        });
                }
                while (!canContinue);

                Debug.Log("equipped wearables count " + world.Get<Profile>(playerEntity).Avatar.SharedWearables.Count);
                foreach (URN avatarSharedWearable in world.Get<Profile>(playerEntity).Avatar.SharedWearables)
                {
                    backpackCommandBus.SendCommand(new BackpackEquipCommand(avatarSharedWearable.ToString()));
                }
            }

            public void Activate()
            {
                view.gameObject.SetActive(true);
            }

            public void Deactivate()
            {
                view.gameObject.SetActive(false);
            }

            public RectTransform GetRectTransform() =>
                rectTransform;

            public void Dispose()
            {
                animationCts?.Dispose();
                avatarController?.Dispose();
            }
        }
    }
