using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.BackpackBus;
using System;

namespace DCL.Backpack
{
    public class BackpackInfoPanelController : IDisposable
    {
        private readonly BackpackInfoPanelView view;
        private readonly BackpackEventBus backpackEventBus;
        private NftTypeIconSO categoryIcons;

        public BackpackInfoPanelController(BackpackInfoPanelView view, BackpackEventBus backpackEventBus, NftTypeIconSO categoryIcons)
        {
            this.view = view;
            this.backpackEventBus = backpackEventBus;
            this.categoryIcons = categoryIcons;

            backpackEventBus.SelectEvent += SetPanelContent;
        }

        private void SetPanelContent(IWearable wearable)
        {
            view.Name.text = ""; //wearable.WearableDTO.Asset.metadata.
            view.Description.text = wearable.GetDescription();
            view.CategoryImage.sprite = categoryIcons.GetTypeImage(wearable.GetCategory());
            view.Creator.text = string.Format("created by <b>{0}</b>", wearable.GetCreator());
        }

        public void Dispose()
        {
            backpackEventBus.SelectEvent -= SetPanelContent;
        }
    }
}
