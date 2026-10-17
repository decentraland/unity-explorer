using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Navmap
{
    public interface INavmapBus
    {
        public struct SearchPlaceParams
        {
            public int page;
            public int pageSize;
            public string? text;
            public NavmapSearchPlaceFilter filter;
            public NavmapSearchPlaceSorting sorting;
            public string? category;

            public static SearchPlaceParams CreateWithDefaultParams(int page = 0, int pageSize = 50, string? text = null,
                NavmapSearchPlaceFilter filter = NavmapSearchPlaceFilter.All,
                NavmapSearchPlaceSorting sorting = NavmapSearchPlaceSorting.MostActive,
                string? category = null) =>
                new()
                {
                    category = category,
                    page = page,
                    sorting = sorting,
                    text = text,
                    pageSize = pageSize,
                    filter = filter,
                };
        }

        public delegate void SearchPlaceResultDelegate(SearchPlaceParams searchParams,
            IReadOnlyList<PlacesData.PlaceInfo> places,
            int totalResultCount);

        event Action<PlacesData.PlaceInfo> OnJumpIn;
        event Action<PlacesData.PlaceInfo>? OnDestinationSelected;
        event SearchPlaceResultDelegate? OnPlaceSearched;
        event Action<string?>? OnFilterByCategory;
        event Action? OnClearFilter;
        event Action? OnClearPlacesFromMap;
        event Action<Vector2, float> OnMoveCameraTo;
        public event Action<bool>? OnZoomCamera;
        public event Action<Vector2Int, Vector2> OnLongHover;
        public event Action<Vector2Int, bool, bool> OnSelectPlaceFromResultsPanel;

        UniTask SelectPlaceAsync(PlacesData.PlaceInfo place, CancellationToken ct, bool isFromSearchResults = false,
            // We need to know the original coordinates of the requested parcel for handling cases like roads.
            // Roads begin at one coordinate but may extend across the map.
            // The user should be able to teleport along the entirety of the road seamlessly.
            Vector2Int? originalParcel = null);
        UniTask SelectPlaceAsync(Vector2Int parcel, CancellationToken ct, bool isFromSearchResults = false);

        UniTask SelectEventAsync(EventDTO @event, CancellationToken ct, PlacesData.PlaceInfo? place = null);

        UniTask SearchForPlaceAsync(SearchPlaceParams @params, CancellationToken ct);

        UniTask GoBackAsync(CancellationToken ct);

        void ClearHistory();

        void SelectDestination(PlacesData.PlaceInfo place);

        void JumpIn(PlacesData.PlaceInfo place);

        void FilterByCategory(string? category);

        void ClearPlacesFromMap();

        void MoveCameraTo(Vector2 position, float speed = 0f);

        void ZoomCamera(bool zoomIn);

        void SendLongHover(Vector2Int parcel, Vector2 screenPosition);

        void ClearFilter();

        void SelectPlaceFromResultsPanel(Vector2Int coordinates, bool isHover, bool isClicked);
    }
}
