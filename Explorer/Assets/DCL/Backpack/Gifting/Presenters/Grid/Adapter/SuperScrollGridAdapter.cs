using DCL.Backpack.Gifting.Models;
using SuperScrollView;
using System;
using DCL.Backpack.Gifting.Styling;
using DCL.Backpack.Gifting.Views;
using DCL.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace DCL.Backpack.Gifting.Presenters.Grid.Adapter
{
    public class SuperScrollGridAdapter<TViewModel> where TViewModel : IGiftableItemViewModel
    {
        private IWearableStylingCatalog? wearableCatalog;
        
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

        public void UseWearableStyling(IWearableStylingCatalog catalog)
        {
            wearableCatalog = catalog;
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

            if (viewModel.NftCount <= 1)
            {
                cellView.NftCountCountainer.SetActive(false);
            }
            else
            {
                cellView.NftCountCountainer.SetActive(true);
                cellView.NftCount.text = $"x{viewModel.NftCount}";
            }
            
            if (wearableCatalog != null)
            {
                if (!string.IsNullOrEmpty(viewModel.RarityId))
                {
                    cellView.FlapBackground.color  = wearableCatalog.GetRarityFlapColor(viewModel.RarityId);
                    cellView.RarityBackground.sprite = wearableCatalog.GetRarityBackground(viewModel.RarityId);
                }
                else
                {
                    cellView.FlapBackground.color  = wearableCatalog.GetRarityFlapColor("base");
                    cellView.RarityBackground.sprite = wearableCatalog.GetRarityBackground("base");
                }

                if (!string.IsNullOrEmpty(viewModel.CategoryId))
                {
                    cellView.CategoryImage.sprite = wearableCatalog.GetCategoryIcon(viewModel.CategoryId);
                }
            }

            cellView.OnItemSelected -= OnItemSelected;
            cellView.OnItemSelected += OnItemSelected;
            
            return item;
        }
    }
}