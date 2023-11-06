using DCLServices.MapRenderer.MapCameraController;
using UnityEngine;

namespace DCLServices.MapRenderer.Culling
{
    internal class CameraState
    {
        public IMapCameraControllerInternal CameraController;
        public Rect Rect;
    }
}
