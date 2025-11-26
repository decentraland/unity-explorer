using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public class HomeMarkerEvents
	{
		public struct MessageHomePositionChanged
		{
			public bool IsHomeSet;
			public Vector2Int Coordinates;

			public MessageHomePositionChanged(Vector2Int? coordinates)
			{
				IsHomeSet = coordinates.HasValue;
				Coordinates = coordinates ?? Vector2Int.zero;
			}
		}
	}
}