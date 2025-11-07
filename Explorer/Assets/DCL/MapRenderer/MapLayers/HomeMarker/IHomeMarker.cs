using System;
using DCL.MapRenderer.Culling;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	public interface IHomeMarker : IMapRendererMarker, IDisposable, IMapPositionProvider
	{
		void SetPosition(Vector3 position);
		
		void SetActive(bool active);
		
		
		void SetZoom(float baseScale, float baseZoom, float zoom);
		
		void ResetToBaseScale();
	}
}