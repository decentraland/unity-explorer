using TMPro;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public class HomeMarkerObject : MapRendererMarkerBase
	{
		private const float Y_POSITION_OFFSET = -0.2f;
		
		[SerializeField] private SpriteRenderer homeIcon;
		[SerializeField] private TextMeshPro title;

		private Vector3 titleBasePosition;
		private float titleBaseScale;
		
		private void Awake()
		{
			titleBaseScale = title.transform.localScale.x;
			titleBasePosition = title.transform.localPosition;
		}
		
		public void SetSortingOrder(int sortingOrder)
		{
			homeIcon.sortingOrder = sortingOrder;
			title.sortingOrder = sortingOrder;
		}

		public void SetTitle(string title)
		{
			this.title.text = title;
		}
		
		public void SetScale(float baseScale, float newScale)
		{
			transform.localScale = new Vector3(newScale, newScale, 1f);

			// Apply inverse scaling to the text object
			float positionFactor = newScale / baseScale;
			float yOffset = (1 - positionFactor) * Y_POSITION_OFFSET;

			float yValue = yOffset < 0.9f
				? titleBasePosition.y + yOffset
				: titleBasePosition.y / positionFactor;

			title.transform.localPosition = new Vector3(titleBasePosition.x, yValue, titleBasePosition.z);

			float textScaleFactor = baseScale / newScale; // Calculate the inverse scale factor
			title.transform.localScale = new Vector3(titleBaseScale * textScaleFactor, titleBaseScale * textScaleFactor, 1f);
		}
	}
}