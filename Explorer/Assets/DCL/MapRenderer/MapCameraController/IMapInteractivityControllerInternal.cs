using DCL.MapRenderer.MapLayers;
using System;

namespace DCL.MapRenderer.MapCameraController
{
    internal interface IMapInteractivityControllerInternal : IMapInteractivityController, IDisposable
    {
        void Initialize(MapLayer layers);

        void Release();

        void ApplyCameraZoom(float baseZoom, float newZoom);
    }
}
