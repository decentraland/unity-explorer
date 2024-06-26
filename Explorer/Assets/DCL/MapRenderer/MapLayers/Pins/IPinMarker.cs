using DCL.MapRenderer.Culling;
using System;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Pins
{
    public interface IPinMarker : IMapRendererMarker, IMapPositionProvider, IDisposable
    {
        bool IsVisible { get; }

        void SetPosition(Vector2 position);

        void SetTexture(Texture2D texture);

        void OnBecameVisible();

        void OnBecameInvisible();

        void SetZoom(float baseScale, float baseZoom, float zoom);

        void ResetScale(float scale);
    }
}
