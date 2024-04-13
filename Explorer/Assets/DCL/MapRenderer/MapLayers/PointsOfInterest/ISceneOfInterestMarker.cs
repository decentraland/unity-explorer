using DCL.MapRenderer.Culling;
using System;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.PointsOfInterest
{
    internal interface ISceneOfInterestMarker : IMapRendererMarker, IMapPositionProvider, IDisposable
    {
        bool IsVisible { get; }

        void SetData(string title, Vector3 position);

        void OnBecameVisible();

        void OnBecameInvisible();

        void SetZoom(float baseScale, float baseZoom, float zoom);

        void ResetScale(float scale);
    }
}
