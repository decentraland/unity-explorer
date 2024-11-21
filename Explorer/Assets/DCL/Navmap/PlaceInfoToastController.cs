using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Navmap
{
    public class PlaceInfoToastController
    {
        private readonly PlaceInfoToastView view;
        private readonly PlaceInfoPanelController placePanelController;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IEventsApiService eventsApiService;

        private CancellationTokenSource? fetchPlaceAndShowCancellationToken;

        public PlaceInfoToastController(PlaceInfoToastView view,
            PlaceInfoPanelController placePanelController,
            IPlacesAPIService placesAPIService,
            IEventsApiService eventsApiService)
        {
            this.view = view;
            this.placePanelController = placePanelController;
            this.placesAPIService = placesAPIService;
            this.eventsApiService = eventsApiService;

            view.CloseButton.onClick.AddListener(Hide);
        }

        public void Set(Vector2Int parcel)
        {
            async UniTaskVoid FetchPlaceAndShowAsync(CancellationToken ct)
            {
                // TODO: show loading state

                placePanelController.HideLiveEvent();

                PlacesData.PlaceInfo? place = await placesAPIService.GetPlaceAsync(parcel, ct, true);

                // This scenario should never occur, as the toast is displayed over map pins,
                // which should always correspond to an existing place
                // TODO: should we show an empty parcel?
                if (place == null) return;

                placePanelController.Set(place);

                IReadOnlyList<EventDTO> events = await eventsApiService.GetEventsByParcelAsync(place.Positions, ct, onlyLiveEvents: true);

                foreach (EventDTO @event in events)
                {
                    if (!@event.live) continue;
                    placePanelController.SetLiveEvent(@event);
                    break;
                }
            }

            fetchPlaceAndShowCancellationToken = fetchPlaceAndShowCancellationToken.SafeRestart();
            FetchPlaceAndShowAsync(fetchPlaceAndShowCancellationToken.Token).Forget();
        }

        public void Show()
        {
            view.gameObject.SetActive(true);
        }

        private void Hide()
        {
            view.gameObject.SetActive(false);
        }
    }
}
