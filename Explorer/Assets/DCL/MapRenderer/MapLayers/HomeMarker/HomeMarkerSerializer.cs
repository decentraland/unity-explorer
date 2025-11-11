using DCL.Prefs;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public static class HomeMarkerSerializer
	{
		public static HomeMarkerData? Deserialize()
		{
			string data = DCLPlayerPrefs.GetString(DCLPrefKeys.MAP_HOME_MARKER_DATA, string.Empty);

			if (string.IsNullOrEmpty(data))
				return null;

			HomeMarkerData homeMarkerData = new HomeMarkerData();
			if (homeMarkerData.ParseFromString(data)) 
				return homeMarkerData;
			
			// Removes corrupted data from player prefs;
			Serialize(null);
			return null;
		}

		public static bool HasSerializedPosition()
		{
			return DCLPlayerPrefs.HasKey(DCLPrefKeys.MAP_HOME_MARKER_DATA);
		}

		internal static void Serialize(HomeMarkerData? data)
		{
			if (data == null)
			{
				DCLPlayerPrefs.DeleteKey(DCLPrefKeys.MAP_HOME_MARKER_DATA);
				return;
			}

			string stringData = data.Value.ParseToString();
			DCLPlayerPrefs.SetString(DCLPrefKeys.MAP_HOME_MARKER_DATA, stringData);
		}
	}
}