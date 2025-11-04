using System;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Styling;
using DCL.Backpack.Gifting.Views;

namespace DCL.Backpack.Gifting.Presenters.Grid
{
    public sealed class WearableCellRenderer : IGridCellRenderer<WearableViewModel>
    {
        private readonly IWearableStylingCatalog catalog;
        private readonly Func<string, bool> isEquippedUrn;
        private readonly Func<IWearable, bool> isCompatible;

        public WearableCellRenderer(IWearableStylingCatalog catalog,
            Func<string, bool> isEquippedUrn,
            Func<IWearable, bool> isCompatible)
        {
            this.catalog        = catalog;
            this.isEquippedUrn  = isEquippedUrn;
            this.isCompatible   = isCompatible;
        }

        public void Render(GiftingItemView cell, WearableViewModel vm, bool isSelected)
        {
            var w = vm.Source;

            // Styling
            // cell.RarityBackground.sprite = catalog.GetRarityBackground(w.GetRarity());
            // cell.FlapBackground.color    = catalog.GetRarityFlapColor(w.GetRarity());
            // cell.CategoryImage.sprite    = catalog.GetCategoryIcon(w.GetCategory());
            //
            // // State
            // bool equipped = isEquippedUrn(w.GetUrn());
            // cell.EquippedIcon.SetActive(equipped);
            // cell.IsEquipped = equipped;
            // cell.IsCompatibleWithBodyShape = isCompatible(w);
            // cell.ItemId = w.GetUrn();
            //
            // // Thumbnail
            // if (vm.ThumbnailState == ThumbnailState.Loaded && vm.Thumbnail != null)
            // {
            //     cell.WearableThumbnail.sprite = vm.Thumbnail;
            //     cell.LoadingView.FinishLoadingAnimation(cell.FullBackpackItem);
            // }
            // else if (vm.ThumbnailState == ThumbnailState.Error)
            // {
            //     cell.LoadingView.FinishLoadingAnimation(cell.FullBackpackItem);
            // }
            // else
            // {
            //     cell.LoadingView.StartLoadingAnimation(cell.FullBackpackItem);
            // }
            //
            // // Selection visuals (if your view supports it)
            // cell.SetSelected(isSelected);
            // cell.SetEquipButtonsState();
        }
    }
}