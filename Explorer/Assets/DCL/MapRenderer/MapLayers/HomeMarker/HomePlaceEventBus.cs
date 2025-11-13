using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public class HomePlaceEventBus
	{
		internal HomeMarkerController Controller;

		public Vector2Int? CurrentHomeCoordinates => Controller.CurrentCoordinates;
		
		public void SetAsHome(Vector2Int coordinates)
		{
			Controller.SetMarker(coordinates);
		}

		public void UnsetAsHome()
		{
			Controller.SetMarker(null);
		}
	}
}