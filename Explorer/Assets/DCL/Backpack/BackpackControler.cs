using DCL.Backpack.BackpackBus;
using DCL.UI;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Backpack
    {
        public class BackpackControler : ISection
        {
            private readonly BackpackView view;
            private readonly RectTransform rectTransform;
            private CancellationTokenSource animationCts;

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

                Dictionary<BackpackSections, ISection> backpackSections = new ()
                {
                    { BackpackSections.Avatar, new AvatarController(view.GetComponentInChildren<AvatarView>(),view.GetComponentsInChildren<AvatarSlotView>(), rarityBackgrounds, categoryIcons, rarityColors, backpackCommandBus, backpackEventBus) },
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
        }
    }
