using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public class HomePlaceEventBus
	{
		internal HomeMarkerController Controller;

		public Vector2Int? CurrentHomeCoordinates => Controller.CurrentCoordinates;
		public string? CurrentHomeWorldName => Controller.CurrentWorldName;
		public bool IsWorldHome => Controller.IsWorldHome;

		public void SetAsHome(Vector2Int coordinates)
		{
			Controller.SetMarker(coordinates);
		}

		public void SetAsHome(string worldName)
		{
			Controller.SetWorldMarker(worldName);
		}

		public void UnsetHome()
		{
			if (Controller.IsWorldHome)
				Controller.SetWorldMarker(null);
			else
				Controller.SetMarker(null);
		}

		public bool IsHome(PlacesData.PlaceInfo placeInfo)
		{
			if (!string.IsNullOrEmpty(placeInfo.world_name))
				return CurrentHomeWorldName == placeInfo.world_name;

			if (VectorUtilities.TryParseVector2Int(placeInfo.base_position, out var coordinates))
				return CurrentHomeCoordinates == coordinates;

			return false;
		}

		public void DisplayPlacesInfoPanel(Vector2Int coords) => Controller.DisplayPlacesInfoPanelAsync(coords).Forget();
	}
}
