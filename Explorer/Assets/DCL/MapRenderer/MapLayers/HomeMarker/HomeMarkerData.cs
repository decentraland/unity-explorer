using System;
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

		public void ParseFromString(string str)
		{
			int posStart = str.IndexOf("position:", StringComparison.Ordinal) + "position:".Length;
			
			string positionStr = str.Substring(posStart);
			string[] coords = positionStr.Split(',');
			
			Position = new Vector2Int(int.Parse(coords[0]), int.Parse(coords[1]));
		}
	}
}