using DCL.MapRenderer.Culling;
using System;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Favorites
{
    internal interface IFavoritesMarker : IMapRendererMarker, IMapPositionProvider, IDisposable
    {
        bool IsVisible { get; }

        void SetData(string title, Vector3 position);

        void OnBecameVisible();

        void OnBecameInvisible();

        void SetZoom(float baseScale, float baseZoom, float zoom);

        void ResetScale(float baseScale);
    }
}
