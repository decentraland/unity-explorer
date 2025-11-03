using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables;
using DCL.Backpack.Gifting.Commands;
using DCL.Backpack.Gifting.Models;
using DCL.Backpack.Gifting.Presenters.Grid.Adapter;
using DCL.Backpack.Gifting.Views;
using UnityEngine;
using Utility;

namespace DCL.Backpack.Gifting.Presenters.Grid
{
    public class EmoteGridPresenter : IGiftingGridPresenter<WearableViewModel>
    {
        private readonly GiftingGridView view;
        private readonly SuperScrollGridAdapter<WearableViewModel> adapter;
        private readonly IWearablesProvider wearablesProvider;
        private readonly IEventBus eventBus;
        private readonly LoadGiftableItemThumbnailCommand loadThumbnailCommand;

        private readonly Dictionary<string, WearableViewModel> viewModelsByUrn = new();
        private readonly List<string> viewModelUrnOrder = new();
        private readonly RectTransform rectTransform;
        private readonly CanvasGroup canvasGroup;

        private CancellationTokenSource? lifeCts;
        private CancellationTokenSource? searchCts;
        private IDisposable? thumbnailLoadedSubscription;

        public EmoteGridPresenter(GiftingGridView view,
            SuperScrollGridAdapter<WearableViewModel> adapter,
            IWearablesProvider wearablesProvider,
            IEventBus eventBus,
            LoadGiftableItemThumbnailCommand loadThumbnailCommand)
        {
            this.view = view;
            this.adapter = adapter;
            this.wearablesProvider = wearablesProvider;
            this.eventBus = eventBus;
            this.loadThumbnailCommand = loadThumbnailCommand;

            rectTransform = view.GetComponent<RectTransform>();
            canvasGroup = view.GetComponent<CanvasGroup>();

            lifeCts = new CancellationTokenSource();
            adapter.SetDataProvider(this);
        }

        public void Activate()
        {
        }

        public void Deactivate()
        {
        }

        public void SetSearchText(string searchText)
        {
        }

        public RectTransform GetRectTransform()
        {
            return rectTransform;
        }

        public CanvasGroup GetCanvasGroup()
        {
            return canvasGroup;
        }

        public event Action<string?>? OnSelectionChanged;
        public string? SelectedUrn { get; }
        public IReadOnlyList<IGiftableItemViewModel> viewModels { get; }

        public void LoadThumbnailForItem(int itemIndex)
        {
        }

        public int ItemCount { get; }

        public WearableViewModel GetViewModel(int itemIndex)
        {
            return new WearableViewModel();
        }

        public void RequestThumbnailLoad(int itemIndex)
        {
        }
    }
}