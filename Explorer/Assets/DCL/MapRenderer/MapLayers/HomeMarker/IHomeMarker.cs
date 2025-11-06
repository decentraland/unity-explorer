using System;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.HomeMarker
{
	internal interface IHomeMarker : IMapRendererMarker, IDisposable
	{
		void SetPosition(Vector3 position);
		
		void SetTitle(string title);
		
		void SetActive(bool active);
		
		void SetZoom(float baseScale, float baseZoom, float zoom);
		
		void ResetToBaseScale();
	}
}