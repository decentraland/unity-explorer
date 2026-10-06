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

        void Initialize(Vector2Int textureResolution, Vector2Int zoomValues, MapLayer layers);

        void SetActive(bool active);

        Rect GetCameraRect();

        /// <summary>
        /// Clears the view to <paramref name="color" /> where nothing is drawn, or to the camera's own background when null
        /// </summary>
        void SetBackgroundColor(Color? color);
    }
}
