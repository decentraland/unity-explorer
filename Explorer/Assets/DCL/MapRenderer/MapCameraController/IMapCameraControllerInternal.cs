using DCL.MapRenderer.CommonBehavior;
using DCL.MapRenderer.MapLayers;
using System;
using UnityEngine;

namespace DCL.MapRenderer.MapCameraController
{
    /// <summary>
    /// Contains methods that are not exposed publicly to consumers
    /// but used inside the MapRenderer's system only
    /// </summary>
    internal interface IMapCameraControllerInternal : IMapCameraController, IDisposable
    {
        event Action<float, float, int> ZoomChanged;

        event Action<IMapActivityOwner, IMapCameraControllerInternal> OnReleasing;

        Camera Camera { get; }

        /// <summary>Prepares a pooled camera for a new rent; only the renter's <see cref="IMapCameraController.Release" /> takes effect afterwards.</summary>
        void Initialize(IMapActivityOwner owner, Vector2Int textureResolution, Vector2Int zoomValuesInParcels, MapLayer layers);

        void SetActive(bool active);

        Rect GetCameraRect();
    }
}
