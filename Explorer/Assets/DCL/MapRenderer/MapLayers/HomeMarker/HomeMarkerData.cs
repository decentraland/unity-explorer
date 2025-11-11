using System;
using DCL.Diagnostics;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public struct HomeMarkerData
	{
		public Vector2Int Position { get; private set; }

		public HomeMarkerData(Vector2Int position)
		{
			Position = position;
		}

		public string ParseToString()
		{
			return $"position:{Position.x.ToString()},{Position.y.ToString()}";
		}

		public bool ParseFromString(string str)
		{
			int posStart = str.IndexOf("position:", StringComparison.Ordinal) + "position:".Length;
			
			string positionStr = str.Substring(posStart);
			string[] coords = positionStr.Split(',');
			int x,y;
			if (int.TryParse(coords[0], out x) && int.TryParse(coords[1], out y))
			{
				Position = new Vector2Int(x, y);
				return true;
			}
			
			ReportHub.LogError(ReportCategory.UNSPECIFIED, "HomeMarkerData: Unable to parse position from string.");
			Position = Vector2Int.zero;
			return false;
		}
	}
}