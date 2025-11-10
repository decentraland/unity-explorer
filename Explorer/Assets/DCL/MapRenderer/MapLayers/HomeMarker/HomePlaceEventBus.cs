using System;
using DCL.PlacesAPIService;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public class HomePlaceEventBus
	{
		internal event Action<Vector2Int> SetAsHomeRequested;
		internal event Action UnsetAsHomeRequested;

		internal Func<Vector2Int, bool> IsHomeQuery;
		internal Func<Vector2Int?> GetHomeCoordinatesQuery;
		
		public void RequestSetAsHome(Vector2Int coordinates)
		{
			SetAsHomeRequested?.Invoke(coordinates);
		}

		public void  RequestUnsetAsHome()
		{
			UnsetAsHomeRequested?.Invoke();
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