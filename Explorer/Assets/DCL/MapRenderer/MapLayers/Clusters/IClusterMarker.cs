using Cysharp.Threading.Tasks;
using DCL.MapRenderer.Culling;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Cluster
{
    internal interface IClusterMarker : IMapRendererMarker, IMapPositionProvider, IDisposable
    {
        bool IsVisible { get; }

        void SetData(string title, Vector3 position);

        void SetCategorySprite(Sprite sprite);

        void OnBecameVisible();

        void OnBecameInvisible();

        void SetZoom(float baseScale, float baseZoom, float zoom);

        void ResetScale(float scale);

        UniTaskVoid AnimateSelectionAsync(CancellationToken ct);

        UniTaskVoid AnimateDeSelectionAsync(CancellationToken ct);

        GameObject? GetGameObject();
    }
}
