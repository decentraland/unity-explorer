using System;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public struct HomeMarkerData
	{
		public Vector2Int Position { get; set; }

		public HomeMarkerData(Vector2Int position)
		{
			Position = position;
		}
		
		public HomeMarkerData(string unprocessedPosition)
		{
			if (!VectorUtilities.TryParseVector2Int(unprocessedPosition, out var position))
				position = Vector2Int.zero;
			
			Position = position;
		}

		public string ParseToString()
		{
			return $"position:{Position.x},{Position.y}";
		}

		public void ParseFromString(string str)
		{
			int posStart = str.IndexOf("position:", StringComparison.Ordinal) + "position:".Length;
			
			string positionStr = str.Substring(posStart);
			string[] coords = positionStr.Split(',');
			
			Position = new Vector2Int(int.Parse(coords[0]), int.Parse(coords[1]));
		}
	}
}