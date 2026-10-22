using Cysharp.Threading.Tasks;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers.Cluster;
using DCL.PlacesAPIService;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.MapRenderer.MapLayers.SearchResults
{
    internal interface ISearchResultMarker : IMapRendererMarker, IMapPositionProvider, IDisposable, IClusterableMarker
    {
        bool IsVisible { get; }

        PlacesData.PlaceInfo? PlaceInfo { get; }

        void SetData(string title, Vector3 position, PlacesData.PlaceInfo placeInfo);

        new void OnBecameVisible();

        new void OnBecameInvisible();

        void SetZoom(float baseScale, float baseZoom, float zoom);

        new void ResetScale(float scale);

        new UniTaskVoid AnimateSelectionAsync(CancellationToken ct);

        new UniTaskVoid AnimateDeSelectionAsync(CancellationToken ct);

        GameObject? GetGameObject();
    }
}
