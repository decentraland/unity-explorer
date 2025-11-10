using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.PlacesAPIService;
using DG.Tweening;
using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public class HomeMarker : IHomeMarker
	{
		private static readonly Vector2 TARGET_SCALE = new (1.2f, 1.2f);
		private static readonly float ANIMATION_DURATION = 0.5f;
		
		private readonly HomeMarkerObject markerObject;

		private float currentBaseScale;
		private float currentNewScale;


		public Vector3 CurrentPosition => markerObject.transform.position;
		public Vector2 Pivot => markerObject.pivot;
		public HomeMarkerObject MarkerObject => markerObject;
		public PlacesData.PlaceInfo PlaceInfo { get; set;  }

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
		
		public async UniTaskVoid AnimateSelectionAsync(CancellationToken ct)
		{
			await MarkerHelper.ScaleToAsync(markerObject.scalingTransform, TARGET_SCALE, ANIMATION_DURATION, Ease.OutBack, ct);
		}

		public async UniTaskVoid AnimateDeSelectionAsync(CancellationToken ct)
		{
			await MarkerHelper.ScaleToAsync(markerObject.scalingTransform, Vector2.one, ANIMATION_DURATION, Ease.OutBack, ct, Vector3.one);
		}
	}
}