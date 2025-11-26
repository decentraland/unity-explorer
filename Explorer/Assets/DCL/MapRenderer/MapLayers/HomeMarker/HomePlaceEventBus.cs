using Cysharp.Threading.Tasks;
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

		public void UnsetHome()
		{
			Controller.SetMarker(null);
		}

		public void DisplayPlacesInfoPanel(Vector2Int coords) => Controller.DisplayPlacesInfoPanelAsync(coords).Forget();
	}
}