using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public class HomeMarkerObject : MapRendererMarkerBase
	{
		[SerializeField] private SpriteRenderer homeIcon;
		
		public void SetSortingOrder(int sortingOrder)
		{
			homeIcon.sortingOrder = sortingOrder;
		}
		
		public void SetScale(float newScale)
		{
			transform.localScale = new Vector3(newScale, newScale, 1f);
		}
	}
}