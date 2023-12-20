using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack.BackpackBus;
using DCL.UI;
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
            private readonly RectTransform rectTransform;
            private CancellationTokenSource animationCts;
            private readonly AvatarController avatarController;

            public BackpackControler(
                BackpackView view,
                NftTypeIconSO rarityBackgrounds,
                NftTypeIconSO categoryIcons,
                NFTColorsSO rarityColors,
                BackpackCommandBus backpackCommandBus,
                BackpackEventBus backpackEventBus)
            {
                this.view = view;
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

            public async UniTask InitialiseAssetsAsync(IAssetsProvisioner assetsProvisioner, CancellationToken ct) =>
                await avatarController.InitialiseAssetsAsync(assetsProvisioner, ct);

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
