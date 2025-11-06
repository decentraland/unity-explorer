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

		public void SetTitle(string title)
		{
			markerObject.SetTitle(title);
		}

		public void SetActive(bool active)
		{
			markerObject.gameObject.SetActive(active);
			
			if(currentBaseScale != 0)
				markerObject.SetScale(currentBaseScale, currentNewScale);
		}

		public void SetZoom(float baseScale, float baseZoom, float zoom)
		{
			currentBaseScale = baseScale;
			currentNewScale = Math.Max(zoom / baseZoom * baseScale, baseScale);
			markerObject.SetScale(currentBaseScale, currentNewScale);
		}

		public void ResetToBaseScale()
		{
			markerObject.SetScale(currentBaseScale, currentBaseScale);
		}
	}
}