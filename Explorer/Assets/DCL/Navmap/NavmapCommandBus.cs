using Cysharp.Threading.Tasks;
using DCL.EventsApi;
using DCL.PlacesAPIService;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Navmap
{
    public class NavmapCommandBus : INavmapBus
    {
        private const float CAMERA_MOVE_SPEED = 1;
        public delegate INavmapCommand SearchPlaceFactory(
            INavmapBus.SearchPlaceResultDelegate callback,
            INavmapBus.SearchPlaceParams @params);

        public delegate INavmapCommand<AdditionalParams> ShowPlaceInfoFactory(PlacesData.PlaceInfo placeInfo);
        public delegate INavmapCommand ShowEventInfoFactory(EventDTO @event, PlacesData.PlaceInfo? place = null);

        private readonly Stack<INavmapCommand> commands = new ();
        private readonly SearchPlaceFactory searchPlaceFactory;
        private readonly ShowPlaceInfoFactory showPlaceInfoFactory;
        private readonly ShowEventInfoFactory showEventInfoFactory;
        private readonly IPlacesAPIService placesAPIService;

        public event Action<PlacesData.PlaceInfo>? OnJumpIn;
        public event Action<PlacesData.PlaceInfo>? OnDestinationSelected;
        public event INavmapBus.SearchPlaceResultDelegate? OnPlaceSearched;
        public event Action<string?>? OnFilterByCategory;
        public event Action? OnClearPlacesFromMap;
        public event Action<Vector2, float>? OnMoveCameraTo;
        public event Action<bool>? OnZoomCamera;
        public event Action<Vector2Int, Vector2> OnLongHover;
        public event Action OnClearFilter;
        public event Action<Vector2Int, bool, bool> OnSelectPlaceFromResultsPanel;

        public NavmapCommandBus(SearchPlaceFactory searchPlaceFactory,
            ShowPlaceInfoFactory showPlaceInfoFactory,
            ShowEventInfoFactory showEventInfoFactory,
            IPlacesAPIService placesAPIService)
        {
            this.searchPlaceFactory = searchPlaceFactory;
            this.showPlaceInfoFactory = showPlaceInfoFactory;
            this.showEventInfoFactory = showEventInfoFactory;
            this.placesAPIService = placesAPIService;
        }

        public async UniTask SelectPlaceAsync(PlacesData.PlaceInfo place, CancellationToken ct,
            bool isFromSearchResults = false, Vector2Int? originalParcel = null)
        {
            INavmapCommand<AdditionalParams> command = showPlaceInfoFactory.Invoke(place);

            if (!isFromSearchResults)
                ClearPlacesFromMap();

            MoveCameraTo(place.Positions[0], CAMERA_MOVE_SPEED);
            await command.ExecuteAsync(new AdditionalParams(isFromSearchResults, originalParcel), ct);

            AddCommand(command);
        }

        public async UniTask SelectPlaceAsync(Vector2Int parcel, CancellationToken ct, bool isFromSearchResults = false)
        {
            PlacesData.PlaceInfo? place = await placesAPIService.GetPlaceAsync(parcel, ct, true);

            // TODO: show empty parcel
            if (place == null) place = new PlacesData.PlaceInfo(parcel);

            await SelectPlaceAsync(place, ct, isFromSearchResults, parcel);
        }

        public async UniTask SelectEventAsync(EventDTO @event, CancellationToken ct, PlacesData.PlaceInfo? place = null)
        {
            INavmapCommand command = showEventInfoFactory.Invoke(@event, place);

            await command.ExecuteAsync(ct);

            AddCommand(command);
        }

        public async UniTask SearchForPlaceAsync(INavmapBus.SearchPlaceParams @params, CancellationToken ct)
        {
            INavmapCommand command = searchPlaceFactory.Invoke(OnSearchPlacePerformed, @params);

            await command.ExecuteAsync(ct);

            AddCommand(command);
        }

        public async UniTask GoBackAsync(CancellationToken ct)
        {
            if (!commands.TryPop(out INavmapCommand? lastCommand)) return;
            lastCommand.Undo();
            lastCommand.Dispose();

            if (!commands.TryPeek(out var currentCommand)) return;
            await currentCommand.ExecuteAsync(ct);
        }

        public void ClearHistory()
        {
            for (var i = 0; i < commands.Count; i++)
            {
                if (!commands.TryPop(out var command)) continue;
                command.Dispose();
            }
        }

        public void SelectDestination(PlacesData.PlaceInfo place) =>
            OnDestinationSelected?.Invoke(place);

        public void JumpIn(PlacesData.PlaceInfo place) =>
            OnJumpIn?.Invoke(place);

        public void FilterByCategory(string? category) =>
            OnFilterByCategory?.Invoke(category);

        public void ClearPlacesFromMap() =>
            OnClearPlacesFromMap?.Invoke();

        public void MoveCameraTo(Vector2 position, float speed = 0f) =>
            OnMoveCameraTo?.Invoke(position, speed);

        public void ZoomCamera(bool zoomIn) =>
            OnZoomCamera?.Invoke(zoomIn);

        public void SendLongHover(Vector2Int parcel, Vector2 screenPosition) =>
            OnLongHover?.Invoke(parcel, screenPosition);

        public void ClearFilter() =>
            OnClearFilter?.Invoke();

        private void OnSearchPlacePerformed(INavmapBus.SearchPlaceParams @params,
            IReadOnlyList<PlacesData.PlaceInfo> places, int totalResultCount) =>
            OnPlaceSearched?.Invoke(@params, places, totalResultCount);

        public void SelectPlaceFromResultsPanel(Vector2Int coordinates, bool isHover, bool isClicked) =>
            OnSelectPlaceFromResultsPanel?.Invoke(coordinates, isHover, isClicked);

        private void AddCommand(INavmapCommand command)
        {
            // Replace the last command of the stack if its the same type of command
            // This is needed so the back feature always navigates to the previous screen
            // We don't want the back button repeating the search commands if you clicked many places in the map
            if (commands.TryPeek(out INavmapCommand prevCommand))
                if (command.GetType() == prevCommand.GetType())
                {
                    prevCommand = commands.Pop();
                    prevCommand.Dispose();
                }

            commands.Push(command);
        }
    }
}
