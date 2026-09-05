using DCL.MapRenderer.MapCameraController;
using UnityEngine;

namespace DCL.MapRenderer.Culling
{
    internal class CameraState
    {
        public IMapCameraControllerInternal CameraController;
        public Rect Rect;
    }
}
