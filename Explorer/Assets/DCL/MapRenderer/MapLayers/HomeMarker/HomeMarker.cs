using System;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public class HomeMarker : IHomeMarker
	{
		private readonly HomeMarkerObject markerObject;

		private float currentBaseScale;
		private float currentNewScale;


		public Vector3 CurrentPosition => markerObject.transform.position;
		public Vector2 Pivot => markerObject.pivot;

		public HomeMarker(HomeMarkerObject markerObject)
		{
			this.markerObject = markerObject;
			SetActive(false);
		}
		
		public void Dispose()
		{
			if (markerObject)
				UnityObjectUtils.SafeDestroy(markerObject.gameObject);
		}

		public void SetPosition(Vector3 position)
		{
			markerObject.transform.localPosition = position;
		}

		public void SetActive(bool active)
		{
			markerObject.gameObject.SetActive(active);
			markerObject.SetScale(currentNewScale);
		}

		public void SetZoom(float baseScale, float baseZoom, float zoom)
		{
			currentBaseScale = baseScale;
			currentNewScale = Math.Max(zoom / baseZoom * baseScale, baseScale);
			markerObject.SetScale(currentNewScale);
		}

		public void ResetToBaseScale()
		{
			currentNewScale = currentBaseScale;
			markerObject.SetScale(currentBaseScale);
		}
	}
}