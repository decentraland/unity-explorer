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
                NavmapSearchPlaceSorting sorting = NavmapSearchPlaceSorting.None,
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
        event Action? OnClearPlacesFromMap;
        event Action<Vector2> OnMoveCameraTo;
        public event Action<bool>? OnZoomCamera;

        UniTask SelectPlaceAsync(PlacesData.PlaceInfo place, CancellationToken ct, bool clearPreviousHistory = false);

        UniTask SelectEventAsync(EventDTO @event, CancellationToken ct, PlacesData.PlaceInfo? place = null, bool clearPreviousHistory = false);

        UniTask SearchForPlaceAsync(SearchPlaceParams @params, CancellationToken ct);

        UniTask GoBackAsync(CancellationToken ct);

        void ClearHistory();

        void SelectDestination(PlacesData.PlaceInfo place);

        void JumpIn(PlacesData.PlaceInfo place);

        void FilterByCategory(string? category);

        void ClearPlacesFromMap();

        void MoveCameraTo(Vector2 position);

        void ZoomCamera(bool zoomIn);
    }
}
