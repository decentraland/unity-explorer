using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public class HomeMarkerEvents
	{
		public struct MessageHomePositionChanged
		{
			public bool IsHomeSet;
			public Vector2Int Coordinates;
			public string? WorldName;

			public MessageHomePositionChanged(Vector2Int? coordinates, string? worldName = null)
			{
				IsHomeSet = coordinates.HasValue || !string.IsNullOrEmpty(worldName);
				Coordinates = coordinates ?? Vector2Int.zero;
				WorldName = worldName;
			}
		}
	}
}