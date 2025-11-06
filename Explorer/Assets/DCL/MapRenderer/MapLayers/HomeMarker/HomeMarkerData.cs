using System;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public struct HomeMarkerData
	{
		public const string DEFAULT_NAME = "Home";
		
		public Vector2Int Position { get; set; }
		public string Title { get; set; }

		public HomeMarkerData(Vector2Int position, string title)
		{
			Position = position;
			Title = title;
		}
		
		public HomeMarkerData(string unprocessedPosition, string title)
		{
			if (!VectorUtilities.TryParseVector2Int(unprocessedPosition, out var position))
				position = Vector2Int.zero;
			
			Position = position;
			Title = title;
		}

		public string ParseToString()
		{
			return $"position:{Position.x},{Position.y}name:{Title})";
		}

		public void ParseFromString(string str)
		{
			int posStart = str.IndexOf("position:", StringComparison.Ordinal) + "position:".Length;
			int nameStart = str.IndexOf("name:", StringComparison.Ordinal);
			
			string positionStr = str.Substring(posStart, nameStart - posStart);
			string[] coords = positionStr.Split(',');
			
			Position = new Vector2Int(int.Parse(coords[0]), int.Parse(coords[1]));
			Title = str.Substring(nameStart + "name:".Length).TrimEnd(')');
			
			if(Title.Length == 0)
				Title = DEFAULT_NAME;
		}
	}
}