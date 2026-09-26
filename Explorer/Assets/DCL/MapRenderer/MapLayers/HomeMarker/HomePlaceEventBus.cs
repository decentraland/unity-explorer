using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
    public class HomePlaceEventBus
    {
        public Vector2Int? CurrentHomeCoordinates => controller?.CurrentCoordinates;
        public string? CurrentHomeWorldName => controller?.CurrentWorldName;
        public bool IsWorldHome => controller is { IsWorldHome: true };

        private HomeMarkerController? controller;

        public void SetController(HomeMarkerController ctrl) =>
            this.controller = ctrl;

        public void SetAsHome(Vector2Int coordinates) =>
            controller?.SetMarker(coordinates);

        public void SetAsHome(string worldName) =>
            controller?.SetWorldMarker(worldName);

        public void UnsetHome()
        {
            if (controller == null)
                return;

            if (controller.IsWorldHome)
                controller.SetWorldMarker(null);
            else
                controller.SetMarker(null);
        }

        public bool IsHome(PlacesData.PlaceInfo placeInfo)
        {
            if (!string.IsNullOrEmpty(placeInfo.world_name))
                return CurrentHomeWorldName == placeInfo.world_name;

            if (IsWorldHome || !CurrentHomeCoordinates.HasValue)
                return false;

            Vector2Int homeCoordinates = CurrentHomeCoordinates.Value;

            // Home is stored as a single parcel, so any parcel of the place must count for the whole place.
            if (placeInfo.Positions is { Length: > 0 })
            {
                foreach (Vector2Int position in placeInfo.Positions)
                    if (position == homeCoordinates)
                        return true;

                return false;
            }

            return VectorUtilities.TryParseVector2Int(placeInfo.base_position, out Vector2Int coordinates) && coordinates == homeCoordinates;
        }

        public void DisplayPlacesInfoPanel(Vector2Int coords) =>
            controller?.DisplayPlacesInfoPanelAsync(coords).Forget();
    }
}
