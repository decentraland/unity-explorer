using Cysharp.Threading.Tasks;
using DCL.Backpack;
using DCL.Browser;
using DCL.Events;
using DCL.EventsApi;
using DCL.FeatureFlags;
using DCL.MarketplaceCredits.Purchase;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Modules;
using DCL.UI;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.Communities.EventInfo
{
    public class EventDetailPanelController : ControllerBase<EventDetailPanelView, EventDetailPanelParameter>
    {
        private readonly EventCardActionsController eventCardActionsController;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Popup;

        private readonly ThumbnailLoader? eventCardThumbnailLoader;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly NftTypeIconSO rarityBackgrounds;
        private readonly NFTColorsSO rarityColors;
        private readonly NftTypeIconSO categoryIcons;
        private readonly UnityAppWebBrowser webBrowser;
        private readonly ImageControllerProvider imageControllerProvider;
        private readonly IMVCManager mvcManager;
        private readonly MarketplaceShopAPIClient marketplaceShopApiClient;
        private EventFeaturedItemsController? featuredItemsController;
        private CancellationTokenSource panelCts = new ();
        private CancellationTokenSource eventCardOperationsCts = new ();

        public EventDetailPanelController(ViewFactoryMethod viewFactory,
            ThumbnailLoader thumbnailLoader,
            EventCardActionsController eventCardActionsController,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            UnityAppWebBrowser webBrowser,
            ImageControllerProvider imageControllerProvider,
            IMVCManager mvcManager,
            MarketplaceShopAPIClient marketplaceShopApiClient)
            : base(viewFactory)
        {
            eventCardThumbnailLoader = thumbnailLoader;
            this.eventCardActionsController = eventCardActionsController;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.rarityBackgrounds = rarityBackgrounds;
            this.rarityColors = rarityColors;
            this.categoryIcons = categoryIcons;
            this.webBrowser = webBrowser;
            this.imageControllerProvider = imageControllerProvider;
            this.mvcManager = mvcManager;
            this.marketplaceShopApiClient = marketplaceShopApiClient;
        }

        public override void Dispose()
        {
            panelCts.SafeCancelAndDispose();
            eventCardOperationsCts.SafeCancelAndDispose();
            featuredItemsController?.Dispose();

            if (viewInstance == null) return;

            viewInstance.InterestedButtonClicked -= OnInterestedButtonClicked;
            viewInstance.JumpInButtonClicked -= OnJumpInButtonClicked;
            viewInstance.AddToCalendarButtonClicked -= OnAddToCalendarButtonClicked;
            viewInstance.AddRecurrentDateToCalendarButtonClicked -= OnAddRecurrentDateToCalendarButtonClicked;
            viewInstance.EventShareButtonClicked -= OnEventShareButtonClicked;
            viewInstance.EventCopyLinkButtonClicked -= OnEventCopyLinkButtonClicked;
        }

        protected override UniTask WaitForCloseIntentAsync(CancellationToken ct) =>
            UniTask.WhenAny(viewInstance!.GetCloseTasks());

        protected override void OnViewInstantiated()
        {
            bool isCreditPurchaseEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.CreditsWearablePurchase)
                                           && FeaturesRegistry.Instance.IsEnabled(FeatureId.UserCredits)
                                           && CreditsFeatureAccess.Instance.IsUserAllowed();

            var creditPurchaseBuyHandler = new CreditPurchaseBuyHandler(mvcManager, marketplaceShopApiClient, webBrowser, stopEmotePreview: static () => { }, isCreditPurchaseEnabled);

            featuredItemsController = new EventFeaturedItemsController(
                viewInstance!.FeaturedItemsSection,
                webRequestController,
                decentralandUrlsSource,
                rarityBackgrounds,
                rarityColors,
                categoryIcons,
                webBrowser,
                imageControllerProvider,
                creditPurchaseBuyHandler,
                isFeatureEnabled: FeaturesRegistry.Instance.IsEnabled(FeatureId.EventFeaturedItems));

            viewInstance!.InterestedButtonClicked += OnInterestedButtonClicked;
            viewInstance.JumpInButtonClicked += OnJumpInButtonClicked;
            viewInstance.AddToCalendarButtonClicked += OnAddToCalendarButtonClicked;
            viewInstance.AddRecurrentDateToCalendarButtonClicked += OnAddRecurrentDateToCalendarButtonClicked;
            viewInstance.EventShareButtonClicked += OnEventShareButtonClicked;
            viewInstance.EventCopyLinkButtonClicked += OnEventCopyLinkButtonClicked;
        }

        protected override void OnBeforeViewShow()
        {
            panelCts = panelCts.SafeRestart();
            viewInstance!.ConfigureEventData(inputData.EventData, inputData.PlaceData, eventCardThumbnailLoader!, panelCts.Token);
            featuredItemsController!.Show(inputData.EventData.Featured_item, panelCts.Token);
        }

        protected override void OnViewClose()
        {
            panelCts.SafeCancelAndDispose();
            featuredItemsController!.Clear();
        }

        private void OnEventCopyLinkButtonClicked(IEventDTO eventData) =>
            eventCardActionsController.CopyEventLink(eventData);

        private void OnAddToCalendarButtonClicked(IEventDTO eventData) =>
            eventCardActionsController.AddEventToCalendar(eventData);

        private void OnAddRecurrentDateToCalendarButtonClicked(IEventDTO eventData, DateTime utcStart) =>
            eventCardActionsController.AddEventToCalendar(eventData, utcStart);

        private void OnEventShareButtonClicked(IEventDTO eventData) =>
            eventCardActionsController.ShareEvent(eventData);

        private void OnJumpInButtonClicked(IEventDTO eventData)
        {
            eventCardOperationsCts = eventCardOperationsCts.SafeRestart();
            eventCardActionsController.JumpInEvent(eventData, eventCardOperationsCts.Token);
        }

        private void OnInterestedButtonClicked(IEventDTO eventData)
        {
            eventCardOperationsCts = eventCardOperationsCts.SafeRestart();
            eventCardActionsController.SetEventAsInterestedAsync(eventData, inputData.SummonerEventCard, viewInstance, eventCardOperationsCts.Token).Forget();
        }
    }
}
