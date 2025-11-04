using DCL.Backpack.Gifting.Views;

namespace DCL.Backpack.Gifting.Presenters.Grid
{
    public interface IGridCellRenderer<TViewModel>
    {
        void Render(GiftingItemView cell, TViewModel viewModel, bool isSelected);
    }
}