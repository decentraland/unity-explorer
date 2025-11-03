using DCL.Backpack.Gifting.Models;
using SuperScrollView;
using System;
using DCL.Backpack.Gifting.Views;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack.Gifting.Presenters.Grid.Adapter
{
    public class SuperScrollGridAdapter<TViewModel> where TViewModel : IGiftableItemViewModel
    {
        private const float LOAD_MORE_THRESHOLD = 0.2f;

        public event Action OnNearEndOfScroll;
        public event Action<string> OnItemSelected;

        public bool IsNearEnd { get; private set; }

        private readonly LoopGridView gridView;
        private IGiftingGridPresenter<TViewModel>? dataProvider;

        public SuperScrollGridAdapter(LoopGridView gridView)
        {
            this.gridView = gridView;
            var scrollRect = gridView.GetComponent<ScrollRect>();
            if (scrollRect != null)
                scrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }

        public void SetDataProvider(IGiftingGridPresenter<TViewModel> newProvider)
        {
            dataProvider = newProvider;
            gridView.InitGridView(dataProvider.ItemCount, OnGetItemByRowColumn);
            RefreshData();
        }

        public void RefreshData()
        {
            gridView.SetListItemCount(dataProvider.ItemCount, false);
            gridView.RefreshAllShownItem();
        }

        public void RefreshItem(int itemIndex)
        {
            gridView.RefreshItemByItemIndex(itemIndex);
        }

        public void RefreshAllShownItem()
        {
            gridView.RefreshAllShownItem();
        }

        private void OnScrollValueChanged(Vector2 pos)
        {
            IsNearEnd = dataProvider.ItemCount > 0 && pos.y <= LOAD_MORE_THRESHOLD;
            if (IsNearEnd)
                OnNearEndOfScroll?.Invoke();
        }

        private LoopGridViewItem? OnGetItemByRowColumn(LoopGridView grid, int itemIndex, int row, int col)
        {
            if (dataProvider == null || itemIndex < 0 || itemIndex >= dataProvider?.ItemCount)
                return null;

            var item = grid.NewListViewItem("GiftingItem");
            var cellView = item.GetComponent<GiftingItemView>();

            var viewModel = dataProvider.GetViewModel(itemIndex);
            cellView.Bind(viewModel, dataProvider.SelectedUrn == viewModel.Urn);

            if (viewModel.ThumbnailState == ThumbnailState.NotLoaded)
                dataProvider.RequestThumbnailLoad(itemIndex);

            cellView.OnItemSelected -= OnItemSelected;
            cellView.OnItemSelected += OnItemSelected;

            return item;
        }
    }
}