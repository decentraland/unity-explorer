using UnityEngine;

namespace CrdtEcsBridge.Components.Special
{
    /// <summary>
    /// A special marker of the camera entity
    /// </summary>
    public readonly struct CameraComponent
    {
        public readonly Camera Camera;

        public CameraComponent(Camera camera)
        {
            Camera = camera;
        }
    }
}
