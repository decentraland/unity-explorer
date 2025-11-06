using System;
using DCL.PlacesAPIService;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public class HomePlaceEventBus
	{
		internal event Action<PlacesData.PlaceInfo> SetAsHomeRequested;
		internal event Action<PlacesData.PlaceInfo> UnsetAsHomeRequested;

		internal Func<Vector2Int, bool> IsHomeQuery;
		internal Func<Vector2Int?> GetHomeCoordinatesQuery;
		
		public void RequestSetAsHome(PlacesData.PlaceInfo placeInfo)
		{
			SetAsHomeRequested?.Invoke(placeInfo);
		}

		public void RequestUnsetAsHome(PlacesData.PlaceInfo placeInfo)
		{
			UnsetAsHomeRequested?.Invoke(placeInfo);
		}

		public bool IsHome(Vector2Int coordinates)
		{
			return IsHomeQuery?.Invoke(coordinates) ?? false;
		}

		public bool TryGetHomeCoordinates(out Vector2Int coordinates)
		{
			var result = GetHomeCoordinatesQuery?.Invoke();
			if (result == null)
			{
				coordinates = default;
				return false;
			}
			
			coordinates = result.Value;
			return true;
		}
	}
}